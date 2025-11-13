using Evermail.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Data;

public class EmailDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly TenantContext? _tenantContext;

    public EmailDbContext(DbContextOptions<EmailDbContext> options, TenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    // ApplicationUser is accessed via Users property from IdentityDbContext
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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

            modelBuilder.Entity<AuditLog>()
                .HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
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

            entity.HasOne(m => m.Tenant)
                .WithMany(t => t.Mailboxes)
                .HasForeignKey(m => m.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Mailboxes)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // EmailMessage
        modelBuilder.Entity<EmailMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserId });
            entity.HasIndex(e => e.MailboxId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.FromAddress);

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
    }
}

