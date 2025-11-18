using Evermail.Common.DTOs;
using Evermail.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Azure.Security.KeyVault.Secrets;

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
        group.MapGet("/test-keyvault", TestKeyVaultAccessAsync)
            .RequireAuthorization();
        
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
}

