using Microsoft.EntityFrameworkCore;
using Monitoring.Blazor.Models;

namespace Monitoring.Blazor.Services;

public sealed class MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : DbContext(options)
{
    public DbSet<HostSnapshotEntity> HostSnapshots => Set<HostSnapshotEntity>();
    public DbSet<AlertEventEntity> AlertEvents => Set<AlertEventEntity>();
    public DbSet<LogEntryEntity> LogEntries => Set<LogEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HostSnapshotEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Hostname, x.CreatedUtc });
            entity.Property(x => x.Hostname).HasMaxLength(200);
            entity.Property(x => x.Ip).HasMaxLength(200);
            entity.Property(x => x.Os).HasMaxLength(200);
            entity.Property(x => x.TargetUrl).HasMaxLength(1000);
        });

        modelBuilder.Entity<AlertEventEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Hostname, x.TimestampUtc });
            entity.Property(x => x.Hostname).HasMaxLength(200);
            entity.Property(x => x.Ip).HasMaxLength(200);
            entity.Property(x => x.Os).HasMaxLength(200);
            entity.Property(x => x.Metric).HasMaxLength(200);
        });

        modelBuilder.Entity<LogEntryEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedUtc);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Uri);
            entity.HasIndex(x => x.Ip);
            entity.Property(x => x.SourceFile).HasMaxLength(400);
        });
    }
}
