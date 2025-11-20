using Evermail.Domain.Entities;

namespace Evermail.Infrastructure.Services;

public interface IMailboxEncryptionStateService
{
    Task<MailboxEncryptionState> CreateAsync(Guid tenantId, Guid mailboxId, Guid mailboxUploadId, Guid userId, CancellationToken cancellationToken = default);
    Task RecordKeyReleaseAsync(Guid encryptionStateId, string componentName, string? providerRequestId = null, string? providerMetadata = null, CancellationToken cancellationToken = default);
}


