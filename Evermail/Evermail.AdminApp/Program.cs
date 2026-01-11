using System.Security.Claims;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Evermail.AdminApp.Auth;
using Evermail.AdminApp.Ops;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.SectionName));
var runtimeState = new AdminRuntimeState();
builder.Services.AddSingleton(runtimeState);

// SameSite mitigation for Safari / older user agents during external auth flows (OAuth redirect/correlation cookies).
// See Microsoft docs: https://learn.microsoft.com/aspnet/core/security/samesite
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
    options.OnAppendCookie = ctx => CheckSameSite(ctx.Context, ctx.CookieOptions);
    options.OnDeleteCookie = ctx => CheckSameSite(ctx.Context, ctx.CookieOptions);
});

static void CheckSameSite(HttpContext httpContext, CookieOptions options)
{
    if (options.SameSite != SameSiteMode.None)
    {
        return;
    }

    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    if (DisallowsSameSiteNone(userAgent))
    {
        options.SameSite = SameSiteMode.Unspecified;
    }
}

static bool DisallowsSameSiteNone(string userAgent)
{
    if (string.IsNullOrWhiteSpace(userAgent))
    {
        return false;
    }

    // iOS 12 (Safari/WkWebView/Chrome use same networking stack)
    if (userAgent.Contains("CPU iPhone OS 12", StringComparison.OrdinalIgnoreCase) ||
        userAgent.Contains("iPad; CPU OS 12", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    // macOS 10.14 Safari (Mojave) behavior
    if (userAgent.Contains("Macintosh; Intel Mac OS X 10_14", StringComparison.OrdinalIgnoreCase) &&
        userAgent.Contains("Version/", StringComparison.OrdinalIgnoreCase) &&
        userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    // Chrome 50-69
    if (userAgent.Contains("Chrome/5", StringComparison.OrdinalIgnoreCase) ||
        userAgent.Contains("Chrome/6", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

// Provide TokenCredential (DefaultAzureCredential ordered chain) for Key Vault reads/writes.
builder.Services.AddSingleton<TokenCredential>(_ =>
{
    var credentialOptions = new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = !builder.Environment.IsDevelopment(),
    };

    return new DefaultAzureCredential(credentialOptions);
});

// Load secrets from Azure Key Vault (read path).
// - In production: Uses managed identity (automatic)
// - In local dev: Uses Azure CLI credentials (if logged in) or falls back to local configuration
try
{
    builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");
    Console.WriteLine("✅ Azure Key Vault secrets loaded (AdminApp)");
    runtimeState.KeyVaultLoaded = true;
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Key Vault not accessible for AdminApp: {ex.Message}");
    Console.WriteLine("ℹ️  Falling back to local configuration (AdminApp)");
}

// AdminApp DB access (no tenant query filters; AdminApp is Evermail-internal only).
var connectionString = builder.Configuration.GetConnectionString("evermaildb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'evermaildb' is not configured for AdminApp. " +
        "Run via Aspire AppHost so SQL is wired, or set ConnectionStrings:evermaildb in user-secrets.");
}

builder.Services.AddDbContext<EvermailDbContext>(options => options.UseSqlServer(connectionString));

// Identity (Admin tools only): enables UserManager/RoleManager backed by the same database.
// AdminApp authentication remains OAuth-only; this is purely for ops actions like role assignment.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<EvermailDbContext>();

// OAuth-only auth for SuperAdmins (Evermail-internal). No password auth.
var authBuilder = builder.Services.AddAuthentication(options =>
{
    // Keep Cookies as the default so OAuth sign-in works reliably across browsers (incl. Safari).
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "evermail_admin";
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
    options.SlidingExpiration = true;
});

// Google OAuth (required in our design, but only wired when credentials exist)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);
if (googleEnabled)
{
    authBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId = googleClientId!;
    options.ClientSecret = googleClientSecret!;
    options.CallbackPath = "/signin-google-admin";
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.SaveTokens = true;
    options.Scope.Add("email");
    options.Scope.Add("profile");
    // Be explicit; correlation cookie behavior is critical for Safari.
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    options.Events.OnCreatingTicket = context =>
    {
        var optionsSnapshot = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminAuthOptions>>().Value;
        var email = AdminAuthPolicy.GetEmail(context.Principal ?? new ClaimsPrincipal());

        if (!AdminAuthPolicy.IsAllowed(email, optionsSnapshot))
        {
            context.Fail("Not allowlisted for Evermail AdminApp.");
            return Task.CompletedTask;
        }

        // Mark allowlisted users as SuperAdmin for AdminApp authorization checks.
        var identity = context.Principal?.Identity as ClaimsIdentity;
        identity?.AddClaim(new Claim(ClaimTypes.Role, AdminAuthPolicy.SuperAdminRole));
        identity?.AddClaim(new Claim("evermail_admin_provider", "Google"));

        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        context.Response.Redirect("/login?error=oauth_failed");
        context.HandleResponse();
        return Task.CompletedTask;
    };
    });
}

// Microsoft OAuth (required in our design, but only wired when credentials exist)
var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
var microsoftEnabled = !string.IsNullOrWhiteSpace(microsoftClientId) && !string.IsNullOrWhiteSpace(microsoftClientSecret);
if (microsoftEnabled)
{
    authBuilder.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
{
    options.ClientId = microsoftClientId!;
    options.ClientSecret = microsoftClientSecret!;
    options.CallbackPath = "/signin-microsoft-admin";
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.SaveTokens = true;
    options.Scope.Add("email");
    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

    options.Events.OnCreatingTicket = context =>
    {
        var optionsSnapshot = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminAuthOptions>>().Value;
        var email = AdminAuthPolicy.GetEmail(context.Principal ?? new ClaimsPrincipal());

        if (!AdminAuthPolicy.IsAllowed(email, optionsSnapshot))
        {
            context.Fail("Not allowlisted for Evermail AdminApp.");
            return Task.CompletedTask;
        }

        var identity = context.Principal?.Identity as ClaimsIdentity;
        identity?.AddClaim(new Claim(ClaimTypes.Role, AdminAuthPolicy.SuperAdminRole));
        identity?.AddClaim(new Claim("evermail_admin_provider", "Microsoft"));

        return Task.CompletedTask;
    };

    options.Events.OnRemoteFailure = context =>
    {
        context.Response.Redirect("/login?error=oauth_failed");
        context.HandleResponse();
        return Task.CompletedTask;
    };
    });
}

builder.Services.AddAuthorization(options =>
{
    // Do not set a fallback policy at the HTTP endpoint level; Blazor routes should still render /login.
    // Route guarding is handled in App.razor via AuthorizeRouteView.
    options.FallbackPolicy = null;

    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole(AdminAuthPolicy.SuperAdminRole));
});

