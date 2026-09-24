using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MicroservicesLab.AuditWorker.Infrastructure;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=audit_db;Username=postgres;Password=postgres")
            .Options;

        return new AuditDbContext(options);
    }
}
