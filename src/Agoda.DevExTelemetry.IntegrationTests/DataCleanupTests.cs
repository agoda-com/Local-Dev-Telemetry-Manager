using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class DataCleanupTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;

    public DataCleanupTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory(_provider);
        _ = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task Cleanup_DeletesOldRecords()
    {
        var oldDate = DateTime.UtcNow.AddDays(-100);
        await SeedBuildMetricAt(oldDate);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-90);

            await db.BuildMetrics
                .Where(bm => bm.ReceivedAt < cutoff)
                .ExecuteDeleteAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var remaining = await db.BuildMetrics.CountAsync();
            remaining.ShouldBe(0);
        }
    }

    [Test]
    public async Task Cleanup_PreservesRecentRecords()
    {
        var recentDate = DateTime.UtcNow.AddDays(-10);
        await SeedBuildMetricAt(recentDate);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-90);

            await db.BuildMetrics
                .Where(bm => bm.ReceivedAt < cutoff)
                .ExecuteDeleteAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var remaining = await db.BuildMetrics.CountAsync();
            remaining.ShouldBe(1);
        }
    }

    [Test]
    public async Task Cleanup_DeletesTestCasesBeforeTestRuns()
    {
        var oldDate = DateTime.UtcNow.AddDays(-100);
        var runId = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

            db.TestRuns.Add(new TestRun
            {
                Id = runId,
                RunId = Guid.NewGuid().ToString(),
                ReceivedAt = oldDate,
                UserName = "testuser",
                Hostname = "dev-workstation",
                Platform = "Unix",
                Os = "Ubuntu",
                Branch = "main",
                ProjectName = "TestProject",
                Repository = "https://github.com/test/repo",
                RepositoryName = "test-repo",
                TestRunner = "NUnit",
                ExecutionEnvironment = "Local",
                TotalTests = 2,
                PassedTests = 2,
                SourceEndpoint = "/dotnet/nunit"
            });

            db.TestCases.Add(new TestCase
            {
                TestRunId = runId,
                Name = "Test_1",
                Status = "Passed",
                DurationMs = 100
            });

            db.TestCases.Add(new TestCase
            {
                TestRunId = runId,
                Name = "Test_2",
                Status = "Passed",
                DurationMs = 200
            });

            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-90);

            await db.TestCases
                .Where(tc => db.TestRuns
                    .Where(tr => tr.ReceivedAt < cutoff)
                    .Select(tr => tr.Id)
                    .Contains(tc.TestRunId))
                .ExecuteDeleteAsync();

            await db.TestRuns
                .Where(tr => tr.ReceivedAt < cutoff)
                .ExecuteDeleteAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            (await db.TestCases.CountAsync()).ShouldBe(0);
            (await db.TestRuns.CountAsync()).ShouldBe(0);
        }
    }

    private async Task SeedBuildMetricAt(DateTime date)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        db.BuildMetrics.Add(new BuildMetric
        {
            Id = Guid.NewGuid().ToString(),
            ReceivedAt = date,
            UserName = "testuser",
            CpuCount = 8,
            Hostname = "dev-workstation",
            Platform = "Unix",
            Os = "Ubuntu",
            Branch = "main",
            ProjectName = "TestProject",
            Repository = "https://github.com/test/repo",
            RepositoryName = "test-repo",
            TimeTakenMs = 1000,
            MetricType = ".Net",
            BuildCategory = "API",
            ExecutionEnvironment = "Local",
            SourceEndpoint = "/dotnet"
        });

        await db.SaveChangesAsync();
    }
}
