using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>
/// find_regressions for kind=test and kind=suite. Defaults: current = the 7 days before Now, baseline = the 28 before.
/// Seeded in web-app / Web.Tests / NUnit, one run per day: 12 baseline runs (8 to 19 days ago) and 7 current runs
/// (0 to 6 days ago, at Now minus k days). From 3 days ago (2026-03-15) the Step test takes 150 ms instead of 100 ms.
/// A second suite, mobile-app, has only 3 slow current runs.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class TestRegressionTests
{
    private const int BASELINE_RUNS = 12;
    private const int CURRENT_RUNS = 7;
    private const int MIN_SAMPLES = 5;

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public TestRegressionTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        await _host.SeedAsync(SeedRuns());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static object[] SeedRuns()
    {
        var runs = new List<object>();

        // Baseline: 12 runs, 8..19 days ago.
        for (var i = 0; i < BASELINE_RUNS; i++)
        {
            var at = McpTestHost.Now.AddDays(-8 - i);
            runs.Add(Seed.Run(at, [
                Seed.Case("Step", durationMs: 100),
                Seed.Case("Mild", durationMs: 100),
                Seed.Case("Steady", durationMs: 100),
                Seed.Case("Noise", durationMs: 100),
                Seed.Case("FailedSlow", durationMs: 100),
                Seed.Case("FlakyFail", durationMs: 100)
            ], totalMs: 1000));
            runs.Add(Seed.Run(at, [Seed.Case("MobileSteady", durationMs: 100, className: "Mobile.Tests.Home")],
                repository: "mobile-app", project: "Mobile.Tests", runner: "Vitest", totalMs: 1000));
        }

        // Current: 7 runs, 0..6 days ago. Step is slow from k = 3 (2026-03-15).
        for (var k = 0; k < CURRENT_RUNS; k++)
        {
            var at = McpTestHost.Now.AddDays(-k);
            runs.Add(Seed.Run(at, [
                Seed.Case("Step", durationMs: k <= 3 ? 150 : 100),
                Seed.Case("Mild", durationMs: 110),
                Seed.Case("Steady", durationMs: 100),
                // Only 3 current samples, much slower: noise below min_samples.
                k < 3 ? Seed.Case("Noise", durationMs: 500) : Seed.Case("Other", durationMs: 100),
                // Passes at 100 but also fails slowly: failures must not count.
                Seed.Case("FailedSlow", durationMs: 100),
                Seed.Case("FailedSlow", status: "Failed", durationMs: 1000),
                // Mostly failing slowly, only 2 passing runs.
                k < 2 ? Seed.Case("FlakyFail", durationMs: 100) : Seed.Case("FlakyFail", status: "Failed", durationMs: 400)
            ], totalMs: k <= 3 ? 1500 : 1000));
            if (k < 3)
                runs.Add(Seed.Run(at, [Seed.Case("MobileSteady", durationMs: 100, className: "Mobile.Tests.Home")],
                    repository: "mobile-app", project: "Mobile.Tests", runner: "Vitest", totalMs: 3000));
        }

        return runs.ToArray();
    }

    private static string WebTestKey(string name) =>
        TestKey.Format(StableId.Suite("web-app", "Web.Tests", "NUnit"), "Web.Tests.LoginTests." + name);

    private static readonly string WebSuiteId = StableId.Suite("web-app", "Web.Tests", "NUnit");

    private static DateTime Utc(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    [Test]
    public async Task FindRegressions_TestStepChange_IsFlaggedWithStartDate()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = MIN_SAMPLES });

        result.IsError.ShouldBeFalse(result.ErrorText);
        var row = result.Rows.ShouldHaveSingleItem();
        row["kind"].ShouldBe("test");
        row["subject_id"].ShouldBe(WebTestKey("Step"));
        row["name"].ShouldBe("Web.Tests.LoginTests.Step");
        row["repository"].ShouldBe("web-app");
        row["project"].ShouldBe("Web.Tests");
        row.Int("baseline_samples").ShouldBe(BASELINE_RUNS);
        row.Int("current_samples").ShouldBe(CURRENT_RUNS);
        row.Long("before_p50_ms").ShouldBe(100);
        row.Long("after_p50_ms").ShouldBe(150);
        row.Double("change_pct").ShouldBe(50, 0.1);
        row["shift_started_on"].ShouldBe("2026-03-15");
        row["reason"].ShouldContain("p50 100 ms");
        row["reason"].ShouldContain("150 ms");
        row["reason"].ShouldContain("+50%");
        row["reason"].ShouldContain("since 2026-03-15");
        row["reason"].ShouldContain("12 vs 7 passing runs");
    }

    [Test]
    public async Task FindRegressions_TestNoiseBelowMinSamples_IsNotFlagged()
    {
        var result = await _client.CallAsync("find_regressions",
            new { kind = "test", min_samples = MIN_SAMPLES, min_increase_pct = 5.0 });

        result.Rows.Select(r => r["name"]).ShouldNotContain("Web.Tests.LoginTests.Noise");
    }

    [Test]
    public async Task FindRegressions_TestMinSamplesOverride_ChangesResult()
    {
        var lowered = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = 3 });
        var raised = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = 8 });
        var defaults = await _client.CallAsync("find_regressions", new { kind = "test" });

        lowered.Rows.Select(r => r["name"]).OrderBy(n => n).ToList()
            .ShouldBe(["Web.Tests.LoginTests.Noise", "Web.Tests.LoginTests.Step"]);
        var noise = lowered.Rows.Single(r => r["name"].EndsWith(".Noise"));
        noise.Int("current_samples").ShouldBe(3);
        noise.Double("change_pct").ShouldBe(400, 0.1);
        raised.Rows.ShouldBeEmpty();
        defaults.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRegressions_TestIncreaseBelowMinIncreasePct_IsNotFlagged()
    {
        var defaults = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = MIN_SAMPLES });
        var lower = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = MIN_SAMPLES, min_increase_pct = 5.0 });
        var higher = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = MIN_SAMPLES, min_increase_pct = 60.0 });

        defaults.Rows.Select(r => r["name"]).ShouldNotContain("Web.Tests.LoginTests.Mild");
        lower.Rows.Select(r => r["name"]).ShouldBe(["Web.Tests.LoginTests.Step", "Web.Tests.LoginTests.Mild"]);
        lower.Rows.Single(r => r["name"].EndsWith(".Mild")).Double("change_pct").ShouldBe(10, 0.1);
        lower.Rows.Single(r => r["name"].EndsWith(".Mild"))["shift_started_on"].ShouldBe("2026-03-12");
        higher.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRegressions_TestFailedExecutions_AreNotSamples()
    {
        var result = await _client.CallAsync("find_regressions",
            new { kind = "test", min_samples = 2, min_increase_pct = 5.0 });

        // FailedSlow passes at 100 ms everywhere (its 1000 ms failures are ignored); FlakyFail has 2 passing current runs
        // at 100 ms (its 400 ms failures are ignored), so neither is slower.
        result.Rows.Select(r => r["name"]).ShouldNotContain("Web.Tests.LoginTests.FailedSlow");
        result.Rows.Select(r => r["name"]).ShouldNotContain("Web.Tests.LoginTests.FlakyFail");
    }

    [Test]
    public async Task FindRegressions_SuiteStepChange_IsFlaggedWithStartDate()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "suite", min_samples = MIN_SAMPLES });

        result.IsError.ShouldBeFalse(result.ErrorText);
        var row = result.Rows.ShouldHaveSingleItem();
        row["kind"].ShouldBe("suite");
        row["subject_id"].ShouldBe(WebSuiteId);
        row["name"].ShouldBe("web-app / Web.Tests / NUnit");
        row.Int("baseline_samples").ShouldBe(BASELINE_RUNS);
        row.Int("current_samples").ShouldBe(CURRENT_RUNS);
        row.Long("before_p50_ms").ShouldBe(1000);
        row.Long("after_p50_ms").ShouldBe(1500);
        row.Double("change_pct").ShouldBe(50, 0.1);
        row["shift_started_on"].ShouldBe("2026-03-15");
        row["reason"].ShouldContain("12 vs 7 runs");
    }

    [Test]
    public async Task FindRegressions_SuiteOverrides_ChangeResult()
    {
        var lowered = await _client.CallAsync("find_regressions", new { kind = "suite", min_samples = 3 });
        var strict = await _client.CallAsync("find_regressions", new { kind = "suite", min_samples = MIN_SAMPLES, min_increase_pct = 60.0 });
        var defaults = await _client.CallAsync("find_regressions", new { kind = "suite" });

        lowered.Rows.Select(r => r["name"]).ShouldBe(
            ["mobile-app / Mobile.Tests / Vitest", "web-app / Web.Tests / NUnit"]);
        strict.Rows.ShouldBeEmpty();
        defaults.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRegressions_SuiteNoiseBelowMinSamples_IsNotFlagged()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "suite", min_samples = MIN_SAMPLES });

        result.Rows.Select(r => r["repository"]).ShouldNotContain("mobile-app");
    }

    [Test]
    public async Task FindRegressions_TestEvidence_ShowsBothWindowsWithSlowerDurations()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "test", min_samples = MIN_SAMPLES });
        var row = result.Rows.ShouldHaveSingleItem();

        var evidence = await _client.CallEvidenceAsync(row["evidence"]);

        evidence.IsError.ShouldBeFalse(evidence.ErrorText);
        evidence.Rows.Count.ShouldBe(BASELINE_RUNS + CURRENT_RUNS);
        evidence.Rows.ShouldAllBe(r => r["status"] == "Passed" || r["status"] == "passed");
        var currentFrom = McpTestHost.Now.AddDays(-7);
        var before = evidence.Rows.Where(r => Utc(r["received_at"]) < currentFrom).ToList();
        var after = evidence.Rows.Where(r => Utc(r["received_at"]) >= currentFrom).ToList();
        before.Count.ShouldBe(BASELINE_RUNS);
        after.Count.ShouldBe(CURRENT_RUNS);
        before.ShouldAllBe(r => r.Long("duration_ms") == 100);
        after.Max(r => r.Long("duration_ms")).ShouldBe(150);
        after.Count(r => r.Long("duration_ms") == 150).ShouldBe(4);
    }

    [Test]
    public async Task FindRegressions_SuiteEvidence_ShowsBothWindowsWithSlowerRuns()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "suite", min_samples = MIN_SAMPLES });
        var row = result.Rows.ShouldHaveSingleItem();

        var evidence = await _client.CallEvidenceAsync(row["evidence"]);

        evidence.IsError.ShouldBeFalse(evidence.ErrorText);
        evidence.Rows.Count.ShouldBe(BASELINE_RUNS + CURRENT_RUNS);
        evidence.Rows.Count(r => r.Long("total_ms") == 1000).ShouldBe(BASELINE_RUNS + 3);
        evidence.Rows.Count(r => r.Long("total_ms") == 1500).ShouldBe(4);
    }

    [Test]
    public async Task FindRegressions_TestExport_ReturnsSameRowsAsTool()
    {
        var args = new { kind = "test", min_samples = 3 };
        var toolRows = await _client.CallAllPagesAsync("find_regressions", args);
        var first = await _client.CallAsync("find_regressions", args);
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        toolRows.Count.ShouldBe(2);
        columns.ShouldBe(first.Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindRegressions_TestFollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        using var host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "1" });
        await host.SeedAsync(SeedRuns());
        await using var client = await host.ConnectAsync();
        var args = new { min_samples = 3, min_increase_pct = 5.0 };

        var first = await client.CallAsync("find_regressions", args);
        var all = await client.CallAllPagesAsync("find_regressions", args);

        first.Rows.Count.ShouldBe(1);
        first.Truncated.ShouldBeTrue();
        all.Count.ShouldBe(first.TotalRows);
        all.Count.ShouldBeGreaterThan(3);
        all.Select(r => r.ToString()).Distinct().Count().ShouldBe(all.Count);
    }

    [Test]
    public async Task FindRegressions_TestAndSuiteKinds_NeverReturnUserNameOrHostname()
    {
        foreach (var kind in new[] { "test", "suite" })
        {
            var result = await _client.CallAsync("find_regressions", new { kind, min_samples = 3 });

            result.IsError.ShouldBeFalse(result.ErrorText);
            result.Rows.ShouldNotBeEmpty();
            result.Body.ShouldNotContain(Seed.USER_NAME);
            result.Body.ShouldNotContain(Seed.HOSTNAME);
        }
    }

    [Test]
    public async Task FindRegressions_BadCurrentDays_ReturnsToolErrorNamingArgument()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "test", current_days = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("current_days");
    }
}
