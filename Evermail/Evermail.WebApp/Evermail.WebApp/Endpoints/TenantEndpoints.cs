using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Evermail.WebApp.Endpoints;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/onboarding/status", GetOnboardingStatusAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        var encryption = group.MapGroup("/encryption")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        encryption.MapGet("/", GetEncryptionSettingsAsync);
        encryption.MapPut("/", UpsertEncryptionSettingsAsync);
        encryption.MapPost("/test", TestEncryptionSettingsAsync);

        return group;
    }

    private static async Task<IResult> GetEncryptionSettingsAsync(
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var dto = await service.GetSettingsAsync(tenantContext.TenantId, cancellationToken);

        return Results.Ok(new ApiResponse<TenantEncryptionSettingsDto>(
            Success: true,
            Data: dto));
    }

    private static async Task<IResult> UpsertEncryptionSettingsAsync(
        UpsertTenantEncryptionSettingsRequest request,
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return Results.BadRequest(new ApiResponse<object>(
                Success: false,
                Error: validationError));
        }

        var dto = await service.UpsertSettingsAsync(
            tenantContext.TenantId,
            tenantContext.UserId,
            request,
            cancellationToken);

        return Results.Ok(new ApiResponse<TenantEncryptionSettingsDto>(
            Success: true,
            Data: dto));
    }

    private static async Task<IResult> TestEncryptionSettingsAsync(
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var dto = await service.TestAccessAsync(tenantContext.TenantId, cancellationToken);

        return Results.Ok(new ApiResponse<TenantEncryptionTestResultDto>(
            Success: dto.Success,
            Data: dto,
            Error: dto.Success ? null : dto.Message));
    }

    private static async Task<IResult> GetOnboardingStatusAsync(
        EvermailDbContext context,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var adminRoleId = await context.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var hasAdmin = adminRoleId != Guid.Empty &&
            await (from ur in context.UserRoles
                   join user in context.Users on ur.UserId equals user.Id
                   where ur.RoleId == adminRoleId && user.TenantId == tenantContext.TenantId
                   select ur).AnyAsync(cancellationToken);

        var encryptionConfigured = await context.TenantEncryptionSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantContext.TenantId)
            .Select(s => IsEncryptionConfigured(s))
            .FirstOrDefaultAsync(cancellationToken);

        var hasMailbox = await context.Mailboxes
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantContext.TenantId, cancellationToken);

        var dto = new TenantOnboardingStatusDto(
            HasAdmin: hasAdmin,
            EncryptionConfigured: encryptionConfigured,
            HasMailbox: hasMailbox);

        return Results.Ok(new ApiResponse<TenantOnboardingStatusDto>(
            Success: true,
            Data: dto));
    }

    private static readonly string[] SupportedProviders =
    [
        "AzureKeyVault",
        "AwsKms",
        "EvermailManaged"
    ];

    private static string? ValidateRequest(UpsertTenantEncryptionSettingsRequest request)
    {
        if (request is null)
        {
            return "Request payload is required.";
        }

        var provider = NormalizeProvider(request.Provider);
        if (!Array.Exists(SupportedProviders, p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Provider '{request.Provider}' is not supported.";
        }

        if (provider.Equals("AwsKms", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Aws is null)
            {
                return "AWS settings are required.";
            }

            if (string.IsNullOrWhiteSpace(request.Aws.KmsKeyArn) || !request.Aws.KmsKeyArn.Trim().StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
            {
                return "A valid AWS KMS Key ARN is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Aws.IamRoleArn) || !request.Aws.IamRoleArn.Trim().StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
            {
                return "A valid AWS IAM Role ARN is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Aws.AccountId))
            {
                return "AWS account ID is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Aws.Region))
            {
                return "AWS region is required.";
            }

            return null;
        }

        if (provider.Equals("EvermailManaged", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Default to Azure Key Vault validation
        if (request.Azure is null)
        {
            return "Azure Key Vault settings are required.";
        }

        if (string.IsNullOrWhiteSpace(request.Azure.KeyVaultUri))
        {
            return "Key Vault URI is required.";
        }

        if (!Uri.TryCreate(request.Azure.KeyVaultUri, UriKind.Absolute, out _))
        {
            return "Key Vault URI must be an absolute URI.";
        }

        if (string.IsNullOrWhiteSpace(request.Azure.KeyVaultKeyName))
        {
            return "Key name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Azure.KeyVaultTenantId))
        {
            return "Azure AD tenant ID for the Key Vault is required.";
        }

        return null;
    }

    private static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return "AzureKeyVault";
        }

        return provider.Trim();
    }

    private static bool IsEncryptionConfigured(TenantEncryptionSettings settings)
    {
        var provider = settings.Provider ?? "AzureKeyVault";

        return provider switch
        {
            "AwsKms" => !string.IsNullOrWhiteSpace(settings.AwsKmsKeyArn) &&
                        !string.IsNullOrWhiteSpace(settings.AwsIamRoleArn),
            "EvermailManaged" => true,
            _ => !string.IsNullOrWhiteSpace(settings.KeyVaultUri) &&
                 !string.IsNullOrWhiteSpace(settings.KeyVaultKeyName)
        };
    }
}


