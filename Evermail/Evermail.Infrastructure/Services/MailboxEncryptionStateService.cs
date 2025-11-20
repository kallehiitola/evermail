using System.Security.Cryptography;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services;

public class MailboxEncryptionStateService : IMailboxEncryptionStateService
{
    private readonly EvermailDbContext _context;
    private readonly ILogger<MailboxEncryptionStateService> _logger;

    public MailboxEncryptionStateService(EvermailDbContext context, ILogger<MailboxEncryptionStateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MailboxEncryptionState> CreateAsync(
        Guid tenantId,
        Guid mailboxId,
        Guid mailboxUploadId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.MailboxEncryptionStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MailboxUploadId == mailboxUploadId, cancellationToken);

        if (existing != null)
        {
            _logger.LogDebug("Encryption state already exists for upload {UploadId}", mailboxUploadId);
            return existing;
        }

        var dekBytes = RandomNumberGenerator.GetBytes(32);
        var wrappedDek = Convert.ToBase64String(dekBytes);

        var state = new MailboxEncryptionState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            MailboxUploadId = mailboxUploadId,
            Algorithm = "AES-256-GCM",
            WrappedDek = wrappedDek,
            DekVersion = "v1",
            TenantKeyVersion = null,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.MailboxEncryptionStates.Add(state);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created encryption state {StateId} for mailbox {MailboxId}", state.Id, mailboxId);

        return state;
    }

    public async Task RecordKeyReleaseAsync(
        Guid encryptionStateId,
        string componentName,
        CancellationToken cancellationToken = default)
    {
        var state = await _context.MailboxEncryptionStates
            .FirstOrDefaultAsync(s => s.Id == encryptionStateId, cancellationToken);

        if (state == null)
        {
            _logger.LogWarning("Encryption state {StateId} not found when recording key release", encryptionStateId);
            return;
        }

        state.LastKeyReleaseAt = DateTime.UtcNow;
        state.LastKeyReleaseComponent = componentName;
        await _context.SaveChangesAsync(cancellationToken);
    }
}


