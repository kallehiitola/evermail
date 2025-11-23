using System.Security.Cryptography;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services.Encryption;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Services;

public class TenantEncryptionService : ITenantEncryptionService
{
    private const string ProviderAzureKeyVault = "AzureKeyVault";
    private const string ProviderAwsKms = "AwsKms";
    private const string ProviderEvermailManaged = "EvermailManaged";

    private readonly EvermailDbContext _context;
    private readonly TokenCredential _credential;
    private readonly IAwsKmsConnector _awsKmsConnector;

    private const string ProviderOffline = "Offline";

    public TenantEncryptionService(
        EvermailDbContext context,
        TokenCredential credential,
        IAwsKmsConnector awsKmsConnector,
        IOfflineByokKeyProtector offlineByokKeyProtector)
    {
        _context = context;
        _credential = credential;
        _awsKmsConnector = awsKmsConnector;
        _offlineByokKeyProtector = offlineByokKeyProtector;
    }

    private readonly IOfflineByokKeyProtector _offlineByokKeyProtector;

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

        var normalizedProvider = NormalizeProvider(request.Provider);
        entity.Provider = normalizedProvider;

        switch (normalizedProvider)
        {
            case ProviderAzureKeyVault:
                ApplyAzureSettings(entity, request.Azure);
                ClearAwsFields(entity);
                break;
            case ProviderAwsKms:
                ApplyAwsSettings(entity, tenantId, request.Aws);
                ClearAzureFields(entity);
                break;
            case ProviderEvermailManaged:
                // Automatic provisioning path (future). For now treat as not configured and clear manual fields.
                ClearAzureFields(entity);
                ClearAwsFields(entity);
                entity.EncryptionPhase = "NotConfigured";
                break;
            case ProviderOffline:
                throw new InvalidOperationException("Use the offline BYOK upload endpoint to configure offline keys.");
            default:
                throw new NotSupportedException($"Encryption provider '{normalizedProvider}' is not supported.");
        }

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<TenantEncryptionSettingsDto> UploadOfflineBundleAsync(
        Guid tenantId,
        Guid userId,
        OfflineByokUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Passphrase) || request.Passphrase.Length < 12)
        {
            throw new InvalidOperationException("Passphrase must be at least 12 characters.");
        }

        var entity = await GetOrCreateEntityAsync(tenantId, cancellationToken);

        var masterKey = DecryptOfflineBundle(request);

        try
        {
            entity.Provider = ProviderOffline;
            entity.OfflineMasterKeyCiphertext = _offlineByokKeyProtector.Protect(masterKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        ClearAzureFields(entity);
        ClearAwsFields(entity);

        entity.OfflineBundleVersion = request.Version?.Trim();
        entity.OfflineKeyChecksum = request.Checksum?.Trim();
        entity.OfflineTenantLabel = string.IsNullOrWhiteSpace(request.TenantLabel)
            ? null
            : request.TenantLabel.Trim();
        entity.OfflineKeyCreatedAt = DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc);
        entity.EncryptionPhase = "BYOKConfigured";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;
        entity.LastVerifiedAt = DateTime.UtcNow;
        entity.LastVerificationMessage = "Offline BYOK bundle uploaded.";

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(entity);
    }

    public async Task<TenantEncryptionTestResultDto> TestAccessAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateEntityAsync(tenantId, cancellationToken);

        return entity.Provider switch
        {
            ProviderAzureKeyVault or ProviderEvermailManaged => await TestAzureKeyVaultAsync(entity, cancellationToken),
            ProviderAwsKms => await TestAwsKmsAsync(entity, cancellationToken),
            ProviderOffline => new TenantEncryptionTestResultDto(
                Success: !string.IsNullOrWhiteSpace(entity.OfflineMasterKeyCiphertext),
                Message: string.IsNullOrWhiteSpace(entity.OfflineMasterKeyCiphertext)
                    ? "Offline BYOK master key not uploaded."
                    : "Offline bundle uploaded.",
                Timestamp: DateTime.UtcNow),
            _ => new TenantEncryptionTestResultDto(
                Success: false,
                Message: $"Provider '{entity.Provider}' cannot be validated yet.",
                Timestamp: DateTime.UtcNow)
        };
    }

    public async Task<IReadOnlyList<TenantEncryptionHistoryItemDto>> GetEncryptionHistoryAsync(
        Guid tenantId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        return await _context.MailboxEncryptionStates
            .AsNoTracking()
            .Where(es => es.TenantId == tenantId)
            .OrderByDescending(es => es.CreatedAt)
            .Take(limit)
            .Select(es => new TenantEncryptionHistoryItemDto(
                es.Id,
                es.MailboxId,
                es.MailboxUploadId,
                es.Provider,
                es.Algorithm,
                es.CreatedAt,
                es.ProviderKeyVersion,
                es.WrapRequestId,
                es.LastUnwrapRequestId,
                es.ProviderMetadata))
            .ToListAsync(cancellationToken);
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

    private void ApplyAzureSettings(TenantEncryptionSettings entity, AzureTenantEncryptionUpsertDto? request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Azure settings are required when provider is AzureKeyVault.");
        }

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
    }

    private void ApplyAwsSettings(TenantEncryptionSettings entity, Guid tenantId, AwsTenantEncryptionUpsertDto? request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("AWS settings are required when provider is AwsKms.");
        }

        entity.AwsAccountId = request.AccountId.Trim();
        entity.AwsRegion = request.Region.Trim();
        entity.AwsKmsKeyArn = request.KmsKeyArn.Trim();
        entity.AwsIamRoleArn = request.IamRoleArn.Trim();
        entity.AwsExternalId ??= GenerateAwsExternalId(tenantId);

        entity.EncryptionPhase = "WrapOnly";
    }

    private static void ClearAzureFields(TenantEncryptionSettings entity)
    {
        entity.KeyVaultUri = null;
        entity.KeyVaultKeyName = null;
        entity.KeyVaultKeyVersion = null;
        entity.KeyVaultTenantId = null;
        entity.ManagedIdentityObjectId = null;
    }

    private static void ClearAwsFields(TenantEncryptionSettings entity)
    {
        entity.AwsAccountId = null;
        entity.AwsRegion = null;
        entity.AwsKmsKeyArn = null;
        entity.AwsIamRoleArn = null;
        entity.AwsExternalId = null;
        entity.ProviderMetadata = null;
    }

    private async Task<TenantEncryptionTestResultDto> TestAzureKeyVaultAsync(
        TenantEncryptionSettings entity,
        CancellationToken cancellationToken)
    {
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

    private async Task<TenantEncryptionTestResultDto> TestAwsKmsAsync(
        TenantEncryptionSettings entity,
        CancellationToken cancellationToken)
    {
        var result = await _awsKmsConnector.TestConnectionAsync(entity, cancellationToken);

        entity.LastVerifiedAt = result.Timestamp;
        entity.LastVerificationMessage = result.Message;
        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static byte[] DecryptOfflineBundle(OfflineByokUploadRequest request)
    {
        byte[] salt;
        byte[] nonce;
        byte[] wrappedDek;
        byte[] checksum;

        try
        {
            salt = Convert.FromBase64String(request.Salt);
            nonce = Convert.FromBase64String(request.Nonce);
            wrappedDek = Convert.FromBase64String(request.WrappedDek);
            checksum = Convert.FromBase64String(request.Checksum);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Offline bundle values must be base64 encoded.", ex);
        }

        if (salt.Length != 16)
        {
            throw new InvalidOperationException("Offline bundle salt must be 16 bytes.");
        }

        if (nonce.Length != 12)
        {
            throw new InvalidOperationException("Offline bundle nonce must be 12 bytes.");
        }

        if (wrappedDek.Length <= 16)
        {
            throw new InvalidOperationException("Offline bundle data is invalid.");
        }

        var ciphertext = wrappedDek.AsSpan(0, wrappedDek.Length - 16);
        var tag = wrappedDek.AsSpan(wrappedDek.Length - 16);

        var plaintext = new byte[ciphertext.Length];

        using var pbkdf2 = new Rfc2898DeriveBytes(
            request.Passphrase,
            salt,
            310_000,
            HashAlgorithmName.SHA256);

        var wrappingKey = pbkdf2.GetBytes(32);

        try
        {
            using var aes = new AesGcm(wrappingKey, tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }

        var computedChecksum = SHA256.HashData(plaintext);
        if (!CryptographicOperations.FixedTimeEquals(computedChecksum, checksum))
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new InvalidOperationException("Offline bundle checksum mismatch.");
        }

        return plaintext;
    }

    private static string SanitizeUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.ToString().TrimEnd('/');
        }

        return value.Trim();
    }

    private static TenantEncryptionSettingsDto MapToDto(TenantEncryptionSettings entity)
    {
        var secureKeyRelease = new SecureKeyReleaseDto(
            entity.IsSecureKeyReleaseConfigured,
            entity.SecureKeyReleaseConfiguredAt,
            entity.AttestationProvider);

        var azure = string.Equals(entity.Provider, ProviderAzureKeyVault, StringComparison.OrdinalIgnoreCase)
            ? new AzureTenantEncryptionSettingsDto(
                entity.KeyVaultUri,
                entity.KeyVaultKeyName,
                entity.KeyVaultKeyVersion,
                entity.KeyVaultTenantId,
                entity.ManagedIdentityObjectId)
            : null;

        var aws = string.Equals(entity.Provider, ProviderAwsKms, StringComparison.OrdinalIgnoreCase)
            ? new AwsTenantEncryptionSettingsDto(
                entity.AwsAccountId,
                entity.AwsRegion,
                entity.AwsKmsKeyArn,
                entity.AwsIamRoleArn,
                entity.AwsExternalId)
            : null;

        var isConfigured = entity.Provider switch
        {
            ProviderAzureKeyVault => !string.IsNullOrWhiteSpace(entity.KeyVaultUri) &&
                                     !string.IsNullOrWhiteSpace(entity.KeyVaultKeyName),
            ProviderAwsKms => !string.IsNullOrWhiteSpace(entity.AwsKmsKeyArn) &&
                              !string.IsNullOrWhiteSpace(entity.AwsIamRoleArn),
            ProviderOffline => !string.IsNullOrWhiteSpace(entity.OfflineMasterKeyCiphertext),
            _ => false
        };

        return new TenantEncryptionSettingsDto(
            Provider: entity.Provider,
            EncryptionPhase: entity.EncryptionPhase,
            IsConfigured: isConfigured,
            UpdatedAt: entity.UpdatedAt ?? entity.CreatedAt,
            LastVerifiedAt: entity.LastVerifiedAt,
            LastVerificationMessage: entity.LastVerificationMessage,
            SecureKeyRelease: secureKeyRelease,
            Azure: azure,
            Aws: aws);
    }

    private static string NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return ProviderAzureKeyVault;
        }

        var normalized = provider.Trim();
        return normalized switch
        {
            "Azure" or "AzureKeyVault" or "azure" => ProviderAzureKeyVault,
            "Aws" or "AWS" or "AwsKms" => ProviderAwsKms,
            "Offline" or "offline-byok" => ProviderOffline,
            "EvermailManaged" or "Evermail" => ProviderEvermailManaged,
            _ => normalized
        };
    }

    private static string GenerateAwsExternalId(Guid tenantId)
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        var suffix = Convert.ToHexString(buffer).ToLowerInvariant();
        return $"evermail-{tenantId:N}-{suffix}";
    }
}


