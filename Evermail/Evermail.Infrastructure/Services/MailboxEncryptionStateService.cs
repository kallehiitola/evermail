using System.Security.Cryptography;
using Evermail.Domain.Entities;
using Evermail.Infrastructure.Data;
using Evermail.Infrastructure.Services.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Evermail.Infrastructure.Services;

public class MailboxEncryptionStateService : IMailboxEncryptionStateService
{
    private readonly EvermailDbContext _context;
    private readonly IKeyWrappingService _keyWrappingService;
    private readonly ILogger<MailboxEncryptionStateService> _logger;

    public MailboxEncryptionStateService(
        EvermailDbContext context,
        IKeyWrappingService keyWrappingService,
        ILogger<MailboxEncryptionStateService> logger)
    {
        _context = context;
        _keyWrappingService = keyWrappingService;
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

        var settings = await _context.TenantEncryptionSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantEncryptionSettings
            {
                TenantId = tenantId,
                Provider = "EvermailManaged",
                EncryptionPhase = "EvermailManaged",
                CreatedAt = DateTime.UtcNow
            };
            _context.TenantEncryptionSettings.Add(settings);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _context.Entry(settings).State = EntityState.Detached;
        }

        var wrapResult = await _keyWrappingService.GenerateDataKeyAsync(
            settings,
            cancellationToken);

        var state = new MailboxEncryptionState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxId = mailboxId,
            MailboxUploadId = mailboxUploadId,
            Algorithm = "AES-256-GCM",
            WrappedDek = wrapResult.WrappedDekBase64,
            DekVersion = "v1",
            TenantKeyVersion = wrapResult.ProviderKeyVersion,
            KeyVaultKeyVersion = wrapResult.ProviderKeyVersion,
            Provider = settings.Provider,
            ProviderKeyVersion = wrapResult.ProviderKeyVersion,
            WrapRequestId = wrapResult.ProviderRequestId,
            ProviderMetadata = wrapResult.ProviderMetadataJson,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.MailboxEncryptionStates.Add(state);
        await _context.SaveChangesAsync(cancellationToken);

        CryptographicOperations.ZeroMemory(wrapResult.PlaintextDek);

        _logger.LogInformation("Created encryption state {StateId} for mailbox {MailboxId} with provider {Provider}", state.Id, mailboxId, settings.Provider);

        return state;
    }

    public async Task RecordKeyReleaseAsync(
        Guid encryptionStateId,
        string componentName,
        string? providerRequestId = null,
        string? providerMetadata = null,
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
        if (!string.IsNullOrWhiteSpace(providerRequestId))
        {
            state.LastUnwrapRequestId = providerRequestId;
        }

        if (!string.IsNullOrWhiteSpace(providerMetadata))
        {
            state.ProviderMetadata = providerMetadata;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}


