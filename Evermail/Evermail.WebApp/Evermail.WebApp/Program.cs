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

// Load secrets from Azure Key Vault
// - In production: Uses managed identity (automatic)
// - In local dev: Uses Azure CLI credentials (if logged in) or falls back to user secrets
// The Key Vault connection is configured via Aspire resource reference
try
{
    builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");
    Console.WriteLine("✅ Azure Key Vault secrets loaded (using DefaultAzureCredential)");
}
catch (Exception ex)
{
    // Fallback to user secrets if Key Vault is not accessible (e.g., not logged into Azure CLI)
    Console.WriteLine($"⚠️  Key Vault not accessible: {ex.Message}");
    Console.WriteLine("ℹ️  Falling back to local user secrets");
}

// Add database context
var connectionString = builder.Configuration.GetConnectionString("evermaildb") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<EvermailDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);
    // TenantContext is injected via constructor in EvermailDbContext
}, ServiceLifetime.Scoped);

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
.AddEntityFrameworkStores<EvermailDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var ecdsaKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
builder.Services.AddSingleton(ecdsaKey);
builder.Services.AddScoped<IJwtTokenService>(sp => 
    new JwtTokenService(
        issuer: "https://api.evermail.com",
        audience: "evermail-webapp",
        ecdsaKey: ecdsaKey,
        context: sp.GetRequiredService<EvermailDbContext>()
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

// Configure Azure Blob Storage
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("blobs");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Blob storage connection string 'blobs' is not configured");
    }
    return new Azure.Storage.Blobs.BlobServiceClient(connectionString);
});
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Configure Azure Queue Storage
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("queues");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Queue storage connection string 'queues' is not configured");
    }
    return new Azure.Storage.Queues.QueueServiceClient(connectionString);
});
builder.Services.AddScoped<IQueueService, QueueService>();

// Add authentication state services for Blazor
builder.Services.AddScoped<Evermail.WebApp.Services.IAuthenticationStateService, Evermail.WebApp.Services.AuthenticationStateService>();
builder.Services.AddScoped<AuthenticationStateProvider, Evermail.WebApp.Services.CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

// Add tenant context resolver
builder.Services.AddScoped<TenantContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    
    Console.WriteLine($"DEBUG TenantContext: HttpContext exists: {httpContext != null}");
    Console.WriteLine($"DEBUG TenantContext: User exists: {httpContext?.User != null}");
    Console.WriteLine($"DEBUG TenantContext: IsAuthenticated: {httpContext?.User?.Identity?.IsAuthenticated}");
    
    if (httpContext?.User?.Identity?.IsAuthenticated != true)
    {
        Console.WriteLine("DEBUG TenantContext: User NOT authenticated, returning empty GUIDs");
        return new TenantContext { TenantId = Guid.Empty, UserId = Guid.Empty };
    }

    // Try multiple claim types for UserId (JWT validation might map "sub" to different claim type)
    var userIdClaim = httpContext.User.FindFirst("sub")?.Value
        ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
    
    var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value;
    
    Console.WriteLine($"DEBUG TenantContext: tenant_id claim: {tenantIdClaim}");
    Console.WriteLine($"DEBUG TenantContext: sub/userId claim: {userIdClaim}");
    Console.WriteLine($"DEBUG TenantContext: All claims: {string.Join(", ", httpContext.User.Claims.Select(c => $"{c.Type}={c.Value?.Substring(0, Math.Min(20, c.Value.Length))}..."))}");


    return new TenantContext
    {
        TenantId = string.IsNullOrEmpty(tenantIdClaim) ? Guid.Empty : Guid.Parse(tenantIdClaim),
        UserId = string.IsNullOrEmpty(userIdClaim) ? Guid.Empty : Guid.Parse(userIdClaim)
    };
});

builder.Services.AddHttpContextAccessor();

// Configure HttpClient for Blazor Server components to call own API
// Use IHttpClientFactory with named client instead of trying to resolve NavigationManager at startup
builder.Services.AddHttpClient("EverMailAPI", (sp, client) =>
{
    // BaseAddress will be set by the component when NavigationManager is available
    // For now, use relative URLs in API calls
});

// Also add a default HttpClient for backward compatibility
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("EverMailAPI"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add API controllers/endpoints
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Database migrations are handled by Evermail.MigrationService in Aspire
// Migrations run automatically before this service starts (via WaitForCompletion in AppHost)
// Data seeding is also handled by the MigrationService
// This ensures migrations complete before the app starts accepting requests

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
api.MapGroup("/upload").MapUploadEndpoints();
api.MapGroup("/mailboxes").MapMailboxEndpoints();
api.MapGroup("/emails").MapEmailEndpoints();
api.MapGroup("/attachments").MapAttachmentEndpoints();

// Development-only endpoints (disabled in production)
if (app.Environment.IsDevelopment())
{
    api.MapGroup("/dev").MapDevEndpoints();
}

// Map Razor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();
