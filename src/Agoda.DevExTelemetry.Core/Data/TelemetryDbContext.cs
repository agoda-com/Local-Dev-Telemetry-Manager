using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Data;

public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options) { }

    public DbSet<BuildMetric> BuildMetrics => Set<BuildMetric>();
    public DbSet<CommandEvent> CommandEvents => Set<CommandEvent>();
    public DbSet<CommandEventNpmTimer> CommandEventNpmTimers => Set<CommandEventNpmTimer>();
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
            entity.HasIndex(e => e.SessionId);
        });

        modelBuilder.Entity<CommandEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Phase);
            entity.HasIndex(e => e.ProjectName);
            entity.HasIndex(e => e.MeasurementSource);
            entity.HasIndex(e => e.SourceEndpoint);
        });

        modelBuilder.Entity<CommandEventNpmTimer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.CommandEventId);
            entity.HasIndex(e => e.TimerName);

            entity.HasOne(e => e.CommandEvent)
                .WithMany(e => e.NpmTimers)
                .HasForeignKey(e => e.CommandEventId)
                .OnDelete(DeleteBehavior.Cascade);
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
