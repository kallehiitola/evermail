using Evermail.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Evermail.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(EmailDbContext context, RoleManager<IdentityRole<Guid>> roleManager)
    {
        // Ensure database is created
        await context.Database.MigrateAsync();

        // Seed Roles
        var roles = new[] { "User", "Admin", "SuperAdmin" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                };
                await roleManager.CreateAsync(role);
            }
        }

        // Seed Subscription Plans
        if (!await context.SubscriptionPlans.AnyAsync())
        {
            var plans = new List<SubscriptionPlan>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Free",
                    DisplayName = "Free Tier",
                    Description = "Try Evermail with 1 mailbox and 30-day retention",
                    PriceMonthly = 0,
                    PriceYearly = 0,
                    Currency = "EUR",
                    MaxStorageGB = 1,
                    MaxUsers = 1,
                    MaxMailboxes = 1,
                    Features = "[\"1 GB storage\",\"1 mailbox\",\"30-day retention\",\"Basic search\"]",
                    IsActive = true,
                    DisplayOrder = 1
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Pro",
                    DisplayName = "Professional",
                    Description = "For individuals with multiple work histories",
                    PriceMonthly = 9,
                    PriceYearly = 90,
                    Currency = "EUR",
                    MaxStorageGB = 5,
                    MaxUsers = 1,
                    MaxMailboxes = int.MaxValue, // Unlimited
                    Features = "[\"5 GB storage\",\"Unlimited mailboxes\",\"1-year retention\",\"Full-text search\",\"AI summaries (50/month)\",\"Gmail/Outlook import\"]",
                    IsActive = true,
                    DisplayOrder = 2
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team",
                    DisplayName = "Team",
                    Description = "For small businesses and HR departments",
                    PriceMonthly = 29,
                    PriceYearly = 290,
                    Currency = "EUR",
                    MaxStorageGB = 50,
                    MaxUsers = 5,
                    MaxMailboxes = int.MaxValue, // Unlimited
                    Features = "[\"50 GB storage\",\"5 users\",\"Unlimited mailboxes\",\"2-year retention\",\"Shared workspaces\",\"AI summaries (500/month)\",\"API access\"]",
                    IsActive = true,
                    DisplayOrder = 3
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Enterprise",
                    DisplayName = "Enterprise",
                    Description = "For regulated industries and large teams",
                    PriceMonthly = 99,
                    PriceYearly = 990,
                    Currency = "EUR",
                    MaxStorageGB = 500,
                    MaxUsers = 50,
                    MaxMailboxes = int.MaxValue, // Unlimited
                    Features = "[\"500 GB storage\",\"50 users\",\"Unlimited mailboxes\",\"Configurable retention (1-10 years)\",\"GDPR Archive (immutable)\",\"Unlimited AI\",\"Full API access\",\"Priority support\",\"99.9% SLA\"]",
                    IsActive = true,
                    DisplayOrder = 4
                }
            };

            await context.SubscriptionPlans.AddRangeAsync(plans);
            await context.SaveChangesAsync();
        }
    }
}

