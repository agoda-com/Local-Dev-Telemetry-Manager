using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindSlowBuildsTests
{
    private static readonly string NetId = StableId.Series("web-app", "Web.Api", ".Net", null);
    private static readonly string ViteId = StableId.Series("web-app", "Web.Client", "vitehmr", "hot");
    private static readonly string GradleId = StableId.Series("web-app", "Android", "Gradle", null);

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindSlowBuildsTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "2" });
        var now = McpTestHost.Now;
        var builds = new List<object>();
        // The default window is 14 days, so its halves split at now - 7 days.
        for (var i = 0; i < 6; i++)
        {
            // .Net: 1000 ms in the first half, 1500 ms in the second -> p50 1250, p95 1500, trend +50%.
            builds.Add(Seed.Build(now.AddDays(-13 + i), 1000));
            builds.Add(Seed.Build(now.AddDays(-5 + i), 1500));
            // vitehmr hot: flat 1300 ms -> p50 1300 (slower than .Net by p50), p95 1300 (faster by p95).
            builds.Add(Seed.Build(now.AddDays(-13 + i), 1300, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"));
            builds.Add(Seed.Build(now.AddDays(-5 + i), 1300, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"));
        }
        // Gradle: only 4 builds, slowest of all.
        for (var i = 0; i < 4; i++)
            builds.Add(Seed.Build(now.AddDays(-3 - i), 9000, metricType: "Gradle", project: "Android", category: "Mobile"));
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
    public async Task FindSlowBuilds_Columns_ArePrefixedWithListBuildSeriesColumns()
    {
        var list = await _client.CallAsync("list_build_series");
        var result = await _client.CallAsync("find_slow_builds");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe([.. list.Columns, "trend_pct", "reason", "evidence"]);
    }

    [Test]
    public async Task FindSlowBuilds_DefaultSort_RanksByP50AndExcludesSmallSeries()
    {
        var rows = await _client.CallAllPagesAsync("find_slow_builds");

        rows.Select(r => r["series_id"]).ShouldBe([ViteId, NetId]);
        rows[0].Long("p50_ms").ShouldBe(1300);
        rows[1].Long("p50_ms").ShouldBe(1250);
    }

    [Test]
    public async Task FindSlowBuilds_SortByP95_RanksDifferentlyFromP50()
    {
        var rows = await _client.CallAllPagesAsync("find_slow_builds", new { sort_by = "p95" });

        rows.Select(r => r["series_id"]).ShouldBe([NetId, ViteId]);
        rows[0].Long("p95_ms").ShouldBe(1500);
    }

    [Test]
    public async Task FindSlowBuilds_MinSamples_ExcludesSmallSeriesAndLowerValueIncludesThem()
    {
        var defaults = await _client.CallAllPagesAsync("find_slow_builds");
        var lowered = await _client.CallAllPagesAsync("find_slow_builds", new { min_samples = 3 });

        defaults.Select(r => r["series_id"]).ShouldNotContain(GradleId);
        lowered.Select(r => r["series_id"]).ShouldBe([GradleId, ViteId, NetId]);
    }

    [Test]
    public async Task FindSlowBuilds_SeriesThatGotSlower_HasPositiveTrend()
    {
        var rows = await _client.CallAllPagesAsync("find_slow_builds");

        rows.Single(r => r["series_id"] == NetId).Double("trend_pct").ShouldBe(50.0);
        rows.Single(r => r["series_id"] == ViteId).Double("trend_pct").ShouldBe(0.0);
    }

    [Test]
    public async Task FindSlowBuilds_HalfWithFewerThanMinSamples_HasEmptyTrend()
    {
        // Each half holds 6 builds of the .Net and vitehmr series; Gradle has 4 in the second half only.
        var rows = await _client.CallAllPagesAsync("find_slow_builds", new { min_samples = 4 });

        rows.Single(r => r["series_id"] == NetId).Double("trend_pct").ShouldBe(50.0);
        rows.Single(r => r["series_id"] == GradleId).NullableString("trend_pct").ShouldBeNull();

        var strict = await _client.CallAllPagesAsync("find_slow_builds", new { min_samples = 7 });
        strict.ShouldBeEmpty();
    }

    [Test]
    public async Task FindSlowBuilds_Filters_NarrowTheSeries()
    {
        var byType = await _client.CallAllPagesAsync("find_slow_builds", new { metric_type = ".Net" });
        var byReload = await _client.CallAllPagesAsync("find_slow_builds", new { reload_type = "hot" });
        var byCategory = await _client.CallAllPagesAsync("find_slow_builds", new { build_category = "Clientside" });

        byType.Select(r => r["series_id"]).ShouldBe([NetId]);
        byReload.Select(r => r["series_id"]).ShouldBe([ViteId]);
        byCategory.Select(r => r["series_id"]).ShouldBe([ViteId]);
    }

    [Test]
    public async Task FindSlowBuilds_Reason_NamesTheNumbers()
    {
        var rows = await _client.CallAllPagesAsync("find_slow_builds");

        rows.Single(r => r["series_id"] == NetId)["reason"].ShouldContain("p50 1250 ms over 12 builds");
        rows.Single(r => r["series_id"] == NetId)["reason"].ShouldContain("+50%");
    }

    [Test]
    public async Task FindSlowBuilds_Evidence_ReturnsDayStatsThatShowTheTrend()
    {
        var rows = await _client.CallAllPagesAsync("find_slow_builds");

        foreach (var row in rows)
        {
            var evidence = await _client.CallEvidenceAsync(row["evidence"]);
            evidence.IsError.ShouldBeFalse(evidence.ErrorText);
            evidence.Rows.Sum(r => r.Long("count")).ShouldBe(row.Long("count"));
        }

        var net = await _client.CallEvidenceAsync(rows.Single(r => r["series_id"] == NetId)["evidence"]);
        net.Rows.Select(r => r.Long("p50_ms")).Distinct().OrderBy(x => x).ShouldBe([1000L, 1500L]);
    }

    [Test]
    public async Task FindSlowBuilds_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("find_slow_builds", new { min_samples = 3 });
        var all = await _client.CallAllPagesAsync("find_slow_builds", new { min_samples = 3 });

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        first.TotalRows.ShouldBe(3);
        all.Select(r => r["series_id"]).ShouldBe([GradleId, ViteId, NetId]);
    }

    [Test]
    public async Task FindSlowBuilds_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("find_slow_builds", new { min_samples = 3 });
        var first = await _client.CallAsync("find_slow_builds", new { min_samples = 3 });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task FindSlowBuilds_Body_NeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("find_slow_builds", new { format = "json" });

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task FindSlowBuilds_BadSortBy_ReturnsToolErrorNamingArgument()
    {
        var result = await _client.CallAsync("find_slow_builds", new { sort_by = "p99" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("sort_by");
    }

    [Test]
    public async Task FindSlowBuilds_ZeroMinSamples_ReturnsToolErrorNamingArgument()
    {
        var result = await _client.CallAsync("find_slow_builds", new { min_samples = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("min_samples");
    }
}
