using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Auth;
using Evermail.Common.DTOs.Dev;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Evermail.WebApp.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Evermail.WebApp.Services.Onboarding;

namespace Evermail.WebApp.Endpoints;

/// <summary>
/// Development-only endpoints for testing and setup.
/// These should be disabled in production.
/// </summary>
public static class DevEndpoints
{
    public static RouteGroupBuilder MapDevEndpoints(this RouteGroupBuilder group)
    {
        // Only enable in Development environment
        // Even in dev, these endpoints should require authentication
        group.MapPost("/add-admin", AddAdminRoleAsync)
            .RequireAuthorization();
        group.MapPost("/add-superadmin", AddSuperAdminRoleAsync)
            .RequireAuthorization();
        group.MapGet("/user-roles/{email}", GetUserRolesAsync)
            .RequireAuthorization();
        group.MapGet("/tenants", GetTenantsAsync)
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
        group.MapDelete("/tenants/{tenantId:guid}", DeleteTenantAsync)
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
        group.MapPost("/tenants/{tenantId:guid}/reset-onboarding", ResetTenantOnboardingAsync)
            .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
        group.MapGet("/test-keyvault", TestKeyVaultAccessAsync)
            .RequireAuthorization();
        group.MapGet("/fulltext-status", GetFullTextStatusAsync)
            .RequireAuthorization();
        group.MapGet("/ai-auth", GenerateAiAuthTokenAsync)
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> AddAdminRoleAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found. Please register first."
            ));
        }

        var result = await userManager.AddToRoleAsync(user, "Admin");

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"Failed to add Admin role: {errors}"
            ));
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new
            {
                Email = user.Email,
                Roles = roles,
                Message = $"✅ User '{email}' has been added to Admin role!"
            }
        ));
    }

    private static async Task<IResult> AddSuperAdminRoleAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found. Please register first."
            ));
        }

        // Add both Admin and SuperAdmin roles
        var adminResult = await userManager.AddToRoleAsync(user, "Admin");
        var superAdminResult = await userManager.AddToRoleAsync(user, "SuperAdmin");

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new
            {
                Email = user.Email,
                Roles = roles,
                Message = $"✅ User '{email}' has been added to SuperAdmin role!"
            }
        ));
    }

    private static async Task<IResult> GetUserRolesAsync(
        string email,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"User with email '{email}' not found."
            ));
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new
            {
                Email = user.Email,
                UserId = user.Id,
                TenantId = user.TenantId,
                Roles = roles
            }
        ));
    }

    private static async Task<IResult> TestKeyVaultAccessAsync(
        IConfiguration configuration,
        IWebHostEnvironment env,
        IServiceProvider serviceProvider)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var results = new Dictionary<string, object>();

        // Test connection strings from Key Vault
        try
        {
            var blobsConnection = configuration.GetConnectionString("blobs");
            var queuesConnection = configuration.GetConnectionString("queues");
            var sqlPassword = configuration["sql-password"];

            results["blobs-connection"] = blobsConnection != null
                ? $"✅ Found (length: {blobsConnection.Length})"
                : "❌ Not found";

            results["queues-connection"] = queuesConnection != null
                ? $"✅ Found (length: {queuesConnection.Length})"
                : "❌ Not found";

            results["sql-password"] = sqlPassword != null
                ? $"✅ Found (length: {sqlPassword.Length})"
                : "❌ Not found";

            // Try to access Key Vault directly via SecretClient (if available)
            try
            {
                var secretClient = configuration.GetSection("Aspire:Azure:Security:KeyVault").Get<object>();
                if (secretClient != null)
                {
                    results["keyvault-client"] = "✅ SecretClient configured";
                }
                else
                {
                    results["keyvault-client"] = "⚠️  SecretClient not directly accessible (using IConfiguration)";
                }
            }
            catch (Exception ex)
            {
                results["keyvault-client"] = $"⚠️  {ex.Message}";
            }

            // Verify source by checking connection string pattern
            // Key Vault connection strings contain "evermaildevstorage" (dev) or production storage account name
            if (blobsConnection?.Contains("evermaildevstorage") == true ||
                blobsConnection?.Contains("evermailprodstorage") == true)
            {
                results["source"] = "✅ Key Vault (connection string matches Key Vault pattern)";
            }
            else if (blobsConnection != null)
            {
                results["source"] = "ℹ️  User secrets (local fallback - connection string doesn't match Key Vault pattern)";
            }
            else
            {
                results["source"] = "❌ No connection string found";
            }

            // Test if services actually USE the connection strings by trying to access storage
            try
            {
                var blobClient = serviceProvider.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                var queueClient = serviceProvider.GetService<Azure.Storage.Queues.QueueServiceClient>();

                if (blobClient != null)
                {
                    results["blob-service"] = "✅ BlobServiceClient registered and using Key Vault connection string";
                    // Try to list containers (this will fail if connection string is wrong)
                    try
                    {
                        var containers = blobClient.GetBlobContainers();
                        var containerCount = containers.Count();
                        results["blob-access-test"] = $"✅ Can access blob storage ({containerCount} containers found)";
                    }
                    catch (Exception ex)
                    {
                        results["blob-access-test"] = $"❌ Cannot access blob storage: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}";
                    }
                }
                else
                {
                    results["blob-service"] = "❌ BlobServiceClient not registered";
                }

                if (queueClient != null)
                {
                    results["queue-service"] = "✅ QueueServiceClient registered and using Key Vault connection string";
                    // Try to list queues (this will fail if connection string is wrong)
                    try
                    {
                        var queues = queueClient.GetQueues();
                        var queueCount = queues.Count();
                        results["queue-access-test"] = $"✅ Can access queue storage ({queueCount} queues found)";
                    }
                    catch (Exception ex)
                    {
                        results["queue-access-test"] = $"❌ Cannot access queue storage: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}";
                    }
                }
                else
                {
                    results["queue-service"] = "❌ QueueServiceClient not registered";
                }
            }
            catch (Exception ex)
            {
                results["service-test-error"] = ex.Message;
            }
        }
        catch (Exception ex)
        {
            results["error"] = ex.Message;
        }

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new
            {
                Message = "Key Vault access test results",
                Results = results,
                Timestamp = DateTime.UtcNow
            }
        ));
    }

    private static async Task<IResult> GetFullTextStatusAsync(
        EvermailDbContext context,
        IWebHostEnvironment env,
        bool reset = false)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        if (reset)
        {
            EmailEndpoints.ClearFullTextCircuitBreaker();
        }

        var installed = await context.Database
            .SqlQueryRaw<int>("SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')")
            .AsAsyncEnumerable()
            .FirstAsync();

        var dbEnabled = await context.Database
            .SqlQueryRaw<int>("SELECT is_fulltext_enabled FROM sys.databases WHERE name = DB_NAME()")
            .AsAsyncEnumerable()
            .FirstAsync();

        var catalogs = await context.Database
            .SqlQueryRaw<string>("SELECT name FROM sys.fulltext_catalogs ORDER BY name")
            .ToListAsync();

        var indexes = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT o.name
                FROM sys.fulltext_indexes fi
                JOIN sys.objects o ON fi.object_id = o.object_id
                ORDER BY o.name
                """)
            .ToListAsync();

        var population = await context.Database
            .SqlQueryRaw<FullTextPopulationRow>(
                """
                SELECT TOP (5)
                    catalog_id AS CatalogId,
                    table_id AS TableId,
                    status AS Status,
                    start_time AS StartTime,
                    completion_time AS CompletionTime
                FROM sys.dm_fts_index_population
                ORDER BY start_time DESC
                """)
            .ToListAsync();

        var populationInfo = population.Select(p => new
        {
            p.CatalogId,
            p.TableId,
            p.Status,
            StatusDescription = DescribePopulationStatus(p.Status),
            p.StartTime,
            p.CompletionTime
        }).ToList();

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new
            {
                ServiceInstalled = installed == 1,
                DatabaseEnabled = dbEnabled == 1,
                Catalogs = catalogs,
                Indexes = indexes,
                Population = populationInfo,
                CircuitBreakerOpen = EmailEndpoints.IsFullTextCircuitOpen,
                CircuitReset = reset,
                Timestamp = DateTime.UtcNow
            }
        ));
    }

    private static async Task<IResult> GenerateAiAuthTokenAsync(
        HttpContext httpContext,
        IWebHostEnvironment env,
        IOptions<AiImpersonationOptions> options,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var aiOptions = options.Value;
        if (!aiOptions.Enabled)
        {
            return Results.Unauthorized();
        }

        if (!IsTriggerPresent(httpContext.Request, aiOptions))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Missing AI impersonation trigger."
            ));
        }

        if (string.IsNullOrWhiteSpace(aiOptions.UserEmail))
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "AI impersonation user email is not configured."
            ));
        }

        var user = await userManager.FindByEmailAsync(aiOptions.UserEmail);
        if (user is null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"AI impersonation user '{aiOptions.UserEmail}' not found."
            ));
        }

        var roles = await userManager.GetRolesAsync(user);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var tokenPair = await jwtTokenService.GenerateTokenPairAsync(user, roles, ipAddress);

        var response = new AuthResponse(
            Token: tokenPair.AccessToken,
            RefreshToken: tokenPair.RefreshToken,
            TokenExpires: tokenPair.AccessTokenExpires,
            RefreshTokenExpires: tokenPair.RefreshTokenExpires,
            User: new UserDto(user.Id, user.TenantId, user.Email ?? string.Empty, user.FirstName, user.LastName, user.TwoFactorEnabled)
        );

        return Results.Ok(new ApiResponse<AuthResponse>(
            Success: true,
            Data: response
        ));
    }

    private static bool IsTriggerPresent(HttpRequest request, AiImpersonationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.TriggerQueryKey))
        {
            return false;
        }

        var triggerKey = options.TriggerQueryKey;
        var triggerValue = options.TriggerValue ?? "1";
        var query = request.Query;

        if (!query.TryGetValue(triggerKey, out var values))
        {
            return false;
        }

        return values.Any(v => string.Equals(v, triggerValue, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IResult> GetTenantsAsync(
        EvermailDbContext context,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var tenants = await context.Tenants
            .AsNoTracking()
            .Include(t => t.Users)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var userRoles = await (from ur in context.UserRoles.AsNoTracking()
                               join role in context.Roles.AsNoTracking() on ur.RoleId equals role.Id
                               select new { ur.UserId, role.Name })
                               .ToListAsync(cancellationToken);

        var roleLookup = userRoles
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Name).ToList());

        var encryptionSettings = await context.TenantEncryptionSettings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var encryptionLookup = encryptionSettings
            .GroupBy(s => s.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(OnboardingStatusCalculator.IsEncryptionConfigured).Any(result => result));

        var mailboxTenantIds = await context.Mailboxes
            .AsNoTracking()
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var mailboxSet = mailboxTenantIds.ToHashSet();

        var dto = tenants.Select(t =>
        {
            var users = t.Users
                .OrderBy(u => u.Email)
                .Select(u =>
                {
                    roleLookup.TryGetValue(u.Id, out var roles);
                    var roleList = roles ?? new List<string>();
                    return new DevTenantUserDto(
                        u.Id,
                        u.Email ?? string.Empty,
                        u.FirstName,
                        u.LastName,
                        roleList.Contains("Admin", StringComparer.OrdinalIgnoreCase),
                        roleList.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase),
                        u.CreatedAt);
                })
                .ToList();

            var encryptionConfigured = encryptionLookup.TryGetValue(t.Id, out var configured) && configured;
            var hasMailbox = mailboxSet.Contains(t.Id);

            return new DevTenantDto(
                t.Id,
                t.Name,
                t.Slug,
                t.CreatedAt,
                users.Count,
                users,
                t.OnboardingPlanConfirmedAt.HasValue,
                encryptionConfigured,
                t.PaymentAcknowledgedAt.HasValue,
                hasMailbox);
        }).ToList();

        return Results.Ok(new ApiResponse<IReadOnlyList<DevTenantDto>>(
            Success: true,
            Data: dto));
    }

    private static async Task<IResult> DeleteTenantAsync(
        Guid tenantId,
        EvermailDbContext context,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var tenantExists = await context.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken);

        if (!tenantExists)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Tenant not found"));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var commands = new (string Sql, object[] Parameters)[]
            {
                ("DELETE FROM Attachments WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM EmailRecipients WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM MailboxEncryptionStates WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM MailboxDeletionQueue WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM MailboxUploads WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM EmailMessages WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM Mailboxes WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM EmailThreads WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM AuditLogs WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM RefreshTokens WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM TenantEncryptionSettings WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM Subscriptions WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM AspNetUserTokens WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE TenantId = {0})", new object[]{ tenantId }),
                ("DELETE FROM AspNetUserLogins WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE TenantId = {0})", new object[]{ tenantId }),
                ("DELETE FROM AspNetUserClaims WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE TenantId = {0})", new object[]{ tenantId }),
                ("DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE TenantId = {0})", new object[]{ tenantId }),
                ("DELETE FROM AspNetUsers WHERE TenantId = {0}", new object[]{ tenantId }),
                ("DELETE FROM Tenants WHERE Id = {0}", new object[]{ tenantId })
            };

            foreach (var (sql, parameters) in commands)
            {
                await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: $"Failed to delete tenant: {ex.Message}"));
        }

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { TenantId = tenantId }));
    }

    private static async Task<IResult> ResetTenantOnboardingAsync(
        Guid tenantId,
        EvermailDbContext context,
        IWebHostEnvironment env,
        IOnboardingStatusService onboardingStatusService,
        CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
        {
            return Results.NotFound();
        }

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(
                Success: false,
                Error: "Tenant not found"));
        }

        var reset = await onboardingStatusService.ResetAsync(tenantId, cancellationToken);

        if (!reset)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: "Failed to reset onboarding state."));
        }

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { TenantId = tenantId }));
    }

    private static string DescribePopulationStatus(int status) => status switch
    {
        0 => "Idle",
        1 => "Full population in progress",
        2 => "Paused",
        3 => "Throttled",
        4 => "Recovering",
        _ => $"Status {status}"
    };

    private sealed record FullTextPopulationRow(
        int CatalogId,
        int TableId,
        int Status,
        DateTime? StartTime,
        DateTime? CompletionTime);
}

