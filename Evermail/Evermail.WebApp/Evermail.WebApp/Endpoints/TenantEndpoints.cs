using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Evermail.WebApp.Services.Onboarding;

namespace Evermail.WebApp.Endpoints;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/onboarding/status", GetOnboardingStatusAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        group.MapPut("/onboarding/security", SetSecurityPreferenceAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        group.MapPut("/onboarding/payment", AcknowledgePaymentAsync)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        var encryption = group.MapGroup("/encryption")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        encryption.MapGet("/", GetEncryptionSettingsAsync);
        encryption.MapPut("/", UpsertEncryptionSettingsAsync);
        encryption.MapPost("/test", TestEncryptionSettingsAsync);
        encryption.MapGet("/history", GetEncryptionHistoryAsync);
        encryption.MapPost("/offline", UploadOfflineBundleAsync);
        encryption.MapGet("/bundles", GetEncryptionBundlesAsync);
        encryption.MapPost("/bundles", CreateEncryptionBundleAsync);
        encryption.MapDelete("/bundles/{bundleId:guid}", DeleteEncryptionBundleAsync);

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
        IOnboardingStatusService onboardingStatusService,
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

        onboardingStatusService.Invalidate(tenantContext.TenantId);

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

    private static async Task<IResult> UploadOfflineBundleAsync(
        OfflineByokUploadRequest request,
        ITenantEncryptionService service,
        TenantContext tenantContext,
        IOnboardingStatusService onboardingStatusService,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        try
        {
            var dto = await service.UploadOfflineBundleAsync(
                tenantContext.TenantId,
                tenantContext.UserId,
                request,
                cancellationToken);

            onboardingStatusService.Invalidate(tenantContext.TenantId);

            return Results.Ok(new ApiResponse<TenantEncryptionSettingsDto>(
                Success: true,
                Data: dto));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, ex.Message));
        }
    }

    private static async Task<IResult> GetEncryptionBundlesAsync(
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var bundles = await service.GetBundlesAsync(tenantContext.TenantId, cancellationToken);
        return Results.Ok(new ApiResponse<IReadOnlyList<TenantEncryptionBundleDto>>(true, bundles));
    }

    private static async Task<IResult> CreateEncryptionBundleAsync(
        CreateTenantEncryptionBundleRequest request,
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        try
        {
            var dto = await service.CreateBundleAsync(
                tenantContext.TenantId,
                tenantContext.UserId,
                request,
                cancellationToken);

            return Results.Ok(new ApiResponse<TenantEncryptionBundleDto>(true, dto));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, ex.Message));
        }
    }

    private static async Task<IResult> DeleteEncryptionBundleAsync(
        Guid bundleId,
        ITenantEncryptionService service,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        try
        {
            await service.DeleteBundleAsync(tenantContext.TenantId, bundleId, tenantContext.UserId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, ex.Message));
        }
    }

    private static async Task<IResult> GetOnboardingStatusAsync(
        EvermailDbContext context,
        TenantContext tenantContext,
        HttpContext httpContext,
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

        var encryptionSettings = await context.TenantEncryptionSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantContext.TenantId, cancellationToken);

        var encryptionConfigured = encryptionSettings is not null &&
            OnboardingStatusCalculator.IsEncryptionConfigured(encryptionSettings);

        var hasMailbox = await context.Mailboxes
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantContext.TenantId, cancellationToken);

        var securityPreference = NormalizeSecurityPreference(tenant.SecurityPreference);
        var paymentAcknowledgedAt = tenant.PaymentAcknowledgedAt;
        var identityProvider = ResolveIdentityProvider(httpContext.User);

        var dto = new TenantOnboardingStatusDto(
            HasAdmin: hasAdmin,
            EncryptionConfigured: encryptionConfigured,
            HasMailbox: hasMailbox,
            PlanConfirmed: tenant.OnboardingPlanConfirmedAt.HasValue,
            SubscriptionTier: tenant.SubscriptionTier,
            SecurityPreference: securityPreference,
            PaymentAcknowledged: paymentAcknowledgedAt.HasValue,
            PaymentAcknowledgedAt: paymentAcknowledgedAt,
            IdentityProvider: identityProvider);

        return Results.Ok(new ApiResponse<TenantOnboardingStatusDto>(
            Success: true,
            Data: dto));
    }

    private static async Task<IResult> SetSecurityPreferenceAsync(
        SetSecurityPreferenceRequest request,
        EvermailDbContext context,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Mode))
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, "mode is required"));
        }

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Tenant not found."));
        }

        var normalized = NormalizeSecurityPreference(request.Mode);
        tenant.SecurityPreference = normalized;
        tenant.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ApiResponse<SecurityPreferenceResponse>(
            Success: true,
            Data: new SecurityPreferenceResponse(normalized)));
    }

    private static async Task<IResult> AcknowledgePaymentAsync(
        PaymentAcknowledgementRequest request,
        EvermailDbContext context,
        TenantContext tenantContext,
        IOnboardingStatusService onboardingStatusService,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (request is null || request.Acknowledged != true)
        {
            return Results.BadRequest(new ApiResponse<object>(false, null, "acknowledged must be true"));
        }

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Results.NotFound(new ApiResponse<object>(false, null, "Tenant not found."));
        }

        if (!tenant.PaymentAcknowledgedAt.HasValue)
        {
            tenant.PaymentAcknowledgedAt = DateTime.UtcNow;
            tenant.PaymentAcknowledgedByUserId = tenantContext.UserId;
            tenant.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            onboardingStatusService.Invalidate(tenantContext.TenantId);
        }

        return Results.Ok(new ApiResponse<PaymentAcknowledgementResponse>(
            Success: true,
            Data: new PaymentAcknowledgementResponse(tenant.PaymentAcknowledgedAt)));
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
        IOnboardingStatusService onboardingStatusService,
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
        onboardingStatusService.Invalidate(tenantContext.TenantId);

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

    private static string NormalizeSecurityPreference(string? mode)
    {
        if (string.Equals(mode, "BYOK", StringComparison.OrdinalIgnoreCase))
        {
            return "BYOK";
        }

        return "QuickStart";
    }

    private static string? ResolveIdentityProvider(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var provider = user.FindFirst("idp")?.Value
            ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/identityprovider")?.Value
            ?? user.Identity?.AuthenticationType;

        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        if (provider.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            return "Google";
        }

        if (provider.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
            provider.Contains("live", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft";
        }

        return provider;
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


