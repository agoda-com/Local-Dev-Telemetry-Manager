using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetBuildsTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetBuildsTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "2" });
        var tie = McpTestHost.Now.AddDays(-1);
        await _host.SeedAsync(
            // Three builds share a timestamp; the order inside the tie is Id desc.
            Seed.Build(tie, 100, id: "a1", cpuCount: 4, toolVersion: "9.0.1", commitSha: "abc123"),
            Seed.Build(tie, 200, id: "a2", cpuCount: 8, toolVersion: "10.0.1"),
            Seed.Build(tie, 300, id: "a3", cpuCount: 8, toolVersion: "10.0.1", platform: "Windows", os: "Windows 11"),
            Seed.Build(tie.AddHours(-1), 400, id: "a0", metricType: "vitehmr", category: "Clientside", reloadType: "hot", project: "Web.Client"),
            Seed.Build(tie.AddHours(-2), 5000, id: "z9", metricType: "vitehmr", category: "Clientside", reloadType: "full", project: "Web.Client"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static string[] Ids(IEnumerable<CsvRow> rows) => rows.Select(r => r["id"]).ToArray();

    [Test]
    public async Task GetBuilds_NoFilter_ReturnsNewestFirstWithIdDescendingInTies()
    {
        var rows = await _client.CallAllPagesAsync("get_builds");

        Ids(rows).ShouldBe(["a3", "a2", "a1", "a0", "z9"]);
    }

    [Test]
    public async Task GetBuilds_KeysetCursorAcrossTie_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("get_builds");
        var second = await _client.CallAsync("get_builds", new { cursor = first.NextCursor });

        Ids(first.Rows).ShouldBe(["a3", "a2"]);
        Ids(second.Rows).ShouldBe(["a1", "a0"]);
        first.TotalRows.ShouldBe(5);
        second.TotalRows.ShouldBe(5);
        (await _client.CallAllPagesAsync("get_builds")).Count.ShouldBe(5);
    }

    [Test]
    public async Task GetBuilds_Columns_AreTheDocumentedOnes()
    {
        var result = await _client.CallAsync("get_builds", new { cpu_count = 4 });

        result.Columns.ShouldBe(["id", "received_at", "repository", "project", "build_category", "metric_type", "reload_type", "time_ms",
            "branch", "environment", "platform", "os", "cpu_count", "tool_version", "debugger_attached", "commit_sha"]);
        result.Rows.Single()["commit_sha"].ShouldBe("abc123");
        result.Rows.Single()["time_ms"].ShouldBe("100");
    }

    [Test]
    public async Task GetBuilds_Filters_Narrow()
    {
        Ids(await _client.CallAllPagesAsync("get_builds", new { cpu_count = 8 })).ShouldBe(["a3", "a2", "a0", "z9"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { tool_version = "10.0.1" })).ShouldBe(["a3", "a2"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { platform = "Windows" })).ShouldBe(["a3"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { os = "Windows 11" })).ShouldBe(["a3"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { min_ms = 400 })).ShouldBe(["a0", "z9"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { metric_type = "vitehmr" })).ShouldBe(["a0", "z9"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { build_category = "Clientside", reload_type = "full" })).ShouldBe(["z9"]);
    }

    [Test]
    public async Task GetBuilds_SeriesId_ReturnsOnlyThatSeries()
    {
        var hot = StableId.Series("web-app", "Web.Client", "vitehmr", "hot");

        Ids(await _client.CallAllPagesAsync("get_builds", new { series_id = hot })).ShouldBe(["a0"]);
        Ids(await _client.CallAllPagesAsync("get_builds", new { series_id = StableId.Series("web-app", "Web.Api", ".Net", null) })).ShouldBe(["a3", "a2", "a1"]);
    }

    [Test]
    public async Task GetBuilds_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_builds", new { min_ms = 200 });
        var first = await _client.CallAsync("get_builds", new { min_ms = 200 });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
        toolRows.Count.ShouldBe(4);
    }

    [Test]
    public async Task GetBuilds_Body_NeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("get_builds", new { format = "json" });

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task GetBuilds_UnknownSeries_ReturnsToolErrorNamingSeriesId()
    {
        var result = await _client.CallAsync("get_builds", new { series_id = "deadbeef" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("series_id");
    }
}
