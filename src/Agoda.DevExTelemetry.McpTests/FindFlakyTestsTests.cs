using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindFlakyTestsTests
{
    private static readonly string[] Columns =
    [
        "test_key", "suite_id", "repository", "project", "test_runner", "class_name", "name", "runs", "failed", "fail_rate",
        "flips", "flip_rate", "last_failed_at", "status_sequence", "top_errors", "reason", "evidence"
    ];

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindFlakyTestsTests(DatabaseProvider provider) => _provider = provider;

    private static readonly DateTime Start = McpTestHost.Now.AddDays(-10);

    /// <summary>
    /// One run per letter, an hour apart, each holding only that test. P passed, F failed, S skipped.
    /// Failures cycle through <paramref name="errors"/>.
    /// </summary>
    private static IEnumerable<TestRun> Series(
        string name, string sequence, string environment = "CI", string[]? branches = null, int startHour = 0, string[]? errors = null)
    {
        var failure = 0;
        for (var i = 0; i < sequence.Length; i++)
        {
            var status = sequence[i] switch { 'P' => "Passed", 'F' => "Failed", _ => "Skipped" };
            string? error = null;
            if (status == "Failed" && errors != null) error = errors[failure++ % errors.Length];
            yield return Seed.Run(
                Start.AddHours(startHour + i),
                [Seed.Case(name, status, 100, "Web.Tests.FlakyClass", error: error)],
                environment: environment,
                branch: branches == null ? "main" : branches[i % branches.Length]);
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "100" });
        var runs = new List<TestRun>();
        // Flicker: 10 runs, 9 flips (flip rate 1), fail rate 50%, three distinct failure messages.
        runs.AddRange(Series("Flicker", "PFPFPFPFPF", errors: ["connection refused\n   at Foo.Bar()", "connection refused", "timeout", "connection refused", "timeout"]));
        // Mild: 6 flips over 9 pairs.
        runs.AddRange(Series("Mild", "PPFPPFPPFP"));
        // Broke for a while then fixed: 33% failures but only 2 flips over 11 pairs.
        runs.AddRange(Series("BrokeAndFixed", "PPPPFFFFPPPP"));
        // Broke and stayed broken: 50% failures, 1 flip.
        runs.AddRange(Series("BrokeAndStayed", "PPPPPFFFFF"));
        // Always failing: never listed as flaky.
        runs.AddRange(Series("AlwaysFails", "FFFFFFFF"));
        // Flips only in local runs.
        runs.AddRange(Series("LocalFlicker", "PFPFPFPFPF", environment: "Local"));
        // Too few runs for the default min_runs of 5.
        runs.AddRange(Series("FewRuns", "PFPF"));
        // Skips are ignored: 6 passed/failed runs with 5 flips, ten executions in all.
        runs.AddRange(Series("WithSkips", "PSFSPSFSPF"));
        // Alternates between two branches, but is steady on each: no flips on any branch.
        runs.AddRange(Series("PerBranchSteady", "PFPFPFPFPF", branches: ["main", "feature"]));
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
    public async Task FindFlakyTests_Columns_AreTheDocumentedOnes()
    {
        var result = await _client.CallAsync("find_flaky_tests");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(Columns);
    }

    [Test]
    public async Task FindFlakyTests_Default_ListsFlickeringTestsRankedByFlipRate()
    {
        var result = await _client.CallAsync("find_flaky_tests");

        // BrokeAndFixed (33% failures, 2 flips) must sit below Mild and above nothing but BrokeAndStayed,
        // although BrokeAndStayed fails more often (50%): flip rate ranks, not fail rate.
        Names(result).ShouldBe(["Flicker", "WithSkips", "Mild", "BrokeAndFixed", "BrokeAndStayed"]);
    }

    [Test]
    public async Task FindFlakyTests_HalfFlippingTest_IsListedWithItsNumbers()
    {
        var flicker = (await _client.CallAsync("find_flaky_tests")).Rows.Single(r => r["name"] == "Flicker");

        flicker["runs"].ShouldBe("10");
        flicker["failed"].ShouldBe("5");
        flicker["fail_rate"].ShouldBe("0.5");
        flicker["flips"].ShouldBe("9");
        flicker["flip_rate"].ShouldBe("1");
        flicker["status_sequence"].ShouldBe("PFPFPFPFPF");
        flicker["last_failed_at"].ShouldBe(Start.AddHours(9).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        flicker["reason"].ShouldContain("9 flips in 10 runs");
    }

    [Test]
    public async Task FindFlakyTests_BrokeAndFixed_HasLowFlipRate()
    {
        var row = (await _client.CallAsync("find_flaky_tests")).Rows.Single(r => r["name"] == "BrokeAndFixed");

        row["flips"].ShouldBe("2");
        row.Double("flip_rate").ShouldBe(2.0 / 11, 0.0001);
    }

    [Test]
    public async Task FindFlakyTests_AlwaysFailingTest_IsNotListedButFailingToolListsIt()
    {
        var flaky = await _client.CallAsync("find_flaky_tests", new { min_runs = 1 });
        var failing = await _client.CallAsync("find_failing_tests");

        Names(flaky).ShouldNotContain("AlwaysFails");
        Names(failing).ShouldContain("AlwaysFails");
    }

    [Test]
    public async Task FindFlakyTests_LocalOnlyFlips_AreExcludedByDefault()
    {
        var ci = await _client.CallAsync("find_flaky_tests");
        var all = await _client.CallAsync("find_flaky_tests", new { environment = "all" });
        var local = await _client.CallAsync("find_flaky_tests", new { environment = "local" });

        Names(ci).ShouldNotContain("LocalFlicker");
        Names(all).ShouldContain("LocalFlicker");
        Names(local).ShouldBe(["LocalFlicker"]);
        ci.Summary.ShouldContain("environment=ci");
    }

    [Test]
    public async Task FindFlakyTests_LocalOrAllEnvironment_CarriesTddCaveat()
    {
        var ci = await _client.CallAsync("find_flaky_tests");
        var all = await _client.CallAsync("find_flaky_tests", new { environment = "all" });
        var local = await _client.CallAsync("find_flaky_tests", new { environment = "local" });

        ci.Summary.ShouldNotContain("TDD");
        all.Summary.ShouldContain("TDD");
        local.Summary.ShouldContain("TDD");
    }

    [Test]
    public async Task FindFlakyTests_MinRuns_ExcludesTestsWithTooFewRuns()
    {
        var defaults = await _client.CallAsync("find_flaky_tests");
        var lowered = await _client.CallAsync("find_flaky_tests", new { min_runs = 4 });
        var raised = await _client.CallAsync("find_flaky_tests", new { min_runs = 11 });

        Names(defaults).ShouldNotContain("FewRuns");
        Names(lowered).ShouldContain("FewRuns");
        Names(raised).ShouldBe(["BrokeAndFixed"]);
    }

    [Test]
    public async Task FindFlakyTests_SkippedExecutions_AreNotRunsAndDoNotBreakFlips()
    {
        var row = (await _client.CallAsync("find_flaky_tests")).Rows.Single(r => r["name"] == "WithSkips");
        var strict = await _client.CallAsync("find_flaky_tests", new { min_runs = 7 });

        row["runs"].ShouldBe("6");
        row["flips"].ShouldBe("5");
        Names(strict).ShouldNotContain("WithSkips");
    }

    [Test]
    public async Task FindFlakyTests_FlipsAreCountedPerBranch()
    {
        var all = await _client.CallAsync("find_flaky_tests", new { min_runs = 1 });
        var main = await _client.CallAsync("find_flaky_tests", new { branch = "main", min_runs = 1 });

        // Steady on each branch although the interleaved results alternate.
        Names(all).ShouldNotContain("PerBranchSteady");
        Names(main).ShouldNotContain("PerBranchSteady");
    }

    [Test]
    public async Task FindFlakyTests_MinFlipRate_RaisesTheThreshold()
    {
        var half = await _client.CallAsync("find_flaky_tests", new { min_flip_rate = 0.5 });
        var one = await _client.CallAsync("find_flaky_tests", new { min_flip_rate = 1.0 });

        Names(half).ShouldBe(["Flicker", "WithSkips", "Mild"]);
        // WithSkips alternates on every Passed/Failed pair too (5 flips over 5 pairs).
        Names(one).ShouldBe(["Flicker", "WithSkips"]);
        half.Summary.ShouldContain("min_flip_rate=0.5");
    }

    [Test]
    public async Task FindFlakyTests_TopErrors_ListsDistinctFirstLinesWithCounts()
    {
        var flicker = (await _client.CallAsync("find_flaky_tests")).Rows.Single(r => r["name"] == "Flicker");

        flicker["top_errors"].ShouldBe("connection refused (3); timeout (2)");
        (await _client.CallAsync("find_flaky_tests")).Rows.Single(r => r["name"] == "Mild")["top_errors"].ShouldBe("Mild failed (3)");
    }

    [Test]
    public async Task FindFlakyTests_EveryRowsEvidence_ShowsBothPassesAndFailures()
    {
        var result = await _client.CallAsync("find_flaky_tests", new { min_runs = 4 });
        result.Rows.Count.ShouldBe(6);

        foreach (var row in result.Rows)
        {
            row["evidence"].ShouldStartWith("get_test ");
            row["evidence"].ShouldContain("environment=ci");
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse($"{row["evidence"]}: {evidence.ErrorText}");
            var statuses = evidence.Rows.Select(e => e["status"]).ToList();
            statuses.ShouldContain("Passed", row["evidence"]);
            statuses.ShouldContain("Failed", row["evidence"]);
            evidence.Rows.Count(e => e["status"] is "Passed" or "Failed").ShouldBe(row.Int("runs"));
            evidence.Rows.Count(e => e["status"] == "Failed").ShouldBe(row.Int("failed"));
        }
    }

    [Test]
    public async Task FindFlakyTests_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("find_flaky_tests", new { limit = 2 });
        var all = await _client.CallAllPagesAsync("find_flaky_tests", new { limit = 2 });

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        all.Select(r => r["name"]).ShouldBe(["Flicker", "WithSkips", "Mild", "BrokeAndFixed", "BrokeAndStayed"]);
    }

    [Test]
    public async Task FindFlakyTests_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("find_flaky_tests", new { limit = 2 });
        var first = await _client.CallAsync("find_flaky_tests", new { limit = 2 });
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindFlakyTests_Output_NeverContainsUserOrHostname()
    {
        var json = await _client.CallAsync("find_flaky_tests", new { format = "json" });
        var csv = await _client.CallAsync("find_flaky_tests");

        foreach (var body in new[] { json.Body, csv.Body })
        {
            body.ShouldNotContain(Seed.USER_NAME);
            body.ShouldNotContain(Seed.HOSTNAME);
        }
    }

    [TestCase("min_runs", -1)]
    [TestCase("min_flip_rate", 1.5)]
    [TestCase("environment", "everywhere")]
    [TestCase("suite_id", "deadbeef")]
    public async Task FindFlakyTests_BadArgument_ReturnsToolErrorNamingIt(string name, object value)
    {
        var result = ToolResult.Parse(await _client.CallToolAsync("find_flaky_tests", new Dictionary<string, object?> { [name] = value }));

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain(name);
    }
}
