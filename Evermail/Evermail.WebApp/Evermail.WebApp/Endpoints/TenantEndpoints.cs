using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
        encryption.MapGet("/history", GetEncryptionHistoryAsync);

        group.MapGet("/plans", GetSubscriptionPlansAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        group.MapPut("/subscription", UpdateSubscriptionAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

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

    private static async Task<IResult> GetEncryptionHistoryAsync(
        [AsParameters] HistoryQuery query,
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var limit = query.Limit.HasValue ? Math.Clamp(query.Limit.Value, 1, 100) : 20;
        var items = await service.GetEncryptionHistoryAsync(tenantContext.TenantId, limit, cancellationToken);

        return Results.Ok(new ApiResponse<IReadOnlyList<TenantEncryptionHistoryItemDto>>(
            Success: true,
            Data: items));
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

        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, "Tenant not found."));
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
            HasMailbox: hasMailbox,
            PlanConfirmed: tenant.OnboardingPlanConfirmedAt.HasValue,
            SubscriptionTier: tenant.SubscriptionTier);

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

    private static async Task<IResult> GetSubscriptionPlansAsync(
        EvermailDbContext context,
        CancellationToken cancellationToken)
    {
        var plans = await context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var dtos = plans
            .Select(plan => new SubscriptionPlanDto(
                plan.Name,
                plan.DisplayName,
                plan.Description ?? string.Empty,
                plan.PriceMonthly,
                plan.PriceYearly,
                plan.Currency,
                plan.MaxStorageGB,
                plan.MaxFileSizeGB,
                plan.MaxUsers,
                plan.MaxMailboxes,
                plan.DisplayOrder == 2,
                ParseFeatures(plan.Features)))
            .ToList();

        return Results.Ok(new ApiResponse<IReadOnlyList<SubscriptionPlanDto>>(
            Success: true,
            Data: dtos));
    }

    private static async Task<IResult> UpdateSubscriptionAsync(
        SelectSubscriptionPlanRequest request,
        EvermailDbContext context,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PlanName))
        {
            return Results.BadRequest(new ApiResponse<object>(false, "planName is required."));
        }

        var plan = await context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.IsActive && p.Name == request.PlanName, cancellationToken);

        if (plan is null)
        {
            return Results.BadRequest(new ApiResponse<object>(false, "Subscription plan not found."));
        }

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, "Tenant not found."));
        }

        tenant.SubscriptionTier = plan.Name;
        tenant.MaxStorageGB = plan.MaxStorageGB;
        tenant.MaxUsers = plan.MaxUsers;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.OnboardingPlanConfirmedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ApiResponse<object>(
            Success: true,
            Data: new { message = $"Subscription updated to {plan.DisplayName}." }));
    }

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

    private sealed record HistoryQuery([property: FromQuery(Name = "limit")] int? Limit);

    private static IReadOnlyList<string> ParseFeatures(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        try
        {
            var features = JsonSerializer.Deserialize<string[]>(raw);
            return features ?? Array.Empty<string>();
        }
        catch
        {
            return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}


