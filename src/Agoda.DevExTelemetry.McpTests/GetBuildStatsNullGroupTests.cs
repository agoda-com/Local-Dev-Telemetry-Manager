using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>Groups whose value is null (no tool version, no reload type) still get their percentiles.</summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetBuildStatsNullGroupTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetBuildStatsNullGroupTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        var at = McpTestHost.Now.AddDays(-1);
        await _host.SeedAsync(
            Seed.Build(at, 100, toolVersion: null),
            Seed.Build(at, 300, toolVersion: null),
            Seed.Build(at, 1000, toolVersion: "10.0.100"),
            Seed.Build(at, 3000, toolVersion: "10.0.100"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task GetBuildStats_NullToolVersionGroup_HasItsOwnPercentiles()
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = "tool_version" });

        var noVersion = result.Rows.Single(r => r["tool_version"] == "");
        noVersion.Int("count").ShouldBe(2);
        noVersion.Long("p50_ms").ShouldBe(200);
        result.Rows.Single(r => r["tool_version"] == "10.0.100").Long("p50_ms").ShouldBe(2000);
    }

    [Test]
    public async Task GetBuildStats_SortedByP50WithNullGroup_KeepsPercentiles()
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = "tool_version", sort = "p50_ms asc" });

        result.Rows.Select(r => r.Long("p50_ms")).ShouldBe([200L, 2000L]);
    }
}
