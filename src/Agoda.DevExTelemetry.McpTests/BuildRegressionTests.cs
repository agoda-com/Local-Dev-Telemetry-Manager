using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>The <c>build</c> detector of find_regressions. Default windows: current 03-11 12:00..03-18 12:00, baseline 28 days before.</summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class BuildRegressionTests
{
    private static readonly string NetId = StableId.Series("web-app", "Web.Api", ".Net", null);
    private static readonly string ViteId = StableId.Series("web-app", "Web.Client", "vitehmr", "hot");
    private static readonly string GradleId = StableId.Series("web-app", "Android", "Gradle", null);

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public BuildRegressionTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "1" });
        var now = McpTestHost.Now;
        var builds = new List<object>();

        // .Net: 1000 ms for 12 baseline builds; in the current window the first 4 builds stay at 1000 ms (days -6, -5)
        // and from 2026-03-14 on a new tool version makes it 1350 ms (+35%): p50 1350.
        for (var i = 0; i < 12; i++)
            builds.Add(Seed.Build(now.AddDays(-30 + i * 2), 1000));
        for (var day = 6; day >= 5; day--)
            for (var k = 0; k < 2; k++)
                builds.Add(Seed.Build(now.AddDays(-day).AddHours(k), 1000));
        for (var day = 4; day >= 1; day--)
            for (var k = 0; k < 2; k++)
                builds.Add(Seed.Build(now.AddDays(-day).AddHours(k), 1350, toolVersion: "10.0.200"));

        // vitehmr hot: +10% only, below the 20% threshold.
        for (var i = 0; i < 12; i++)
        {
            builds.Add(Seed.Build(now.AddDays(-30 + i * 2), 1000, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"));
            builds.Add(Seed.Build(now.AddDays(-6 + i * 0.4), 1100, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"));
        }

        // Gradle: tripled, but only 4 builds per window.
        for (var i = 0; i < 4; i++)
        {
            builds.Add(Seed.Build(now.AddDays(-20 - i), 1000, metricType: "Gradle", project: "Android", category: "Mobile"));
            builds.Add(Seed.Build(now.AddDays(-2 - i), 3000, metricType: "Gradle", project: "Android", category: "Mobile"));
        }
        await _host.SeedAsync(builds.ToArray());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private Task<List<CsvRow>> BuildRegressions(object? args = null) =>
        _client.CallAllPagesAsync("find_regressions", Merge(args));

    private static Dictionary<string, object?> Merge(object? args)
    {
        var dict = new Dictionary<string, object?> { ["kind"] = "build" };
        if (args != null)
            foreach (var p in args.GetType().GetProperties()) dict[p.Name] = p.GetValue(args);
        return dict;
    }

    [Test]
    public async Task FindRegressions_StepChangeInOneSeries_IsDetectedWithTheStartDate()
    {
        var rows = await BuildRegressions();

        rows.Select(r => r["subject_id"]).ShouldBe([NetId]);
        var row = rows[0];
        row["kind"].ShouldBe("build");
        row["name"].ShouldBe(".Net");
        row["repository"].ShouldBe("web-app");
        row["project"].ShouldBe("Web.Api");
        row.Int("baseline_samples").ShouldBe(12);
        row.Int("current_samples").ShouldBe(12);
        row.Long("before_p50_ms").ShouldBe(1000);
        row.Long("after_p50_ms").ShouldBe(1350);
        row.Double("change_pct").ShouldBe(35.0);
        row["shift_started_on"].ShouldBe("2026-03-14");
        row["reason"].ShouldContain("p50 1000 ms → 1350 ms (+35%) since 2026-03-14");
    }

    [Test]
    public async Task FindRegressions_NoiseBelowMinSamples_IsNotFlagged()
    {
        var rows = await BuildRegressions();

        rows.Select(r => r["subject_id"]).ShouldNotContain(GradleId);
    }

    [Test]
    public async Task FindRegressions_IncreaseBelowThreshold_IsNotFlagged()
    {
        var rows = await BuildRegressions();

        rows.Select(r => r["subject_id"]).ShouldNotContain(ViteId);
    }

    [Test]
    public async Task FindRegressions_LowerMinIncrease_FlagsTheSmallerIncreaseAndNamesTheReloadType()
    {
        var rows = await BuildRegressions(new { min_increase_pct = 5.0 });

        rows.Select(r => r["subject_id"]).ShouldBe([NetId, ViteId]);
        var vite = rows[1];
        vite["name"].ShouldBe("vitehmr (hot)");
        vite.Double("change_pct").ShouldBe(10.0);
        vite["shift_started_on"].ShouldNotBeEmpty();
    }

    [Test]
    public async Task FindRegressions_HigherMinIncrease_DropsTheSeries()
    {
        var rows = await BuildRegressions(new { min_increase_pct = 50.0 });

        rows.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRegressions_LowerMinSamples_FlagsTheSmallSeries()
    {
        var rows = await BuildRegressions(new { min_samples = 4 });

        rows.Select(r => r["subject_id"]).ShouldBe([GradleId, NetId]);
        rows[0].Double("change_pct").ShouldBe(200.0);
    }

    [Test]
    public async Task FindRegressions_HigherMinSamples_DropsTheSeries()
    {
        var rows = await BuildRegressions(new { min_samples = 13 });

        rows.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRegressions_ShorterCurrentWindow_ChangesWhatIsCompared()
    {
        // The last 3 days hold only 1350 ms builds (6 of them), baseline = the 28 days before (includes the old 1000 ms and 1350 ms builds).
        var rows = await BuildRegressions(new { current_days = 3, baseline_days = 28, min_samples = 6 });

        rows.Single(r => r["subject_id"] == NetId)["shift_started_on"].ShouldBe("2026-03-15");
    }

    [Test]
    public async Task FindRegressions_Evidence_DayStatsShowTheStep()
    {
        var rows = await BuildRegressions();

        var evidence = await _client.CallEvidenceAsync(rows[0]["evidence"]);

        evidence.IsError.ShouldBeFalse(evidence.ErrorText);
        evidence.Rows.Sum(r => r.Long("count")).ShouldBe(24);
        var byDay = evidence.Rows.ToDictionary(r => r["day"], r => r.Long("p50_ms"));
        byDay["2026-03-13"].ShouldBe(1000);
        byDay["2026-03-14"].ShouldBe(1350);
        byDay["2026-03-17"].ShouldBe(1350);
    }

    [Test]
    public async Task FindRegressions_Evidence_RunsForEveryFlaggedRow()
    {
        var rows = await BuildRegressions(new { min_increase_pct = 5.0, min_samples = 4 });

        rows.Count.ShouldBe(3);
        foreach (var row in rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse(evidence.ErrorText);
            evidence.Rows.Sum(r => r.Long("count")).ShouldBe(row.Long("baseline_samples") + row.Long("current_samples"));
        }
    }

    [Test]
    public async Task FindRegressions_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var args = new { kind = "build", min_increase_pct = 5.0, min_samples = 4 };
        var first = await _client.CallAsync("find_regressions", args);
        var all = await _client.CallAllPagesAsync("find_regressions", args);

        first.Rows.Count.ShouldBe(1);
        first.NextCursor.ShouldNotBeNull();
        first.TotalRows.ShouldBe(3);
        all.Select(r => r["subject_id"]).ShouldBe([GradleId, NetId, ViteId]);
    }

    [Test]
    public async Task FindRegressions_BuildKind_ExportReturnsSameRowsAsTool()
    {
        var args = new { kind = "build", min_increase_pct = 5.0, min_samples = 4 };
        var toolRows = await _client.CallAllPagesAsync("find_regressions", args);
        var first = await _client.CallAsync("find_regressions", args);
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindRegressions_BuildKind_BodyNeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "build", format = "json" });

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task FindRegressions_BuildKindWithBadMinSamples_ReturnsToolErrorNamingArgument()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "build", min_samples = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("min_samples");
    }
}
