using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services;

public interface IAwsKmsConnector
{
    Task<TenantEncryptionTestResultDto> TestConnectionAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default);

    Task<AwsGenerateDataKeyResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default);

    Task<AwsDecryptResult> DecryptAsync(
        TenantEncryptionSettings settings,
        byte[] ciphertext,
        CancellationToken cancellationToken = default);
}

public record AwsGenerateDataKeyResult(
    byte[] Plaintext,
    byte[] Ciphertext,
    string KeyId,
    string RequestId);

public record AwsDecryptResult(
    byte[] Plaintext,
    string RequestId);

