using System.Globalization;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests.Eval;

/// <summary>
/// The agent eval's "all tools" run, scripted: the analysis tools find every planted problem with their default
/// thresholds, get_overview surfaces each one, and none of the noise is reported.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class AgentEvalAllToolsTests
{
    private static readonly string[] NoiseNames = ["RejectsBadPassword", "ExportsYearEnd", "vitehmr", PlantedProblems.HmrSeriesId];

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public AgentEvalAllToolsTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task FindFlakyTests_RanksTheFlickeringTestFirst()
    {
        var result = await _client.CallAsync("find_flaky_tests", new { repository = PlantedProblems.REPOSITORY });

        result.Rows[0]["test_key"].ShouldEndWith(PlantedProblems.FLAKY_TEST);
        result.Rows[0]["top_errors"].ShouldContain("TimeoutException");
        result.Rows.ShouldNotContain(r => r["test_key"].Contains("RejectsBadPassword"));
    }

    [Test]
    public async Task FindFailingTests_ListsTheBrokenTestWithItsStreakStart()
    {
        var result = await _client.CallAsync("find_failing_tests", new { repository = PlantedProblems.REPOSITORY });

        var broken = result.Rows.ShouldHaveSingleItem();
        broken["test_key"].ShouldEndWith(PlantedProblems.BROKEN_TEST);
        broken["latest_error"].ShouldStartWith("System.NullReferenceException");
        var since = DateTime.Parse(broken["failing_since"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        (McpTestHost.Now - since).TotalDays.ShouldBeLessThan(PlantedProblems.BROKEN_DAYS_AGO);
    }

    [Test]
    public async Task FindSlowTests_ByClass_RanksInventoryFirst()
    {
        var result = await _client.CallAsync("find_slow_tests", new { repository = PlantedProblems.REPOSITORY, group_by = "class" });

        result.Rows[0]["test_key"].ShouldEndWith(PlantedProblems.SLOW_CLASS);
        result.Rows[0].Double("share_of_suite_time").ShouldBeGreaterThan(50);
    }

    [Test]
    public async Task FindRegressions_FlagsTheCompileStepOnTheRightDay()
    {
        var result = await _client.CallAsync("find_regressions", new { repository = PlantedProblems.REPOSITORY, kind = "build" });

        var row = result.Rows.ShouldHaveSingleItem();
        row["subject_id"].ShouldBe(PlantedProblems.CompileSeriesId);
        row["shift_started_on"].ShouldBe(McpTestHost.Now.Date.AddDays(-PlantedProblems.REGRESSION_DAYS_AGO)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        row.Double("change_pct").ShouldBe(35, 5);
    }

    [Test]
    public async Task ExplainBuildTime_RanksToolVersionFirstForTheCompile()
    {
        var result = await _client.CallAsync("explain_build_time", new { series_id = PlantedProblems.CompileSeriesId });

        result.Rows[0]["dimension"].ShouldBe("tool_version");
        result.Rows[0]["value"].ShouldBe(PlantedProblems.NEW_TOOL_VERSION);
    }

    [Test]
    public async Task GetOverview_SurfacesEveryPlantedProblem()
    {
        var result = await _client.CallAsync("get_overview", new { repository = PlantedProblems.REPOSITORY });

        string Top(string section) => result.Rows.First(r => r["section"] == section)["subject_id"];
        Top("find_flaky_tests").ShouldEndWith(PlantedProblems.FLAKY_TEST);
        Top("find_failing_tests").ShouldEndWith(PlantedProblems.BROKEN_TEST);
        Top("find_slow_tests").ShouldContain(PlantedProblems.SLOW_CLASS);
        Top("find_slow_builds").ShouldBe(PlantedProblems.CompileSeriesId);
        Top("find_regressions").ShouldBe(PlantedProblems.CompileSeriesId);
    }

    [Test]
    public async Task GetOverview_DefaultTop_ReportsNoNoise()
    {
        var result = await _client.CallAsync("get_overview", new { repository = PlantedProblems.REPOSITORY });

        foreach (var row in result.Rows)
        foreach (var noise in NoiseNames)
            row["subject_id"].ShouldNotContain(noise, Case.Sensitive, $"{row["section"]} reported noise: {row}");
    }

    /// <summary>
    /// The judging sections never list noise, however many rows are asked for. (The slow-test and slow-build
    /// rankings list everything above min_runs/min_samples, so a long enough list includes ordinary tests.)
    /// </summary>
    [Test]
    public async Task GetOverview_JudgingSections_NeverReportNoise()
    {
        var result = await _client.CallAsync("get_overview", new { repository = PlantedProblems.REPOSITORY, top = 50 });

        foreach (var row in result.Rows.Where(r => r["section"] is "find_flaky_tests" or "find_failing_tests" or "find_regressions"))
        foreach (var noise in NoiseNames)
            row["subject_id"].ShouldNotContain(noise, Case.Sensitive, $"{row["section"]} reported noise: {row}");
    }

    [Test]
    public async Task GetOverview_EveryEvidenceCallSucceeds()
    {
        var result = await _client.CallAsync("get_overview", new { repository = PlantedProblems.REPOSITORY });

        result.Rows.ShouldNotBeEmpty();
        foreach (var row in result.Rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse($"{row["evidence"]}: {evidence.ErrorText}");
            evidence.Rows.ShouldNotBeEmpty(row["evidence"]);
        }
    }
}
