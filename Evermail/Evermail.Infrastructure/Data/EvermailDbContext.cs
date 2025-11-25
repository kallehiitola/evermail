using Evermail.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Data;

public class EvermailDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly TenantContext? _tenantContext;

    public EvermailDbContext(DbContextOptions<EvermailDbContext> options, TenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    // ApplicationUser is accessed via Users property from IdentityDbContext
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<MailboxUpload> MailboxUploads => Set<MailboxUpload>();
    public DbSet<MailboxDeletionQueue> MailboxDeletionQueue => Set<MailboxDeletionQueue>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<EmailThread> EmailThreads => Set<EmailThread>();
    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TenantEncryptionSettings> TenantEncryptionSettings => Set<TenantEncryptionSettings>();
    public DbSet<MailboxEncryptionState> MailboxEncryptionStates => Set<MailboxEncryptionState>();
    public DbSet<FullTextSearchResult> FullTextSearchResults => Set<FullTextSearchResult>();
    public DbSet<UserDisplaySetting> UserDisplaySettings => Set<UserDisplaySetting>();
    public DbSet<SavedSearchFilter> SavedSearchFilters => Set<SavedSearchFilter>();
    public DbSet<PinnedEmailThread> PinnedEmailThreads => Set<PinnedEmailThread>();
    public DbSet<UserDataExport> UserDataExports => Set<UserDataExport>();
    public DbSet<UserDeletionJob> UserDeletionJobs => Set<UserDeletionJob>();
    public DbSet<ZeroAccessMailboxToken> ZeroAccessMailboxTokens => Set<ZeroAccessMailboxToken>();
    public DbSet<TenantEncryptionBundle> TenantEncryptionBundles => Set<TenantEncryptionBundle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global query filters for multi-tenancy (if TenantContext is available)
        if (_tenantContext != null)
        {
            modelBuilder.Entity<Mailbox>()
                .HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<EmailMessage>()
                .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<Attachment>()
                .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<MailboxUpload>()
                .HasQueryFilter(mu => mu.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<MailboxDeletionQueue>()
                .HasQueryFilter(mdq => mdq.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<AuditLog>()
                .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<RefreshToken>()
                .HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<EmailThread>()
                .HasQueryFilter(t => t.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<EmailRecipient>()
                .HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<TenantEncryptionSettings>()
                .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<MailboxEncryptionState>()
                .HasQueryFilter(es => es.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<UserDisplaySetting>()
                .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId && s.UserId == _tenantContext.UserId);

            modelBuilder.Entity<SavedSearchFilter>()
                .HasQueryFilter(f => f.TenantId == _tenantContext.TenantId && f.UserId == _tenantContext.UserId);

            modelBuilder.Entity<PinnedEmailThread>()
                .HasQueryFilter(p => p.TenantId == _tenantContext.TenantId && p.UserId == _tenantContext.UserId);

            modelBuilder.Entity<UserDataExport>()
                .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId && e.UserId == _tenantContext.UserId);

            modelBuilder.Entity<UserDeletionJob>()
                .HasQueryFilter(j => j.TenantId == _tenantContext.TenantId && j.UserId == _tenantContext.UserId);

            modelBuilder.Entity<ZeroAccessMailboxToken>()
                .HasQueryFilter(t => t.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<TenantEncryptionBundle>()
                .HasQueryFilter(b => b.TenantId == _tenantContext.TenantId);
        }

        // Tenant
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.HasIndex(t => t.StripeCustomerId);
            entity.Property(t => t.SecurityPreference)
                .HasMaxLength(32)
                .HasDefaultValue("QuickStart");
        });

        modelBuilder.Entity<TenantEncryptionSettings>(entity =>
        {
            entity.HasKey(s => s.TenantId);
            entity.Property(s => s.Provider)
                .HasMaxLength(50)
                .HasDefaultValue("AzureKeyVault");
            entity.Property(s => s.KeyVaultUri).HasMaxLength(500);
            entity.Property(s => s.KeyVaultKeyName).HasMaxLength(200);
            entity.Property(s => s.KeyVaultKeyVersion).HasMaxLength(200);
            entity.Property(s => s.KeyVaultTenantId).HasMaxLength(64);
            entity.Property(s => s.ManagedIdentityObjectId).HasMaxLength(100);
            entity.Property(s => s.AwsAccountId).HasMaxLength(32);
            entity.Property(s => s.AwsRegion).HasMaxLength(32);
            entity.Property(s => s.AwsKmsKeyArn).HasMaxLength(2048);
            entity.Property(s => s.AwsIamRoleArn).HasMaxLength(2048);
            entity.Property(s => s.AwsExternalId).HasMaxLength(128);
            entity.Property(s => s.EncryptionPhase).HasMaxLength(50);
            entity.Property(s => s.LastVerificationMessage).HasMaxLength(500);
            entity.Property(s => s.SecureKeyReleasePolicyHash).HasMaxLength(128);
            entity.Property(s => s.AttestationProvider).HasMaxLength(128);

            entity.HasOne(s => s.Tenant)
                .WithOne(t => t.EncryptionSettings)
                .HasForeignKey<TenantEncryptionSettings>(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ApplicationUser (extends IdentityUser)
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.TenantId);

            entity.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.NoAction); // NO ACTION to avoid circular cascade
        });

        // Mailbox
        modelBuilder.Entity<Mailbox>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.TenantId, m.UserId });
            entity.HasIndex(m => m.Status);
            entity.HasIndex(m => new { m.IsPendingDeletion, m.PurgeAfter });
            entity.Property(m => m.SourceFormat)
                .HasMaxLength(64)
                .HasDefaultValue("mbox");
            entity.Property(m => m.NormalizedSizeBytes)
                .HasDefaultValue(0L);
            entity.Property(m => m.IsClientEncrypted)
                .HasDefaultValue(false);
            entity.Property(m => m.EncryptionScheme)
                .HasMaxLength(100);
            entity.Property(m => m.EncryptionKeyFingerprint)
                .HasMaxLength(128);
            entity.Property(m => m.ZeroAccessTokenSalt)
                .HasMaxLength(64);

            entity.HasOne(m => m.Tenant)
                .WithMany(t => t.Mailboxes)
                .HasForeignKey(m => m.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Mailboxes)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.LatestUpload)
                .WithMany()
                .HasForeignKey(m => m.LatestUploadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // MailboxUpload
        modelBuilder.Entity<MailboxUpload>(entity =>
        {
            entity.HasKey(mu => mu.Id);
            entity.HasIndex(mu => mu.MailboxId);
            entity.HasIndex(mu => mu.Status);
            entity.HasIndex(mu => mu.TenantId);
            entity.Property(mu => mu.SourceFormat)
                .HasMaxLength(64)
                .HasDefaultValue("mbox");
            entity.Property(mu => mu.NormalizedSizeBytes)
                .HasDefaultValue(0L);
            entity.Property(mu => mu.IsClientEncrypted)
                .HasDefaultValue(false);
            entity.Property(mu => mu.EncryptionScheme)
                .HasMaxLength(100);
            entity.Property(mu => mu.EncryptionKeyFingerprint)
                .HasMaxLength(128);

            entity.HasOne(mu => mu.Mailbox)
                .WithMany(m => m.Uploads)
                .HasForeignKey(mu => mu.MailboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ZeroAccessMailboxToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.TenantId, t.MailboxId });
            entity.HasIndex(t => new { t.TenantId, t.TokenType, t.TokenValue });
            entity.Property(t => t.TokenType).HasMaxLength(50);
            entity.Property(t => t.TokenValue).HasMaxLength(512);

            entity.HasOne(t => t.Mailbox)
                .WithMany()
                .HasForeignKey(t => t.MailboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantEncryptionBundle>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.TenantId, b.CreatedAt });
            entity.Property(b => b.Label).HasMaxLength(150);
            entity.Property(b => b.Version).HasMaxLength(50);
            entity.Property(b => b.Salt).HasMaxLength(64);
            entity.Property(b => b.Nonce).HasMaxLength(48);
            entity.Property(b => b.Checksum).HasMaxLength(88);

            entity.HasOne(b => b.Tenant)
                .WithMany()
                .HasForeignKey(b => b.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailboxEncryptionState>(entity =>
        {
            entity.HasKey(es => es.Id);
            entity.HasIndex(es => es.MailboxUploadId).IsUnique();
            entity.HasIndex(es => es.MailboxId);
            entity.HasIndex(es => new { es.TenantId, es.CreatedAt });

            entity.Property(es => es.Algorithm).HasMaxLength(50);
            entity.Property(es => es.DekVersion).HasMaxLength(100);
            entity.Property(es => es.TenantKeyVersion).HasMaxLength(100);
            entity.Property(es => es.LastKeyReleaseComponent).HasMaxLength(200);
            entity.Property(es => es.LastKeyReleaseLedgerEntryId).HasMaxLength(200);
            entity.Property(es => es.AttestationPolicyId).HasMaxLength(200);
            entity.Property(es => es.KeyVaultKeyVersion).HasMaxLength(200);
            entity.Property(es => es.Provider).HasMaxLength(50);
            entity.Property(es => es.ProviderKeyVersion).HasMaxLength(200);
            entity.Property(es => es.WrapRequestId).HasMaxLength(200);
            entity.Property(es => es.LastUnwrapRequestId).HasMaxLength(200);

            entity.HasOne(es => es.Mailbox)
                .WithMany()
                .HasForeignKey(es => es.MailboxId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(es => es.MailboxUpload)
                .WithOne(mu => mu.EncryptionState)
                .HasForeignKey<MailboxEncryptionState>(es => es.MailboxUploadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MailboxDeletionQueue
        modelBuilder.Entity<MailboxDeletionQueue>(entity =>
        {
            entity.HasKey(mdq => mdq.Id);
            entity.HasIndex(mdq => new { mdq.Status, mdq.ExecuteAfter });

            entity.HasOne(mdq => mdq.Mailbox)
                .WithMany()
                .HasForeignKey(mdq => mdq.MailboxId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmailMessage
        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserId });
            entity.HasIndex(e => e.MailboxId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.FromAddress);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.TenantId, e.ConversationId });
            entity.HasIndex(e => new { e.MailboxId, e.ContentHash }).IsUnique().HasFilter("[ContentHash] IS NOT NULL");

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Mailbox)
                .WithMany(m => m.EmailMessages)
                .HasForeignKey(e => e.MailboxId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MailboxUpload)
                .WithMany()
                .HasForeignKey(e => e.MailboxUploadId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Thread)
                .WithMany(t => t.Emails)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Recipients)
                .WithOne(r => r.EmailMessage)
                .HasForeignKey(r => r.EmailMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmailThread
        modelBuilder.Entity<EmailThread>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.TenantId, t.ConversationKey }).IsUnique();
            entity.HasIndex(t => new { t.TenantId, t.LastMessageDate });

            entity.Property(t => t.ParticipantsSummary)
                .HasDefaultValue("[]");

            entity.HasOne(t => t.Tenant)
                .WithMany(te => te.EmailThreads)
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmailRecipient
        modelBuilder.Entity<EmailRecipient>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.TenantId, r.Address });
            entity.HasIndex(r => new { r.TenantId, r.RecipientType, r.Address });

            entity.HasOne(r => r.EmailMessage)
                .WithMany(e => e.Recipients)
                .HasForeignKey(r => r.EmailMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Attachment
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.EmailMessageId);
            entity.HasIndex(a => a.TenantId);

            entity.HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.EmailMessage)
                .WithMany(e => e.Attachments)
                .HasForeignKey(a => a.EmailMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserDisplaySetting>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.TenantId, s.UserId }).IsUnique();
            entity.Property(s => s.DateFormat).HasMaxLength(32);
            entity.Property(s => s.ResultDensity).HasMaxLength(16);

            entity.HasOne(s => s.Tenant)
                .WithMany()
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavedSearchFilter>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.TenantId, f.UserId, f.OrderIndex });
            entity.Property(f => f.Name).HasMaxLength(128);

            entity.HasOne(f => f.Tenant)
                .WithMany()
                .HasForeignKey(f => f.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PinnedEmailThread>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.TenantId, p.UserId });
            entity.HasIndex(p => new { p.TenantId, p.UserId, p.ConversationId })
                .IsUnique()
                .HasFilter("[ConversationId] IS NOT NULL");

            entity.HasIndex(p => new { p.TenantId, p.UserId, p.EmailMessageId })
                .IsUnique()
                .HasFilter("[EmailMessageId] IS NOT NULL");

            entity.HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Conversation)
                .WithMany()
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.EmailMessage)
                .WithMany()
                .HasForeignKey(p => p.EmailMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserDataExport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserId });
            entity.HasIndex(e => new { e.TenantId, e.RequestedByUserId });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.Property(e => e.Status).HasMaxLength(32);
            entity.Property(e => e.BlobPath).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.Sha256).HasMaxLength(128);
        });

        modelBuilder.Entity<UserDeletionJob>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.HasIndex(j => new { j.TenantId, j.UserId });
            entity.HasIndex(j => new { j.TenantId, j.RequestedByUserId });
            entity.Property(j => j.Status).HasMaxLength(32);
            entity.Property(j => j.Notes).HasMaxLength(1000);
        });

        // SubscriptionPlan
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(sp => sp.Id);
            entity.HasIndex(sp => sp.Name).IsUnique();

            // Specify decimal precision
            entity.Property(sp => sp.PriceMonthly).HasPrecision(18, 2);
            entity.Property(sp => sp.PriceYearly).HasPrecision(18, 2);
        });

        // Subscription
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.TenantId);
            entity.HasIndex(s => s.StripeSubscriptionId).IsUnique();
            entity.HasIndex(s => s.Status);

            entity.HasOne(s => s.Tenant)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.SubscriptionPlan)
                .WithMany(sp => sp.Subscriptions)
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.TokenHash);
            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.TenantId);
            entity.HasIndex(rt => rt.ExpiresAt);

            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Computed column for IsActive can't be persisted, it's calculated in code
            entity.Ignore(rt => rt.IsActive);
            entity.Ignore(rt => rt.IsExpired);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(al => al.Id);
            entity.HasIndex(al => al.TenantId);
            entity.HasIndex(al => al.UserId);
            entity.HasIndex(al => al.Timestamp);
            entity.HasIndex(al => al.Action);

            entity.HasOne(al => al.Tenant)
                .WithMany()
                .HasForeignKey(al => al.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Full-text search helper (keyless)
        modelBuilder.Entity<FullTextSearchResult>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null); // Not mapped to a table/view
            entity.Property(e => e.EmailId).HasColumnName("EmailId");
            entity.Property(e => e.Rank).HasColumnName("Rank");
        });
    }
}

