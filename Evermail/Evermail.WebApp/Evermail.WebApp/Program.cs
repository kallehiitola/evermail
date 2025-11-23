using Amazon.SecurityToken;
using Azure.Core;
using Azure.Identity;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Configuration;
using Evermail.Infrastructure.Services;
using Evermail.Infrastructure.Services.Encryption;
using Evermail.Infrastructure.Services.Archives;
using Evermail.WebApp.Client.Pages;
using Evermail.WebApp.Components;
using Evermail.WebApp.Endpoints;
using Evermail.WebApp.Configuration;
using Evermail.WebApp.Middleware;
using Evermail.WebApp.Services;
using Evermail.WebApp.Services.Onboarding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire telemetry, service discovery)
builder.AddServiceDefaults();

// Provide TokenCredential (DefaultAzureCredential ordered chain) for Key Vault + BYOK services
// Ref: https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains#defaultazurecredential-overview
builder.Services.AddSingleton<TokenCredential>(_ =>
{
    var credentialOptions = new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = !builder.Environment.IsDevelopment(),
    };

    return new DefaultAzureCredential(credentialOptions);
});

builder.Services.AddMemoryCache();

builder.Services.Configure<AiImpersonationOptions>(
    builder.Configuration.GetSection(AiImpersonationOptions.SectionName));

builder.Services.Configure<OfflineByokOptions>(
    builder.Configuration.GetSection("OfflineByok"));

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
    // JWT Bearer is used for API endpoints
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Only challenge (return 401) for API routes, not Blazor routes
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnChallenge = context =>
        {
            // If this is a Blazor route (not /api/*), suppress the challenge
            // This allows Blazor routes to render and handle authorization at component level
            var path = context.Request.Path.Value ?? "";
            if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                context.HandleResponse();
                return Task.CompletedTask;
            }
            // For API routes, let the default challenge behavior proceed (return 401)
            return Task.CompletedTask;
        }
    };

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

builder.Services.AddAuthorization(options =>
{
    // Don't set a fallback policy - let Blazor components handle authorization
    // This allows anonymous access at HTTP level, authorization happens at component level
    options.FallbackPolicy = null;
});

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
builder.Services.AddScoped<IArchiveFormatDetector, ArchiveFormatDetector>();

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
builder.Services.AddSingleton<IAmazonSecurityTokenService>(_ => new AmazonSecurityTokenServiceClient());
builder.Services.AddSingleton<IAwsKmsConnector, AwsKmsConnector>();
builder.Services.AddSingleton<IKeyWrappingProvider, EvermailManagedWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingProvider, AzureKeyVaultWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingProvider, AwsKmsWrappingProvider>();
builder.Services.AddSingleton<IOfflineByokKeyProtector, OfflineByokKeyProtector>();
builder.Services.AddSingleton<IKeyWrappingProvider, OfflineByokWrappingProvider>();
builder.Services.AddSingleton<IKeyWrappingService, KeyWrappingService>();
builder.Services.AddScoped<ITenantEncryptionService, TenantEncryptionService>();
builder.Services.AddScoped<IMailboxEncryptionStateService, MailboxEncryptionStateService>();

// Add authentication state services for Blazor
builder.Services.AddScoped<Evermail.WebApp.Services.IAuthenticationStateService, Evermail.WebApp.Services.AuthenticationStateService>();
builder.Services.AddScoped<AuthenticationStateProvider, Evermail.WebApp.Services.CustomAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddMudServices();
builder.Services.AddScoped<IDateFormatService, DateFormatService>();
builder.Services.AddScoped<IOnboardingStatusService, OnboardingStatusService>();

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
app.UseMiddleware<AiImpersonationMiddleware>();
app.UseMiddleware<OnboardingRedirectMiddleware>();

// Map API endpoints
var api = app.MapGroup("/api/v1");
// Auth endpoints allow anonymous (login, register, OAuth)
var authApi = api.MapGroup("/auth").AllowAnonymous();
authApi.MapAuthEndpoints().MapOAuthEndpoints();
// Other API endpoints require authorization
api.MapGroup("/upload").MapUploadEndpoints().RequireAuthorization();
api.MapGroup("/mailboxes").MapMailboxEndpoints().RequireAuthorization();
api.MapGroup("/emails").MapEmailEndpoints().RequireAuthorization();
api.MapGroup("/attachments").MapAttachmentEndpoints().RequireAuthorization();
api.MapGroup("/tenants").MapTenantEndpoints();
api.MapGroup("/users").MapUserEndpoints().RequireAuthorization();

// Development-only endpoints (disabled in production)
if (app.Environment.IsDevelopment())
{
    api.MapGroup("/dev").MapDevEndpoints();
}

// Map Razor components (allow anonymous - authorization handled by AuthorizeRouteView)
// Blazor pages handle authorization at the component level, not HTTP middleware level
// JWT Bearer authentication is configured to skip Blazor routes (see OnChallenge handler above)
app.UseAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

app.Run();
