using Evermail.Domain.Entities;
using Evermail.Common.Runtime;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Configuration;
using Evermail.MigrationService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

// Load secrets from Azure Key Vault (same pattern as WebApp/Worker).
try
{
    builder.Configuration.AddAzureKeyVaultSecrets(connectionName: "key-vault");
    Console.WriteLine("✅ Azure Key Vault secrets loaded (MigrationService)");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Key Vault not accessible for MigrationService: {ex.Message}");
    Console.WriteLine("ℹ️  Falling back to local configuration (MigrationService)");
}

// Database context (Migrations run here; no TenantContext required).
var runtimeMode = EvermailRuntimeResolver.ResolveMode(builder.Configuration, builder.Environment);
var connectionString = EvermailRuntimeResolver.ResolveConnectionString(builder.Configuration, runtimeMode, "evermaildb");
if (string.IsNullOrWhiteSpace(connectionString) && builder.Environment.IsDevelopment())
{
    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'evermaildb' is not configured for MigrationService.");
}

builder.Services.AddDbContext<EvermailDbContext>(options => options.UseSqlServer(connectionString));

// Configure ASP.NET Core Identity (required for DataSeeder)
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
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<EvermailDbContext>()
.AddRoles<IdentityRole<Guid>>()
.AddDefaultTokenProviders();

var host = builder.Build();
host.Run();
