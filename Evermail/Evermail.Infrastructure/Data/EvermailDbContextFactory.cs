using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Evermail.Infrastructure.Data;

public class EvermailDbContextFactory : IDesignTimeDbContextFactory<EvermailDbContext>
{
    public EvermailDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EvermailDbContext>();
        
        // Use a default connection string for migrations
        // This will be replaced with actual connection string at runtime
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=Evermail;Trusted_Connection=True;MultipleActiveResultSets=true",
            b => b.MigrationsAssembly("Evermail.Infrastructure"));

        return new EvermailDbContext(optionsBuilder.Options, tenantContext: null);
    }
}

