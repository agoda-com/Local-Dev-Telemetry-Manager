using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindSlowTestsTests
{
    private static readonly string[] GetTestsColumns =
    [
        "test_key", "suite_id", "repository", "project", "test_runner", "class_name", "name", "runs", "passed", "failed",
        "skipped", "fail_rate", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms", "first_seen_at", "last_run_at",
        "last_status", "last_failed_at", "status_sequence"
    ];

    private static readonly string NUnitSuite = StableId.Suite("web-app", "Web.Tests", "NUnit");
    private const string FAST = "Web.Tests.FastClass.Fast";
    private const string SLOW = "Web.Tests.SlowClass.Slow";

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindSlowTestsTests(DatabaseProvider provider) => _provider = provider;

    /// <summary>
    /// NUnit suite, 50 runs: Fast (50 x 100 ms = 5000 total, p95 100), Slow (3 x 1000 = 3000, p95 1000),
    /// Rare (2 x 5000 = 10000), and S1..S10 in SetupClass (5 x 300 each: 1500 per test, 15000 for the class).
    /// Suite total 33000. A Vitest suite has one test X (4 x 1000).
    /// </summary>
    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        var runs = new List<TestRun>();
        for (var i = 0; i < 50; i++)
        {
            var cases = new List<TestCase> { Seed.Case("Fast", "Passed", 100, "Web.Tests.FastClass") };
            if (i < 3) cases.Add(Seed.Case("Slow", "Passed", 1000, "Web.Tests.SlowClass"));
            if (i < 2) cases.Add(Seed.Case("Rare", "Passed", 5000, "Web.Tests.RareClass"));
            if (i < 5)
                for (var s = 1; s <= 10; s++) cases.Add(Seed.Case($"S{s}", "Passed", 300, "Web.Tests.SetupClass"));
            runs.Add(Seed.Run(McpTestHost.Now.AddHours(-i - 1), cases));
        }

        for (var i = 0; i < 4; i++)
            runs.Add(Seed.Run(McpTestHost.Now.AddHours(-i - 1), [Seed.Case("X", "Passed", 1000, "src/x.test.ts")], project: "Web.Client", runner: "Vitest"));

        await _host.SeedAsync(runs.ToArray());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static string[] Names(ToolResult r) => r.Rows.Select(x => x["name"]).ToArray();

    [Test]
    public async Task FindSlowTests_Columns_AreGetTestsColumnsThenShareReasonEvidence()
    {
        var result = await _client.CallAsync("find_slow_tests");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe([.. GetTestsColumns, "share_of_suite_time", "reason", "evidence"]);
    }

    [Test]
    public async Task FindSlowTests_Default_RanksByTotalTime()
    {
        var result = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite });

        // Fast runs often (5000 total) and beats Slow (3000) even though Slow is 10x slower per run.
        Names(result).Take(2).ShouldBe(["Fast", "Slow"]);
        result.Rows[0]["total_ms"].ShouldBe("5000");
    }

    [Test]
    public async Task FindSlowTests_SortByP95_RanksTheRarelyRunSlowTestFirst()
    {
        var result = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, sort_by = "p95" });

        // Fast has the biggest total but the smallest p95, so it drops below Slow and the S tests.
        Names(result)[0].ShouldBe("Slow");
        Array.IndexOf(Names(result), "Fast").ShouldBe(Names(result).Length - 1);
        result.Rows[0]["p95_ms"].ShouldBe("1000");
        result.Rows[0]["reason"].ShouldStartWith("p95 1000 ms");
    }

    [Test]
    public async Task FindSlowTests_SortByAvg_RanksByAverage()
    {
        var result = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, sort_by = "avg" });

        Names(result)[0].ShouldBe("Slow");
        result.Rows[0]["reason"].ShouldStartWith("avg 1000 ms");
    }

    [Test]
    public async Task FindSlowTests_MinRuns_ExcludesTestsWithTooFewRuns()
    {
        var defaults = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite });
        var lowered = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, min_runs = 2 });
        var raised = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, min_runs = 6 });

        Names(defaults).ShouldNotContain("Rare");
        Names(lowered)[0].ShouldBe("Rare");
        Names(raised).ShouldBe(["Fast"]);
        defaults.Summary.ShouldContain("min_runs=3");
        raised.Summary.ShouldContain("min_runs=6");
    }

    [Test]
    public async Task FindSlowTests_ShareOfSuiteTime_IsPercentOfTheWholeSuite()
    {
        var result = await _client.CallAsync("find_slow_tests", new { min_runs = 2 });

        // Suite totals: NUnit 33000 (including Rare, which min_runs=2 keeps), Vitest 4000.
        var fast = result.Rows.Single(r => r["name"] == "Fast");
        fast.Double("share_of_suite_time").ShouldBe(15.15, 0.001);
        result.Rows.Single(r => r["name"] == "X").Double("share_of_suite_time").ShouldBe(100, 0.001);
        fast["reason"].ShouldContain("15.15% of suite time");
    }

    [Test]
    public async Task FindSlowTests_ShareOfSuiteTime_IgnoresNameFilters()
    {
        var result = await _client.CallAsync("find_slow_tests", new { name_contains = "Fast" });

        // The suite is still all of its tests, not just the filtered ones.
        result.Rows.Single()["share_of_suite_time"].ShouldBe("15.15");
    }

    [Test]
    public async Task FindSlowTests_GroupByClass_RanksTheClassWithManyTestsSharingSetup()
    {
        var byTest = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite });
        var byClass = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, group_by = "class" });

        // No single S test is among the top 2 tests, but the class they share is the biggest.
        Names(byTest).Take(2).ShouldNotContain(n => n.StartsWith('S') && n.Length <= 3);
        var top = byClass.Rows[0];
        top["class_name"].ShouldBe("Web.Tests.SetupClass");
        top["name"].ShouldBe("");
        top["total_ms"].ShouldBe("15000");
        top.Double("share_of_suite_time").ShouldBe(45.45, 0.001);
        top["evidence"].ShouldStartWith("get_tests ");
        top["evidence"].ShouldContain("group_by=class");
    }

    [Test]
    public async Task FindSlowTests_Filters_NarrowTheRows()
    {
        var suite = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite });
        var runner = await _client.CallAsync("find_slow_tests", new { test_runner = "Vitest" });
        var cls = await _client.CallAsync("find_slow_tests", new { class_contains = "setupclass" });

        suite.Rows.ShouldAllBe(r => r["test_runner"] == "NUnit");
        Names(runner).ShouldBe(["X"]);
        Names(cls).Length.ShouldBe(10);
    }

    [Test]
    public async Task FindSlowTests_EveryRowsEvidence_ReturnsTheRowsBehindTheReason()
    {
        var result = await _client.CallAsync("find_slow_tests", new { min_runs = 2, limit = 100 });
        result.Rows.Count.ShouldBe(14);

        foreach (var row in result.Rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse($"{row["evidence"]}: {evidence.ErrorText}");
            evidence.Rows.Count.ShouldBe(row.Int("runs"), row["evidence"]);
            evidence.Rows.Sum(e => e.Double("duration_ms")).ShouldBe(row.Double("total_ms"), row["evidence"]);
        }
    }

    [Test]
    public async Task FindSlowTests_ClassEvidence_ReturnsTheClassRow()
    {
        var result = await _client.CallAsync("find_slow_tests", new { suite_id = NUnitSuite, group_by = "class", min_runs = 1 });

        foreach (var row in result.Rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse($"{row["evidence"]}: {evidence.ErrorText}");
            var match = evidence.Rows.Single(e => e["test_key"] == row["test_key"]);
            match["total_ms"].ShouldBe(row["total_ms"]);
        }
    }

    [Test]
    public async Task FindSlowTests_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("find_slow_tests", new { min_runs = 1, limit = 3 });
        var all = await _client.CallAllPagesAsync("find_slow_tests", new { min_runs = 1, limit = 3 });

        first.Rows.Count.ShouldBe(3);
        first.NextCursor.ShouldNotBeNull();
        all.Count.ShouldBe(first.TotalRows);
        all.Select(r => r["test_key"]).Distinct().Count().ShouldBe(all.Count);
    }

    [Test]
    public async Task FindSlowTests_Export_ReturnsSameRowsAsTool()
    {
        var args = new { sort_by = "p95", min_runs = 1, limit = 4 };
        var toolRows = await _client.CallAllPagesAsync("find_slow_tests", args);
        var first = await _client.CallAsync("find_slow_tests", args);
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(first.Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindSlowTests_Output_NeverContainsUserOrHostname()
    {
        var json = await _client.CallAsync("find_slow_tests", new { format = "json" });
        var csv = await _client.CallAsync("find_slow_tests");

        foreach (var body in new[] { json.Body, csv.Body })
        {
            body.ShouldNotContain(Seed.USER_NAME);
            body.ShouldNotContain(Seed.HOSTNAME);
        }
    }

    [TestCase("sort_by", "bogus")]
    [TestCase("group_by", "user")]
    [TestCase("min_runs", -1)]
    [TestCase("suite_id", "deadbeef")]
    [TestCase("environment", "everywhere")]
    public async Task FindSlowTests_BadArgument_ReturnsToolErrorNamingIt(string name, object value)
    {
        var result = ToolResult.Parse(await _client.CallToolAsync("find_slow_tests", new Dictionary<string, object?> { [name] = value }));

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain(name);
    }
}
