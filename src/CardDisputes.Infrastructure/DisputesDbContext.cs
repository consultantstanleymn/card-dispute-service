using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CardDisputes.Infrastructure;

public sealed class DisputesDbContext : DbContext
{
    public DisputesDbContext(DbContextOptions<DisputesDbContext> options)
        : base(options)
    {
    }

    public DbSet<DisputeRow> Disputes => Set<DisputeRow>();

    public DbSet<DisputeEvent> DisputeEvents => Set<DisputeEvent>();

    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    public static DbContextOptions<DisputesDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<DisputesDbContext>()
            .UseNpgsql(connectionString)
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DisputesDbContext).Assembly);
    }
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DisputesDbContext>
{
    public DisputesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CDS_PG")
            ?? "Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes";

        return new DisputesDbContext(DisputesDbContext.CreateOptions(connectionString));
    }
}
