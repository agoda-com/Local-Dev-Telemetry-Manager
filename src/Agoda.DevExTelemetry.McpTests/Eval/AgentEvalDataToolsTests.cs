using System.Globalization;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests.Eval;

/// <summary>
/// The agent eval's "data tools only" run, scripted: the calls an agent would make with the generic data tools
/// and no analysis tools. Each planted problem must be findable, and the noise must be distinguishable.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class AgentEvalDataToolsTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public AgentEvalDataToolsTests(DatabaseProvider provider) => _provider = provider;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _host = new McpTestHost(_provider);
        await _host.SeedAsync(PlantedProblems.Build(McpTestHost.Now).ToArray());
        _client = await _host.ConnectAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task FlakyTest_CiTestsWithPassesAndFailures_FlickeringSequenceSetsItApart()
    {
        var result = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, environment = "ci", status = "has_passes_and_failures", min_runs = 5
        });

        var byKey = result.Rows.ToDictionary(r => r["test_key"].Split(':', 2)[1]);
        byKey.Keys.ShouldBe([PlantedProblems.FLAKY_TEST, PlantedProblems.BROKEN_TEST], ignoreOrder: true);
        Flips(byKey[PlantedProblems.FLAKY_TEST]["status_sequence"]).ShouldBeGreaterThanOrEqualTo(8);
        Flips(byKey[PlantedProblems.BROKEN_TEST]["status_sequence"]).ShouldBe(1);
        byKey[PlantedProblems.FLAKY_TEST].Double("fail_rate").ShouldBe(0.4, 0.001);
    }

    [Test]
    public async Task FlakyTest_FailuresShareOneTimeoutError()
    {
        var result = await _client.CallAsync("get_test_failures", new
        {
            test_key = $"{PlantedProblems.SuiteId}:{PlantedProblems.FLAKY_TEST}", environment = "ci"
        });

        result.Rows.Count.ShouldBe(12);
        result.Rows.Select(r => r["error"]).Distinct().ShouldHaveSingleItem().ShouldContain("TimeoutException");
    }

    [Test]
    public async Task TddTest_FlipsOnlyLocally()
    {
        var ci = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, environment = "ci", name_contains = "RejectsBadPassword"
        });
        var local = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, environment = "local", name_contains = "RejectsBadPassword"
        });

        ci.Rows.ShouldHaveSingleItem().Int("failed").ShouldBe(0);
        local.Rows.ShouldHaveSingleItem().Int("failed").ShouldBe(6);
    }

    [Test]
    public async Task BrokenTest_SequenceEndsInUnbrokenFailures()
    {
        var result = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, environment = "ci", status = "last_failed"
        });

        var broken = result.Rows.Single(r => r["test_key"].EndsWith(PlantedProblems.BROKEN_TEST, StringComparison.Ordinal));
        broken["status_sequence"].ShouldMatch("^P+F{6,}$");

        var failures = await _client.CallAsync("get_test_failures", new { test_key = broken["test_key"] });
        failures.Rows.ShouldAllBe(r => r["error"].StartsWith("System.NullReferenceException", StringComparison.Ordinal));
    }

    [Test]
    public async Task SlowClass_ClassRollupRanksInventoryFirst()
    {
        var result = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, group_by = "class", sort = "total_ms desc", limit = 3
        });

        result.Rows[0]["test_key"].ShouldEndWith(PlantedProblems.SLOW_CLASS);
        var perTest = await _client.CallAsync("get_tests", new
        {
            suite_id = PlantedProblems.SuiteId, sort = "total_ms desc", min_runs = 3, limit = 1
        });
        perTest.Rows[0]["test_key"].ShouldContain(PlantedProblems.SLOW_CLASS);
    }

    [Test]
    public async Task CompileRegression_DailyStatsShowTheStepOnTheDayTheToolVersionChanged()
    {
        var result = await _client.CallAsync("get_build_stats", new
        {
            series_id = PlantedProblems.CompileSeriesId, group_by = "day", days = 21, limit = 100
        });

        var stepDay = McpTestHost.Now.Date.AddDays(-PlantedProblems.REGRESSION_DAYS_AGO).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var before = result.Rows.Where(r => string.CompareOrdinal(r["day"], stepDay) < 0).Select(r => r.Double("p50_ms")).ToList();
        var after = result.Rows.Where(r => string.CompareOrdinal(r["day"], stepDay) >= 0).Select(r => r.Double("p50_ms")).ToList();
        before.ShouldNotBeEmpty();
        after.ShouldNotBeEmpty();
        before.Max().ShouldBeLessThan(after.Min());
        (after.Average() / before.Average()).ShouldBe(1.35, 0.05);

        var byVersion = await _client.CallAsync("get_build_stats", new
        {
            series_id = PlantedProblems.CompileSeriesId, group_by = "tool_version", days = 21
        });
        byVersion.Rows.Single(r => r["tool_version"] == PlantedProblems.NEW_TOOL_VERSION).Double("p50_ms")
            .ShouldBeGreaterThan(byVersion.Rows.Single(r => r["tool_version"] == PlantedProblems.OLD_TOOL_VERSION).Double("p50_ms") * 1.25);
    }

    [Test]
    public async Task Noise_RareTestAndHmrSeries_HaveTooFewSamples()
    {
        var rare = await _client.CallAsync("get_tests", new { suite_id = PlantedProblems.SuiteId, name_contains = "ExportsYearEnd" });
        var series = await _client.CallAsync("list_build_series", new { metric_type = "vitehmr" });
        var withMinRuns = await _client.CallAsync("get_tests", new { suite_id = PlantedProblems.SuiteId, name_contains = "ExportsYearEnd", min_runs = 3 });

        rare.Rows.ShouldHaveSingleItem().Int("runs").ShouldBe(2);
        series.Rows.ShouldHaveSingleItem().Int("count").ShouldBe(4);
        withMinRuns.Rows.ShouldBeEmpty();
    }

    private static int Flips(string sequence)
    {
        var outcomes = sequence.Where(c => c is 'P' or 'F').ToArray();
        return outcomes.Zip(outcomes.Skip(1)).Count(p => p.First != p.Second);
    }
}
