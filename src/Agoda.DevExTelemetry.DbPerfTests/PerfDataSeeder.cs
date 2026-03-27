using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.DbPerfTests;

public static class PerfDataSeeder
{
    public static async Task SeedAsync(TelemetryDbContext db, PerfSeedOptions options, CancellationToken ct = default)
    {
        if (await db.BuildMetrics.AnyAsync(ct) || await db.TestRuns.AnyAsync(ct))
            return;

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var random = new Random(options.RandomSeed);
        var projects = Enumerable.Range(1, options.ProjectCardinality).Select(i => $"project-{i:D3}").ToArray();
        var repos = Enumerable.Range(1, options.RepositoryCardinality).Select(i => $"repo-{i:D3}").ToArray();
        var branches = Enumerable.Range(1, options.BranchCardinality).Select(i => $"feature/{i:D4}").ToArray();
        var platforms = Enumerable.Range(1, options.PlatformCardinality).Select(i => $"platform-{i:D2}").ToArray();
        var metricTypes = new[] { ".Net", ".AspNetStartup", ".AspNetResponse", "vite", "webpack" };
        var buildCategories = new[] { "API", "Clientside" };
        var testRunners = new[] { "NUnit", "xUnit", "Jest", "Vitest" };
        var statuses = new[] { "Passed", "Failed", "Skipped" };
        var environments = new[] { "Local", "CI" };

        var start = DateTime.UtcNow.Date.AddDays(-30);

        const int batchSize = 1000;

        for (var i = 0; i < options.BuildMetrics; i++)
        {
            var ts = start.AddMinutes(random.Next(0, 30 * 24 * 60));
            var category = buildCategories[random.Next(buildCategories.Length)];
            var metricType = category == "API"
                ? metricTypes[random.Next(0, 3)]
                : metricTypes[random.Next(3, 5)];

            db.BuildMetrics.Add(new BuildMetric
            {
                Id = $"bm-{i:D8}",
                ReceivedAt = ts,
                UserName = $"user-{random.Next(1, 200):D3}",
                CpuCount = random.Next(4, 17),
                Hostname = $"host-{random.Next(1, 80):D3}",
                Platform = platforms[random.Next(platforms.Length)],
                Os = random.Next(2) == 0 ? "Windows" : "Linux",
                Branch = branches[random.Next(branches.Length)],
                ProjectName = projects[random.Next(projects.Length)],
                Repository = repos[random.Next(repos.Length)],
                RepositoryName = repos[random.Next(repos.Length)],
                TimeTakenMs = random.Next(100, 20000),
                MetricType = metricType,
                BuildCategory = category,
                ReloadType = random.Next(2) == 0 ? "hot" : "full",
                ToolVersion = "1.0.0",
                CommitSha = Guid.NewGuid().ToString("N")[..8],
                IsDebuggerAttached = random.Next(10) == 0,
                ExecutionEnvironment = environments[random.Next(environments.Length)],
                SourceEndpoint = category == "API" ? "/dotnet" : "/vite",
                ExtraData = null
            });

            if (i % batchSize == 0 && i > 0)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        for (var i = 0; i < options.TestRuns; i++)
        {
            var ts = start.AddMinutes(random.Next(0, 30 * 24 * 60));
            var runId = $"tr-{i:D8}";
            var totalTests = options.TestCasesPerRun;
            var failed = random.Next(0, 3);
            var skipped = random.Next(0, 2);
            var passed = Math.Max(0, totalTests - failed - skipped);

            var run = new TestRun
            {
                Id = runId,
                RunId = Guid.NewGuid().ToString("N"),
                ReceivedAt = ts,
                UserName = $"user-{random.Next(1, 200):D3}",
                CpuCount = random.Next(4, 17),
                Hostname = $"host-{random.Next(1, 80):D3}",
                Platform = platforms[random.Next(platforms.Length)],
                Os = random.Next(2) == 0 ? "Windows" : "Linux",
                Branch = branches[random.Next(branches.Length)],
                ProjectName = projects[random.Next(projects.Length)],
                Repository = repos[random.Next(repos.Length)],
                RepositoryName = repos[random.Next(repos.Length)],
                TestRunner = testRunners[random.Next(testRunners.Length)],
                IsDebuggerAttached = random.Next(10) == 0,
                ExecutionEnvironment = environments[random.Next(environments.Length)],
                TotalTests = totalTests,
                PassedTests = passed,
                FailedTests = failed,
                SkippedTests = skipped,
                TotalDurationMs = random.Next(1000, 180000),
                SourceEndpoint = "/dotnet/nunit"
            };

            db.TestRuns.Add(run);

            for (var t = 0; t < options.TestCasesPerRun; t++)
            {
                var status = statuses[random.Next(statuses.Length)];
                db.TestCases.Add(new TestCase
                {
                    TestRunId = runId,
                    OriginalId = Guid.NewGuid().ToString("N"),
                    Name = $"Test_{t:D3}",
                    FullName = $"{run.ProjectName}.Spec.Test_{t:D3}",
                    ClassName = $"{run.ProjectName}.Spec",
                    MethodName = $"Test_{t:D3}",
                    Status = status,
                    DurationMs = random.Next(5, 5000),
                    ErrorMessage = status == "Failed" ? "Randomized failure for perf seed" : null
                });
            }

            if (i % batchSize == 0 && i > 0)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        for (var i = 0; i < options.RawPayloads; i++)
        {
            db.RawPayloads.Add(new RawPayload
            {
                ReceivedAt = start.AddMinutes(random.Next(0, 30 * 24 * 60)),
                Endpoint = random.Next(2) == 0 ? "/dotnet" : "/vite",
                ContentType = "application/json",
                PayloadJson = "{\"seed\":true}"
            });

            if (i % batchSize == 0 && i > 0)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}
