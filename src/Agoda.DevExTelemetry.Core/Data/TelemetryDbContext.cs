using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Data;

public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options) { }

    public DbSet<BuildMetric> BuildMetrics => Set<BuildMetric>();
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<RawPayload> RawPayloads => Set<RawPayload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BuildMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.MetricType);
            entity.HasIndex(e => e.BuildCategory);
            entity.HasIndex(e => e.ProjectName);
            entity.HasIndex(e => e.ExecutionEnvironment);
        });

        modelBuilder.Entity<TestRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.TestRunner);
            entity.HasIndex(e => e.ProjectName);
            entity.HasIndex(e => e.ExecutionEnvironment);

            entity.HasMany(e => e.TestCases)
                .WithOne(e => e.TestRun)
                .HasForeignKey(e => e.TestRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.TestRunId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ClassName);
        });

        modelBuilder.Entity<RawPayload>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });
    }
}
