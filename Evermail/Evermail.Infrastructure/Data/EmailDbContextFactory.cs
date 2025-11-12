using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Evermail.Infrastructure.Data;

public class EmailDbContextFactory : IDesignTimeDbContextFactory<EmailDbContext>
{
    public EmailDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EmailDbContext>();
        
        // Use a default connection string for migrations
        // This will be replaced with actual connection string at runtime
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true",
            b => b.MigrationsAssembly("Evermail.Infrastructure"));

        return new EmailDbContext(optionsBuilder.Options, tenantContext: null);
    }
}

