using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindFailingTestsTests
{
    private static readonly string[] Columns =
    [
        "test_key", "suite_id", "repository", "project", "test_runner", "class_name", "name", "runs", "failed", "fail_rate",
        "failing_since", "last_passed_at", "latest_error", "branches", "status_sequence", "reason", "evidence"
    ];

    private static readonly DateTime Start = McpTestHost.Now.AddDays(-10);

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindFailingTestsTests(DatabaseProvider provider) => _provider = provider;

    /// <summary>
    /// One run per letter, an hour apart from <paramref name="startHour"/>, each holding only that test.
    /// P passed, F failed, S skipped. The last failure's error is "latest failure", earlier ones "older failure".
    /// </summary>
    private static IEnumerable<TestRun> Series(
        string name, string sequence, string environment = "CI", string[]? branches = null, int startHour = 0)
    {
        var lastFailure = sequence.LastIndexOf('F');
        for (var i = 0; i < sequence.Length; i++)
        {
            var status = sequence[i] switch { 'P' => "Passed", 'F' => "Failed", _ => "Skipped" };
            var error = status == "Failed" ? (i == lastFailure ? "latest failure" : "older failure") : null;
            yield return Seed.Run(
                Start.AddHours(startHour + i),
                [Seed.Case(name, status, 100, "Web.Tests.BrokenClass", error: error)],
                environment: environment,
                branch: branches == null ? "main" : branches[i % branches.Length]);
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "100" });
        var runs = new List<TestRun>();
        // Failing since hour 0 on two branches, never passed.
        runs.AddRange(Series("AlwaysFails", "FFFFFFFFFF", branches: ["main", "feature"]));
        // Broke at hour 2 (80% failures), so both rules match.
        runs.AddRange(Series("BrokeEarly", "PPFFFFFFFF"));
        // Broke at hour 15: only 50% failures, found by the recent-runs rule.
        runs.AddRange(Series("BrokeLater", "PPPPPFFFFF", startHour: 10));
        // Failed locally only, since hour 20.
        runs.AddRange(Series("LocalFails", "FFFFF", environment: "Local", startHour: 20));
        // 90% failures but passed last: listed by rate, not currently failing.
        runs.AddRange(Series("NearlyDead", "FFFFFFFFFP"));
        // Ends on a failure but alternates: neither rule.
        runs.AddRange(Series("Flicker", "PFPFPFPFPF"));
        // Only two runs, both failed.
        runs.AddRange(Series("Rare", "FF"));
        // Never fails.
        runs.AddRange(Series("Healthy", "PPPPPPPP"));
        // Skipped runs are ignored: four failed runs in between.
        runs.AddRange(Series("WithSkips", "FSFSFSF", startHour: 30));
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

    private static string Iso(int hour) => Start.AddHours(hour).ToString("yyyy-MM-ddTHH:mm:ssZ");

    [Test]
    public async Task FindFailingTests_Columns_AreTheDocumentedOnes()
    {
        var result = await _client.CallAsync("find_failing_tests");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(Columns);
    }

    [Test]
    public async Task FindFailingTests_Default_ListsBrokenTestsFailingStreaksFirstOldestFirst()
    {
        var result = await _client.CallAsync("find_failing_tests");

        // Failing now, by the start of the streak: AlwaysFails (h0), BrokeEarly (h2), BrokeLater (h15), LocalFails (h20), WithSkips (h30).
        // Then the one that passed last, by fail rate. Flicker, Rare and Healthy match neither rule.
        Names(result).ShouldBe(["AlwaysFails", "BrokeEarly", "BrokeLater", "LocalFails", "WithSkips", "NearlyDead"]);
    }

    [Test]
    public async Task FindFailingTests_FailingStreak_ReportsStartLastPassLatestErrorAndBranches()
    {
        var result = await _client.CallAsync("find_failing_tests");
        var always = result.Rows.Single(r => r["name"] == "AlwaysFails");
        var later = result.Rows.Single(r => r["name"] == "BrokeLater");

        always["runs"].ShouldBe("10");
        always["failed"].ShouldBe("10");
        always["fail_rate"].ShouldBe("1");
        always["failing_since"].ShouldBe(Iso(0));
        always["last_passed_at"].ShouldBe("");
        always["latest_error"].ShouldBe("latest failure");
        always["branches"].ShouldBe("feature;main");
        later["failing_since"].ShouldBe(Iso(15));
        later["last_passed_at"].ShouldBe(Iso(14));
        later["fail_rate"].ShouldBe("0.5");
        later["status_sequence"].ShouldBe("PPPPPFFFFF");
        later["reason"].ShouldContain("last 3 runs all failed");
    }

    [Test]
    public async Task FindFailingTests_HighFailRateWithLatestPass_IsListedButNotFailingSince()
    {
        var row = (await _client.CallAsync("find_failing_tests")).Rows.Single(r => r["name"] == "NearlyDead");

        row["failing_since"].ShouldBe("");
        row["last_passed_at"].ShouldBe(Iso(9));
        row["fail_rate"].ShouldBe("0.9");
        row["latest_error"].ShouldBe("latest failure");
        row["branches"].ShouldBe("main");
    }

    [Test]
    public async Task FindFailingTests_SkippedExecutions_AreIgnored()
    {
        var row = (await _client.CallAsync("find_failing_tests")).Rows.Single(r => r["name"] == "WithSkips");

        row["runs"].ShouldBe("4");
        row["failing_since"].ShouldBe(Iso(30));
    }

    [Test]
    public async Task FindFailingTests_EnvironmentDefaultsToAll_AndCanBeNarrowed()
    {
        var all = await _client.CallAsync("find_failing_tests");
        var ci = await _client.CallAsync("find_failing_tests", new { environment = "ci" });
        var local = await _client.CallAsync("find_failing_tests", new { environment = "local" });

        Names(all).ShouldContain("LocalFails");
        Names(ci).ShouldNotContain("LocalFails");
        Names(local).ShouldBe(["LocalFails"]);
    }

    [Test]
    public async Task FindFailingTests_FlakyAndAlwaysFailing_AreSeparatedBetweenTheTools()
    {
        var failing = await _client.CallAsync("find_failing_tests");
        var flaky = await _client.CallAsync("find_flaky_tests");

        Names(failing).ShouldContain("AlwaysFails");
        Names(failing).ShouldNotContain("Flicker");
        Names(flaky).ShouldNotContain("AlwaysFails");
        Names(flaky).ShouldContain("Flicker");
    }

    [Test]
    public async Task FindFailingTests_MinFailRate_RaisedToOne_DropsTheTestFailingNinetyPercent()
    {
        var defaults = await _client.CallAsync("find_failing_tests");
        var strict = await _client.CallAsync("find_failing_tests", new { min_fail_rate = 1.0 });
        var lenient = await _client.CallAsync("find_failing_tests", new { min_fail_rate = 0.5 });

        Names(defaults).ShouldContain("NearlyDead");
        Names(strict).ShouldNotContain("NearlyDead");
        // Still there through the other rules.
        Names(strict).ShouldContain("BrokeEarly");
        // 50% failures now qualifies Flicker.
        Names(lenient).ShouldContain("Flicker");
        strict.Summary.ShouldContain("min_fail_rate=1");
    }

    [Test]
    public async Task FindFailingTests_MinRuns_LoweredIncludesTheRareTest()
    {
        var defaults = await _client.CallAsync("find_failing_tests");
        var lowered = await _client.CallAsync("find_failing_tests", new { min_runs = 2 });

        Names(defaults).ShouldNotContain("Rare");
        Names(lowered).ShouldContain("Rare");
        lowered.Summary.ShouldContain("min_runs=2");
    }

    [Test]
    public async Task FindFailingTests_RecentRuns_ChangesWhichTestsAreStillFailing()
    {
        var two = await _client.CallAsync("find_failing_tests", new { recent_runs = 2 });
        var one = await _client.CallAsync("find_failing_tests", new { recent_runs = 1 });
        var six = await _client.CallAsync("find_failing_tests", new { recent_runs = 6, min_fail_rate = 1.0 });

        Names(two).ShouldContain("Rare");
        Names(two).ShouldNotContain("Flicker");
        Names(one).ShouldContain("Flicker");
        // Six failures in a row no longer holds for BrokeLater (5 in a row, 50% overall). The others stay: BrokeEarly by
        // its 8 in a row, and AlwaysFails, LocalFails and WithSkips by a 100% fail rate.
        Names(six).ShouldBe(["AlwaysFails", "BrokeEarly", "LocalFails", "WithSkips"]);
    }

    [Test]
    public async Task FindFailingTests_EveryRowsEvidence_ReturnsTheFailuresBehindTheReason()
    {
        var result = await _client.CallAsync("find_failing_tests", new { min_runs = 2 });
        result.Rows.Count.ShouldBe(7);

        foreach (var row in result.Rows)
        {
            row["evidence"].ShouldStartWith("get_test_failures ");
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse($"{row["evidence"]}: {evidence.ErrorText}");
            evidence.Rows.Count.ShouldBe(row.Int("failed"), row["evidence"]);
            evidence.Rows.ShouldAllBe(e => e["test_key"] == row["test_key"]);
            evidence.Rows[0]["error"].ShouldBe(row["latest_error"]);
        }
    }

    [Test]
    public async Task FindFailingTests_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("find_failing_tests", new { limit = 2 });
        var all = await _client.CallAllPagesAsync("find_failing_tests", new { limit = 2 });

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        all.Select(r => r["name"]).ShouldBe(["AlwaysFails", "BrokeEarly", "BrokeLater", "LocalFails", "WithSkips", "NearlyDead"]);
    }

    [Test]
    public async Task FindFailingTests_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("find_failing_tests", new { limit = 2 });
        var first = await _client.CallAsync("find_failing_tests", new { limit = 2 });
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindFailingTests_Output_NeverContainsUserOrHostname()
    {
        var json = await _client.CallAsync("find_failing_tests", new { format = "json" });
        var csv = await _client.CallAsync("find_failing_tests");

        foreach (var body in new[] { json.Body, csv.Body })
        {
            body.ShouldNotContain(Seed.USER_NAME);
            body.ShouldNotContain(Seed.HOSTNAME);
        }
    }

    [TestCase("min_fail_rate", 1.5)]
    [TestCase("min_runs", -1)]
    [TestCase("recent_runs", 0)]
    [TestCase("environment", "everywhere")]
    [TestCase("suite_id", "deadbeef")]
    public async Task FindFailingTests_BadArgument_ReturnsToolErrorNamingIt(string name, object value)
    {
        var result = ToolResult.Parse(await _client.CallToolAsync("find_failing_tests", new Dictionary<string, object?> { [name] = value }));

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain(name);
    }
}