// Key Vault write client (used later for runtime switching).
builder.Services.AddSingleton(sp =>
{
    var credential = sp.GetRequiredService<TokenCredential>();
    var vaultUri = builder.Configuration["AdminKeyVault:Uri"]
                  ?? builder.Configuration["ConnectionStrings:key-vault"]
                  ?? builder.Configuration["ConnectionStrings__key-vault"];

    if (string.IsNullOrWhiteSpace(vaultUri))
    {
        var defaultVaultName = builder.Environment.IsDevelopment()
            ? "evermail-dev-kv"
            : "evermail-prod-kv";
        vaultUri = $"https://{defaultVaultName}.vault.azure.net/";
    }

    return new SecretClient(new Uri(vaultUri), credential);
});
builder.Services.AddSingleton<AdminKeyVaultService>();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMudServices();

var app = builder.Build();

// Auth endpoints (challenge + logout)
app.MapGet("/auth/login/google", (HttpContext httpContext, string? returnUrl) =>
{
    if (!googleEnabled)
    {
        return Results.Redirect("/login?error=google_not_configured");
    }

    var redirect = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    return Results.Challenge(new AuthenticationProperties { RedirectUri = redirect }, [GoogleDefaults.AuthenticationScheme]);
});

app.MapGet("/auth/login/microsoft", (HttpContext httpContext, string? returnUrl) =>
{
    if (!microsoftEnabled)
    {
        return Results.Redirect("/login?error=microsoft_not_configured");
    }

    var redirect = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    return Results.Challenge(new AuthenticationProperties { RedirectUri = redirect }, [MicrosoftAccountDefaults.AuthenticationScheme]);
});

app.MapPost("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Dev-only bypass: signs in via the normal cookie scheme (does NOT replace OAuth; it just shortcuts it).
// - Enabled only in Development and when explicitly toggled on.
// - Localhost only.
app.MapPost("/auth/dev-bypass", async (HttpContext httpContext, string? returnUrl) =>
{
    if (!app.Environment.IsDevelopment() || !builder.Configuration.GetValue("AdminAuth:DevBypassEnabled", false))
    {
        return Results.NotFound();
    }

    if (httpContext.Connection.RemoteIpAddress is { } ip && !System.Net.IPAddress.IsLoopback(ip))
    {
        return Results.Forbid();
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, "Dev Bypass"),
        new(ClaimTypes.Email, "dev-bypass@evermail.ai"),
        new(ClaimTypes.Role, AdminAuthPolicy.SuperAdminRole),
        new("evermail_admin_provider", "DevBypass"),
    };

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    var redirect = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    return Results.Redirect(redirect);
});

// Self-restart (works in Azure Container Apps; local Aspire will typically restart the process as well).
app.MapPost("/ops/restart", (IHostApplicationLifetime lifetime) =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(250);
        lifetime.StopApplication();
    });

    return Results.Ok();
}).RequireAuthorization("SuperAdminOnly");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
