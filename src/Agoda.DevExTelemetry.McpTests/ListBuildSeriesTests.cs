using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ListBuildSeriesTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public ListBuildSeriesTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "2" });
        var at = McpTestHost.Now.AddDays(-2);
        await _host.SeedAsync(
            // .Net series: a null and an empty reload type are the same series. 100,200,300,400 -> p50 250, p95 385.
            Seed.Build(at, 100, reloadType: null),
            Seed.Build(at.AddHours(1), 200, reloadType: ""),
            Seed.Build(at.AddHours(2), 300, reloadType: null),
            Seed.Build(at.AddHours(3), 400, reloadType: null),
            // vitehmr: hot and full are separate series
            Seed.Build(at, 10, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"),
            Seed.Build(at.AddHours(1), 30, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "hot"),
            Seed.Build(at.AddHours(2), 5000, metricType: "vitehmr", project: "Web.Client", category: "Clientside", reloadType: "full"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task ListBuildSeries_HotAndFullReload_AreSeparateSeries()
    {
        var rows = await _client.CallAllPagesAsync("list_build_series");

        rows.Count.ShouldBe(3);
        rows.Select(r => r["reload_type"]).OrderBy(x => x).ShouldBe(["", "full", "hot"]);
        rows.Select(r => r["series_id"]).Distinct().Count().ShouldBe(3);
    }

    [Test]
    public async Task ListBuildSeries_NullAndEmptyReloadType_AreOneSeries()
    {
        var rows = await _client.CallAllPagesAsync("list_build_series", new { metric_type = ".Net" });

        rows.Count.ShouldBe(1);
        rows[0].Int("count").ShouldBe(4);
        rows[0]["series_id"].ShouldBe(StableId.Series("web-app", "Web.Api", ".Net", null));
        rows[0]["build_category"].ShouldBe("API");
    }

    [Test]
    public async Task ListBuildSeries_Percentiles_UseLinearInterpolation()
    {
        var rows = await _client.CallAllPagesAsync("list_build_series", new { metric_type = ".Net" });

        rows[0].Long("avg_ms").ShouldBe(250);
        rows[0].Long("p50_ms").ShouldBe(250);
        rows[0].Long("p95_ms").ShouldBe(385);
        rows[0]["last_at"].ShouldBe(McpTestHost.Now.AddDays(-2).AddHours(3).ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    [Test]
    public async Task ListBuildSeries_DefaultSort_IsCountDescThenSeriesId()
    {
        var rows = await _client.CallAllPagesAsync("list_build_series");

        rows[0].Int("count").ShouldBe(4);
        rows[1]["reload_type"].ShouldBe("hot");
    }

    [Test]
    public async Task ListBuildSeries_SortByP95Ascending_OrdersByPercentile()
    {
        var rows = await _client.CallAllPagesAsync("list_build_series", new { sort = "p95_ms asc" });

        rows.Select(r => r.Long("p95_ms")).ShouldBe([30, 385, 5000], ignoreOrder: false);
    }

    [Test]
    public async Task ListBuildSeries_Filters_NarrowSeries()
    {
        (await _client.CallAllPagesAsync("list_build_series", new { reload_type = "hot" })).Count.ShouldBe(1);
        (await _client.CallAllPagesAsync("list_build_series", new { build_category = "Clientside" })).Count.ShouldBe(2);
        (await _client.CallAllPagesAsync("list_build_series", new { metric_type = "vitehmr", project = "Web.Client" })).Count.ShouldBe(2);
    }

    [Test]
    public async Task ListBuildSeries_FollowingCursors_ReturnsEverySeriesExactlyOnce()
    {
        var first = await _client.CallAsync("list_build_series");
        var all = await _client.CallAllPagesAsync("list_build_series");

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        all.Count.ShouldBe(3);
        all.Select(r => r["series_id"]).Distinct().Count().ShouldBe(3);
    }

    [Test]
    public async Task ListBuildSeries_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("list_build_series");
        var first = await _client.CallAsync("list_build_series");
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(["series_id", "repository", "project", "build_category", "metric_type", "reload_type", "count", "last_at", "avg_ms", "p50_ms", "p95_ms"]);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task ListBuildSeries_Body_NeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("list_build_series");

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task ListBuildSeries_BadSort_ReturnsToolErrorNamingSort()
    {
        var result = await _client.CallAsync("list_build_series", new { sort = "hostname" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("sort");
    }
}
