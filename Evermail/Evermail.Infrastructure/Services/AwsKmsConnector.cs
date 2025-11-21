using System.IO;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Evermail.Common.DTOs.Tenant;
using Evermail.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services;

public class AwsKmsConnector : IAwsKmsConnector
{
    private readonly IAmazonSecurityTokenService _stsClient;
    private readonly ILogger<AwsKmsConnector> _logger;

    public AwsKmsConnector(
        IAmazonSecurityTokenService stsClient,
        ILogger<AwsKmsConnector> logger)
    {
        _stsClient = stsClient;
        _logger = logger;
    }

    public async Task<TenantEncryptionTestResultDto> TestConnectionAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!Validate(settings, out var validationMessage))
        {
            return Failure(validationMessage);
        }

        try
        {
            var result = await GenerateDataKeyInternalAsync(settings, includePlaintext: false, cancellationToken);
            var message = $"AWS KMS key reachable. RequestId: {result.RequestId}.";
            return Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AWS KMS validation failed for tenant {TenantId}", settings.TenantId);
            return Failure($"AWS KMS validation failed: {ex.Message}");
        }
    }

    public Task<AwsGenerateDataKeyResult> GenerateDataKeyAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken = default)
        => GenerateDataKeyInternalAsync(settings, includePlaintext: true, cancellationToken);

    public async Task<AwsDecryptResult> DecryptAsync(
        TenantEncryptionSettings settings,
        byte[] ciphertext,
        CancellationToken cancellationToken = default)
    {
        if (!Validate(settings, out var validationMessage))
        {
            throw new InvalidOperationException(validationMessage);
        }

        using var kmsClient = await CreateKmsClientAsync(settings, cancellationToken);
        using var cipherStream = new MemoryStream(ciphertext);
        var response = await kmsClient.DecryptAsync(new DecryptRequest
        {
            CiphertextBlob = cipherStream,
            KeyId = settings.AwsKmsKeyArn
        }, cancellationToken);

        return new AwsDecryptResult(
            Plaintext: response.Plaintext.ToArray(),
            RequestId: response.ResponseMetadata.RequestId);
    }

    private static string BuildSessionName()
    {
        var name = $"evermail-validation-{Guid.NewGuid():N}";
        return name.Length <= 64 ? name : name[..64];
    }

    private bool Validate(TenantEncryptionSettings settings, out string message)
    {
        if (string.IsNullOrWhiteSpace(settings.AwsIamRoleArn) ||
            string.IsNullOrWhiteSpace(settings.AwsKmsKeyArn) ||
            string.IsNullOrWhiteSpace(settings.AwsRegion) ||
            string.IsNullOrWhiteSpace(settings.AwsExternalId))
        {
            message = "AWS settings are incomplete. Please provide the role ARN, key ARN, region, and external ID.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private async Task<AwsGenerateDataKeyResult> GenerateDataKeyInternalAsync(
        TenantEncryptionSettings settings,
        bool includePlaintext,
        CancellationToken cancellationToken)
    {
        if (!Validate(settings, out var validationMessage))
        {
            throw new InvalidOperationException(validationMessage);
        }

        using var kmsClient = await CreateKmsClientAsync(settings, cancellationToken);

        if (includePlaintext)
        {
            var response = await kmsClient.GenerateDataKeyAsync(new GenerateDataKeyRequest
            {
                KeyId = settings.AwsKmsKeyArn,
                KeySpec = DataKeySpec.AES_256
            }, cancellationToken);

            return new AwsGenerateDataKeyResult(
                Plaintext: response.Plaintext.ToArray(),
                Ciphertext: response.CiphertextBlob.ToArray(),
                KeyId: response.KeyId,
                RequestId: response.ResponseMetadata.RequestId);
        }
        else
        {
            var response = await kmsClient.GenerateDataKeyWithoutPlaintextAsync(new GenerateDataKeyWithoutPlaintextRequest
            {
                KeyId = settings.AwsKmsKeyArn,
                KeySpec = DataKeySpec.AES_256
            }, cancellationToken);

            return new AwsGenerateDataKeyResult(
                Plaintext: Array.Empty<byte>(),
                Ciphertext: response.CiphertextBlob.ToArray(),
                KeyId: response.KeyId,
                RequestId: response.ResponseMetadata.RequestId);
        }
    }

    private async Task<AmazonKeyManagementServiceClient> CreateKmsClientAsync(
        TenantEncryptionSettings settings,
        CancellationToken cancellationToken)
    {
        var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
        {
            RoleArn = settings.AwsIamRoleArn,
            ExternalId = settings.AwsExternalId,
            RoleSessionName = BuildSessionName(),
            DurationSeconds = 900
        }, cancellationToken);

        var credentials = assumeRoleResponse.Credentials;
        return new AmazonKeyManagementServiceClient(
            new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretAccessKey, credentials.SessionToken),
            Amazon.RegionEndpoint.GetBySystemName(settings.AwsRegion));
    }

    private static TenantEncryptionTestResultDto Success(string message) =>
        new(true, message, DateTime.UtcNow);

    private static TenantEncryptionTestResultDto Failure(string message) =>
        new(false, message, DateTime.UtcNow);
}

