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
    public DbSet<FullTextSearchResult> FullTextSearchResults => Set<FullTextSearchResult>();

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
        }

        // Tenant
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.HasIndex(t => t.StripeCustomerId);
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

            entity.HasOne(mu => mu.Mailbox)
                .WithMany(m => m.Uploads)
                .HasForeignKey(mu => mu.MailboxId)
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
            entity.HasIndex(rt => new { rt.UserId, rt.IsActive });

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

