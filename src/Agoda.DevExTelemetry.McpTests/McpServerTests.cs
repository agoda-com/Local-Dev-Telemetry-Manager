using System.Net;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class McpServerTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public McpServerTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public void Initialize_SendsServerInstructions()
    {
        var instructions = _client.ServerInstructions;

        instructions.ShouldNotBeNull();
        instructions.ShouldContain("Test suite");
        instructions.ShouldContain("environment=ci");
        instructions.ShouldContain("export_url");
    }

    [Test]
    public void Initialize_ReportsServerName()
    {
        _client.ServerInfo.Name.ShouldBe("dx-telemetry");
    }

    [Test]
    public async Task ListTools_IncludesListScopes()
    {
        var tools = await _client.ListToolsAsync();

        tools.Select(t => t.Name).ShouldContain("list_scopes");
    }

    [Test]
    public async Task ListTools_ReturnsAll18Tools()
    {
        var tools = await _client.ListToolsAsync();

        tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ShouldBe([
            "explain_build_time", "find_failing_tests", "find_flaky_tests", "find_regressions", "find_slow_builds",
            "find_slow_tests", "get_build", "get_build_stats", "get_builds", "get_overview", "get_test",
            "get_test_failures", "get_test_run", "get_tests", "list_build_series", "list_scopes", "list_test_runs",
            "list_test_suites"
        ]);
    }

    [Test]
    public async Task ListTools_NoToolTakesUserOrHost()
    {
        var tools = await _client.ListToolsAsync();

        foreach (var tool in tools)
        {
            var schema = tool.JsonSchema.ToString();
            schema.ShouldNotContain("user_name", Case.Insensitive, tool.Name);
            schema.ShouldNotContain("\u0022hostname\u0022", Case.Insensitive, tool.Name);
        }
    }

    [Test]
    public async Task ListTools_EveryToolIsReadOnly()
    {
        var tools = await _client.ListToolsAsync();

        tools.ShouldNotBeEmpty();
        foreach (var tool in tools)
        {
            tool.ProtocolTool.Annotations.ShouldNotBeNull($"{tool.Name} has no annotations");
            tool.ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true, $"{tool.Name} is not read-only");
            tool.ProtocolTool.Annotations.DestructiveHint.ShouldNotBe(true, $"{tool.Name} is destructive");
        }
    }

    [Test]
    public async Task ListTools_EveryToolHasADescription()
    {
        var tools = await _client.ListToolsAsync();

        foreach (var tool in tools)
            tool.Description.ShouldNotBeNullOrWhiteSpace($"{tool.Name} has no description");
    }
}

[TestFixture]
public class McpDisabledTests
{
    [Test]
    public async Task PostMcp_WhenMcpDisabled_Returns404()
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite,
            new Dictionary<string, string?> { ["Mcp:Enabled"] = "false" });
        using var http = host.CreateClient();

        var response = await http.PostAsync("/mcp", new StringContent("{}"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
