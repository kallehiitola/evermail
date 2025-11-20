using Evermail.Common.DTOs;
using Evermail.Common.DTOs.Tenant;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services;

namespace Evermail.WebApp.Endpoints;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this RouteGroupBuilder group)
    {
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

    private static string? ValidateRequest(UpsertTenantEncryptionSettingsRequest request)
    {
        if (request is null)
        {
            return "Request payload is required.";
        }

        if (string.IsNullOrWhiteSpace(request.KeyVaultUri))
        {
            return "Key Vault URI is required.";
        }

        if (!Uri.TryCreate(request.KeyVaultUri, UriKind.Absolute, out _))
        {
            return "Key Vault URI must be an absolute URI.";
        }

        if (string.IsNullOrWhiteSpace(request.KeyVaultKeyName))
        {
            return "Key name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.KeyVaultTenantId))
        {
            return "Azure AD tenant ID for the Key Vault is required.";
        }

        return null;
    }
}


