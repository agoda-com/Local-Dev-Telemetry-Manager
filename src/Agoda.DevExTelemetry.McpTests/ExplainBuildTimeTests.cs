using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ExplainBuildTimeTests
{
    private static readonly string NetId = StableId.Series("web-app", "Web.Api", ".Net", null);
    private static readonly string GradleId = StableId.Series("web-app", "Android", "Gradle", null);

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public ExplainBuildTimeTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "2" });
        var now = McpTestHost.Now;
        var builds = new List<object>();
        // Only the CPU count matters (4 cores 2300 ms, 16 cores 1000 ms). Every other dimension is a binary digit of i,
        // so each of its two groups holds the same mix of CPU counts and has the same p50.
        for (var i = 0; i < 128; i++)
        {
            var slow = i % 2 == 0;
            builds.Add(Seed.Build(
                now.AddHours(-1 - i),
                slow ? 2300 : 1000,
                cpuCount: slow ? 4 : 16,
                os: (i / 2) % 2 == 0 ? "Ubuntu 22.04" : "Windows 11",
                platform: (i / 4) % 2 == 0 ? "Linux" : "Windows",
                toolVersion: (i / 8) % 2 == 0 ? "10.0.100" : "10.0.200",
                branch: (i / 16) % 2 == 0 ? "main" : "feature",
                environment: (i / 32) % 2 == 0 ? "Local" : "CI",
                debuggerAttached: i >= 64));
        }
        // Noise: a series too small for any group to reach min_samples.
        for (var i = 0; i < 4; i++)
            builds.Add(Seed.Build(now.AddHours(-1 - i), 500 + i * 400, metricType: "Gradle", project: "Android", cpuCount: i % 2 == 0 ? 4 : 16));
        await _host.SeedAsync(builds.ToArray());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task ExplainBuildTime_OnlyCpuCountMatters_RanksCpuCountFirst()
    {
        var rows = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId });

        rows[0]["dimension"].ShouldBe("cpu_count");
        rows[0].Int("dimension_rank").ShouldBe(1);
        rows[0].Double("dimension_spread").ShouldBe(2.3);
        rows.Where(r => r["dimension"] != "cpu_count").Select(r => r.Double("dimension_spread")).ShouldAllBe(s => s == 1.0);
    }

    [Test]
    public async Task ExplainBuildTime_Default_ReturnsThreeDimensionsWithOneRowPerGroupSlowestFirst()
    {
        var rows = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId });

        rows.Count.ShouldBe(6);
        rows.Select(r => r["dimension"]).Distinct().Count().ShouldBe(3);
        rows.Select(r => r.Int("dimension_rank")).ShouldBe([1, 1, 2, 2, 3, 3]);
        rows[0]["value"].ShouldBe("4");
        rows[0].Long("p50_ms").ShouldBe(2300);
        rows[0].Long("count").ShouldBe(64);
        rows[0].Double("ratio_to_fastest").ShouldBe(2.3);
        rows[1]["value"].ShouldBe("16");
        rows[1].Long("p50_ms").ShouldBe(1000);
        rows[1].Double("ratio_to_fastest").ShouldBe(1.0);
    }

    [Test]
    public async Task ExplainBuildTime_SlowestGroup_ReasonNamesTheRatioToTheFastestGroup()
    {
        var rows = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId, top_dimensions = 1 });

        rows[0]["reason"].ShouldStartWith("4-core machines: p50 2.3× the 16-core machines");
        rows[1]["reason"].ShouldContain("16-core machines");
    }

    [Test]
    public async Task ExplainBuildTime_TopDimensions_LimitsTheDimensions()
    {
        var one = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId, top_dimensions = 1 });
        var all = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId, top_dimensions = 7 });

        one.Select(r => r["dimension"]).Distinct().ShouldBe(["cpu_count"]);
        all.Select(r => r["dimension"]).Distinct().Count().ShouldBe(7);
        all.Select(r => r["dimension"]).ShouldContain("debugger_attached");
        all.Single(r => r["dimension"] == "debugger_attached" && r["value"] == "true").Long("count").ShouldBe(64);
    }

    [Test]
    public async Task ExplainBuildTime_MinSamples_ExcludesSmallGroups()
    {
        var strict = await _client.CallAsync("explain_build_time", new { series_id = NetId, min_samples = 65 });
        var exact = await _client.CallAsync("explain_build_time", new { series_id = NetId, min_samples = 64, top_dimensions = 1 });

        strict.Rows.ShouldBeEmpty();
        strict.Summary.ShouldContain("note:");
        exact.Rows.Count.ShouldBe(2);
    }

    [Test]
    public async Task ExplainBuildTime_OnlyNoise_ReturnsEmptyTableWithNote()
    {
        var result = await _client.CallAsync("explain_build_time", new { series_id = GradleId });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Rows.ShouldBeEmpty();
        result.Columns.ShouldBe([
            "dimension", "value", "count", "avg_ms", "p50_ms", "p95_ms", "ratio_to_fastest", "dimension_spread",
            "dimension_rank", "reason", "evidence"
        ]);
        result.Summary.ShouldContain("noise");
    }

    [Test]
    public async Task ExplainBuildTime_Evidence_ReturnsGroupStatsMatchingEachRow()
    {
        var rows = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId });

        foreach (var row in rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse(evidence.ErrorText);
            var group = evidence.Rows.Single(r => r[row["dimension"]] == row["value"]);
            group.Long("count").ShouldBe(row.Long("count"));
            group.Long("p50_ms").ShouldBe(row.Long("p50_ms"));
        }
    }

    [Test]
    public async Task ExplainBuildTime_UnknownSeries_ReturnsToolErrorNamingSeriesId()
    {
        var result = await _client.CallAsync("explain_build_time", new { series_id = "deadbeef" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("series_id");
    }

    [Test]
    public async Task ExplainBuildTime_ZeroTopDimensions_ReturnsToolErrorNamingArgument()
    {
        var result = await _client.CallAsync("explain_build_time", new { series_id = NetId, top_dimensions = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("top_dimensions");
    }

    [Test]
    public async Task ExplainBuildTime_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("explain_build_time", new { series_id = NetId });
        var all = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId });

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        first.TotalRows.ShouldBe(6);
        all.Select(r => r["dimension"] + "=" + r["value"]).Distinct().Count().ShouldBe(6);
    }

    [Test]
    public async Task ExplainBuildTime_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("explain_build_time", new { series_id = NetId });
        var first = await _client.CallAsync("explain_build_time", new { series_id = NetId });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task ExplainBuildTime_Body_NeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("explain_build_time", new { series_id = NetId, format = "json" });

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }
}
