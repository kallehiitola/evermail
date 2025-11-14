using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.WebApp.Client.Pages;
using Evermail.WebApp.Components;
using Evermail.WebApp.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire telemetry, service discovery)
builder.AddServiceDefaults();

// Add database context
var connectionString = builder.Configuration.GetConnectionString("evermaildb") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<EmailDbContext>((serviceProvider, options) =>
{
    var tenantContext = serviceProvider.GetService<TenantContext>();
    options.UseSqlServer(connectionString);
});

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password requirements
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false; // For MVP, set to true in production
})
.AddEntityFrameworkStores<EmailDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var ecdsaKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
builder.Services.AddSingleton(ecdsaKey);
builder.Services.AddScoped<IJwtTokenService>(sp => 
    new JwtTokenService(
        issuer: "https://api.evermail.com",
        audience: "evermail-webapp",
        ecdsaKey: ecdsaKey,
        context: sp.GetRequiredService<EmailDbContext>()
    ));

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "https://api.evermail.com",
        ValidAudience = "evermail-webapp",
        IssuerSigningKey = new ECDsaSecurityKey(ecdsaKey),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

// Configure OAuth providers (only if credentials are available)

// Google OAuth (optional)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = googleClientId;
        googleOptions.ClientSecret = googleClientSecret;
        googleOptions.CallbackPath = "/signin-google";
    });
    Console.WriteLine("✅ Google OAuth configured");
}
else
{
    Console.WriteLine("⚠️  Google OAuth not configured (missing credentials)");
}

// Microsoft OAuth (optional)
var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
{
    authBuilder.AddMicrosoftAccount(microsoftOptions =>
    {
        microsoftOptions.ClientId = microsoftClientId;
        microsoftOptions.ClientSecret = microsoftClientSecret;
        microsoftOptions.CallbackPath = "/signin-microsoft";
    });
    Console.WriteLine("✅ Microsoft OAuth configured");
}
else
{
    Console.WriteLine("⚠️  Microsoft OAuth not configured (missing credentials)");
}

builder.Services.AddAuthorization();

// Add 2FA service
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();

// Add authentication state services for Blazor
builder.Services.AddScoped<Evermail.WebApp.Services.IAuthenticationStateService, Evermail.WebApp.Services.AuthenticationStateService>();
builder.Services.AddScoped<AuthenticationStateProvider, Evermail.WebApp.Services.CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// Add tenant context resolver
builder.Services.AddScoped<TenantContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    
    if (httpContext?.User?.Identity?.IsAuthenticated != true)
    {
        return new TenantContext { TenantId = Guid.Empty, UserId = Guid.Empty };
    }

    var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value;
    var userIdClaim = httpContext.User.FindFirst("sub")?.Value;

    return new TenantContext
    {
        TenantId = string.IsNullOrEmpty(tenantIdClaim) ? Guid.Empty : Guid.Parse(tenantIdClaim),
        UserId = string.IsNullOrEmpty(userIdClaim) ? Guid.Empty : Guid.Parse(userIdClaim)
    };
});

builder.Services.AddHttpContextAccessor();

// Configure HttpClient for Blazor Server components to call own API
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add API controllers/endpoints
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Seed database (with retry for SQL container startup)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    
    // Retry connecting to SQL (container might not be ready immediately)
    var maxRetries = 10;
    var retryDelay = TimeSpan.FromSeconds(2);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await context.Database.MigrateAsync();
            await DataSeeder.SeedAsync(context, roleManager);
            break; // Success!
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            Console.WriteLine($"Waiting for database... (attempt {i + 1}/{maxRetries})");
            await Task.Delay(retryDelay);
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// .NET 10: Use MapStaticAssets instead of UseStaticFiles for Blazor Web Apps
// MapStaticAssets replaces UseBlazorFrameworkFiles and optimizes static asset delivery
app.MapStaticAssets();

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map API endpoints
var api = app.MapGroup("/api/v1");
api.MapGroup("/auth").MapAuthEndpoints().MapOAuthEndpoints();

// Map Razor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();
