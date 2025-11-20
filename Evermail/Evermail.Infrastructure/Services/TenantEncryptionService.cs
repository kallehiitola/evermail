using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Services;

public class TenantEncryptionService : ITenantEncryptionService
{
    private readonly EvermailDbContext _context;
    private readonly TokenCredential _credential;

    public TenantEncryptionService(EvermailDbContext context, TokenCredential credential)
    {
        _context = context;
        _credential = credential;
    }

    public async Task<TenantEncryptionSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(tenantId, cancellationToken);
        return MapToDto(entity);
    }

    public async Task<TenantEncryptionSettingsDto> UpsertSettingsAsync(
        Guid tenantId,
        Guid userId,
        UpsertTenantEncryptionSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(tenantId, cancellationToken);

        entity.KeyVaultUri = SanitizeUri(request.KeyVaultUri);
        entity.KeyVaultKeyName = request.KeyVaultKeyName.Trim();
        entity.KeyVaultKeyVersion = string.IsNullOrWhiteSpace(request.KeyVaultKeyVersion)
            ? null
            : request.KeyVaultKeyVersion.Trim();
        entity.KeyVaultTenantId = request.KeyVaultTenantId.Trim();
        entity.ManagedIdentityObjectId = string.IsNullOrWhiteSpace(request.ManagedIdentityObjectId)
            ? null
            : request.ManagedIdentityObjectId.Trim();

        entity.EncryptionPhase = string.IsNullOrWhiteSpace(entity.KeyVaultUri)
            ? "NotConfigured"
            : "BYOKConfigured";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<TenantEncryptionTestResultDto> TestAccessAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(tenantId, cancellationToken);

        if (string.IsNullOrWhiteSpace(entity.KeyVaultUri) ||
            string.IsNullOrWhiteSpace(entity.KeyVaultKeyName) ||
            string.IsNullOrWhiteSpace(entity.KeyVaultTenantId))
        {
            return new TenantEncryptionTestResultDto(
                Success: false,
                Message: "Key Vault settings are incomplete. Please save the URI, key name, and tenant ID before running a validation.",
                Timestamp: DateTime.UtcNow);
        }

        try
        {
            var keyClient = new KeyClient(new Uri(entity.KeyVaultUri), _credential);
            KeyVaultKey keyVaultKey;

            if (string.IsNullOrWhiteSpace(entity.KeyVaultKeyVersion))
            {
                keyVaultKey = await keyClient.GetKeyAsync(entity.KeyVaultKeyName, cancellationToken: cancellationToken);
                entity.KeyVaultKeyVersion = keyVaultKey.Properties.Version;
            }
            else
            {
                keyVaultKey = await keyClient.GetKeyAsync(
                    entity.KeyVaultKeyName,
                    entity.KeyVaultKeyVersion,
                    cancellationToken: cancellationToken);
            }

            entity.LastVerifiedAt = DateTime.UtcNow;
            entity.LastVerificationMessage =
                $"Key '{keyVaultKey.Name}' reachable. Version {entity.KeyVaultKeyVersion}.";

            await _context.SaveChangesAsync(cancellationToken);

            return new TenantEncryptionTestResultDto(
                Success: true,
                Message: entity.LastVerificationMessage!,
                Timestamp: entity.LastVerifiedAt.Value);
        }
        catch (Exception ex)
        {
            entity.LastVerifiedAt = DateTime.UtcNow;
            entity.LastVerificationMessage = $"Validation failed: {ex.Message}";
            await _context.SaveChangesAsync(cancellationToken);

            return new TenantEncryptionTestResultDto(
                Success: false,
                Message: entity.LastVerificationMessage,
                Timestamp: entity.LastVerifiedAt.Value);
        }
    }

    private async Task<TenantEncryptionSettings> GetOrCreateEntityAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entity = await _context.TenantEncryptionSettings
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (entity != null)
        {
            return entity;
        }

        entity = new TenantEncryptionSettings
        {
            TenantId = tenantId,
            EncryptionPhase = "NotConfigured",
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEncryptionSettings.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity;
    }

    private static string SanitizeUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.ToString().TrimEnd('/');
        }

        return value.Trim();
    }

    private static TenantEncryptionSettingsDto MapToDto(TenantEncryptionSettings entity) =>
        new(
            IsConfigured: !string.IsNullOrWhiteSpace(entity.KeyVaultUri) &&
                          !string.IsNullOrWhiteSpace(entity.KeyVaultKeyName),
            EncryptionPhase: entity.EncryptionPhase,
            KeyVaultUri: entity.KeyVaultUri,
            KeyVaultKeyName: entity.KeyVaultKeyName,
            KeyVaultKeyVersion: entity.KeyVaultKeyVersion,
            KeyVaultTenantId: entity.KeyVaultTenantId,
            ManagedIdentityObjectId: entity.ManagedIdentityObjectId,
            UpdatedAt: entity.UpdatedAt ?? entity.CreatedAt,
            LastVerifiedAt: entity.LastVerifiedAt,
            LastVerificationMessage: entity.LastVerificationMessage,
            IsSecureKeyReleaseConfigured: entity.IsSecureKeyReleaseConfigured,
            SecureKeyReleaseConfiguredAt: entity.SecureKeyReleaseConfiguredAt);
}


