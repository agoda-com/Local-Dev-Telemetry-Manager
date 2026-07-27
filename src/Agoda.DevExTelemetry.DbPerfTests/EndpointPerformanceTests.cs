using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.DbPerfTests;

[TestFixture]
public class EndpointPerformanceTests
{
    private static readonly string[] EndpointPaths =
    [
        "/api/build-metrics?page=1&pageSize=50&environment=all",
        "/api/test-runs?page=1&pageSize=50&environment=all",
        "/api/build-metrics/api-summary?environment=all",
        "/api/build-metrics/clientside-summary?environment=all",
        "/api/test-runs/summary?environment=all"
    ];

    [TestCase(DatabaseProvider.Sqlite)]
    [TestCase(DatabaseProvider.PostgreSql)]
    public async Task Should_seed_and_measure_dashboard_endpoints(DatabaseProvider provider)
    {
        var targetProvider = ResolveProviderFromEnvironment(provider);
        if (targetProvider != provider)
        {
            Assert.Ignore($"Skipping {provider}. PERF_DB_PROVIDER={Environment.GetEnvironmentVariable("PERF_DB_PROVIDER")}");
            return;
        }

        using var factory = new PerfWebApplicationFactory(provider);
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();

        var options = new PerfSeedOptions(
            BuildMetrics: GetIntEnv("PERF_BUILD_METRICS", 20_000),
            TestRuns: GetIntEnv("PERF_TEST_RUNS", 10_000),
            TestCasesPerRun: GetIntEnv("PERF_TEST_CASES_PER_RUN", 8),
            RawPayloads: GetIntEnv("PERF_RAW_PAYLOADS", 10_000));

        await factory.SeedIfNeededAsync(options);

        // warm-up
        foreach (var endpoint in EndpointPaths)
        {
            for (var i = 0; i < 2; i++)
            {
                var warmup = await client.GetAsync(endpoint);
                warmup.EnsureSuccessStatusCode();
            }
        }

        var results = new List<EndpointTimingResult>();
        foreach (var endpoint in EndpointPaths)
        {
            var timings = new List<double>();
            const int iterations = 10;

            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var response = await client.GetAsync(endpoint);
                sw.Stop();

                response.EnsureSuccessStatusCode();
                timings.Add(sw.Elapsed.TotalMilliseconds);
            }

            timings.Sort();
            var result = new EndpointTimingResult(
                Engine: provider.ToString(),
                Endpoint: endpoint,
                Iterations: timings.Count,
                MeanMs: timings.Average(),
                P50Ms: Percentile(timings, 0.50),
                P95Ms: Percentile(timings, 0.95),
                P99Ms: Percentile(timings, 0.99),
                CollectedAtUtc: DateTime.UtcNow);

            results.Add(result);

            // Non-strict guardrail: endpoint should not be catastrophically slow in perf harness.
            result.P95Ms.ShouldBeLessThan(10_000);
        }

        var outputDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "db-perf-results");
        Directory.CreateDirectory(outputDir);
        var outputFile = Path.Combine(outputDir, $"endpoint-timings-{provider.ToString().ToLowerInvariant()}.json");
        await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        TestContext.AddTestAttachment(outputFile, $"Endpoint timing results for {provider}");
    }

    private static DatabaseProvider ResolveProviderFromEnvironment(DatabaseProvider fallback)
    {
        var env = Environment.GetEnvironmentVariable("PERF_DB_PROVIDER");
        if (string.IsNullOrWhiteSpace(env))
            return fallback;

        return env.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DatabaseProvider.Sqlite,
            "postgres" => DatabaseProvider.PostgreSql,
            "postgresql" => DatabaseProvider.PostgreSql,
            _ => fallback
        };
    }

    private static int GetIntEnv(string name, int @default)
    {
        var env = Environment.GetEnvironmentVariable(name);
        return int.TryParse(env, out var parsed) && parsed > 0 ? parsed : @default;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var rank = percentile * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sortedValues[lower];

        var weight = rank - lower;
        return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
    }
}
