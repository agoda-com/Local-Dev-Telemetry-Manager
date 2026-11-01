using System.Net;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ListScopesTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public ListScopesTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "3" });
        await _host.SeedAsync(
            Seed.Run(McpTestHost.Now.AddDays(-1), [Seed.Case("A")], repository: "web-app", runner: "NUnit"),
            Seed.Run(McpTestHost.Now.AddDays(-2), [Seed.Case("B")], repository: "mobile-app", runner: "Vitest", branch: "feature/x"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 1200, metricType: ".Net"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 300, metricType: "vitehmr", category: "Clientside", reloadType: "hot"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task ListScopes_KindFilter_ReturnsOnlyThatKind()
    {
        var result = await _client.CallAsync("list_scopes", new { kind = "test_runner" });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["kind", "value"]);
        result.Rows.Select(r => r["value"]).ShouldBe(["NUnit", "Vitest"]);
        result.Rows.ShouldAllBe(r => r["kind"] == "test_runner");
    }

    [Test]
    public async Task ListScopes_MoreRowsThanMaxRows_IsTruncatedWithCursor()
    {
        var result = await _client.CallAsync("list_scopes", new { limit = 50 });

        result.Rows.Count.ShouldBe(3);
        result.Truncated.ShouldBeTrue();
        result.NextCursor.ShouldNotBeNull();
        result.TotalRows.ShouldBeGreaterThan(3);
        result.Summary.ShouldContain("truncated");
    }

    [Test]
    public async Task ListScopes_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("list_scopes");
        var all = await _client.CallAllPagesAsync("list_scopes");

        all.Count.ShouldBe(first.TotalRows);
        all.Select(r => r.ToString()).Distinct().Count().ShouldBe(all.Count);
        all.ShouldContain(r => r["kind"] == "branch" && r["value"] == "feature/x");
        all.ShouldContain(r => r["kind"] == "metric_type" && r["value"] == "vitehmr");
    }

    [Test]
    public async Task ListScopes_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("list_scopes");
        var first = await _client.CallAsync("list_scopes");
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(["kind", "value"]);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task ListScopes_FormatJson_ReturnsJsonArray()
    {
        var result = await _client.CallAsync("list_scopes", new { kind = "repository", format = "json" });

        result.Body.ShouldStartWith("[");
        result.Body.ShouldContain("\"kind\":\"repository\"");
        result.Body.ShouldContain("\"value\":\"mobile-app\"");
    }

    [Test]
    public async Task ListScopes_BadKind_ReturnsToolError()
    {
        var result = await _client.CallAsync("list_scopes", new { kind = "user" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("kind");
    }

    [Test]
    public async Task ListScopes_BadCursor_ReturnsToolError()
    {
        var result = await _client.CallAsync("list_scopes", new { cursor = "not-a-cursor" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("cursor");
    }

    [Test]
    public async Task Export_UnknownDataset_Returns404()
    {
        using var http = _host.CreateClient();

        var response = await http.GetAsync("/api/export/no_such_thing.csv");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Export_BadArgument_Returns400()
    {
        using var http = _host.CreateClient();

        var response = await http.GetAsync("/api/export/list_scopes.csv?kind=user");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("kind");
    }
}
