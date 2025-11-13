using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.WebApp.Client.Pages;
using Evermail.WebApp.Components;
using Evermail.WebApp.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddSingleton<IJwtTokenService>(sp => 
    new JwtTokenService(
        issuer: "https://api.evermail.com",
        audience: "evermail-webapp",
        ecdsaKey: ecdsaKey
    ));

builder.Services.AddAuthentication(options =>
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

builder.Services.AddAuthorization();

// Add 2FA service
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();

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
    
    // Retry connecting to SQL (container might not be ready immediately)
    var maxRetries = 10;
    var retryDelay = TimeSpan.FromSeconds(2);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await context.Database.MigrateAsync();
            await DataSeeder.SeedAsync(context);
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
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map API endpoints
var api = app.MapGroup("/api/v1");
api.MapGroup("/auth").MapAuthEndpoints();

// Map Razor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();
