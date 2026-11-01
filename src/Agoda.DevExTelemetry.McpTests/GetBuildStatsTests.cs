using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetBuildStatsTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetBuildStatsTests(DatabaseProvider provider) => _provider = provider;

    private static DateTime At(int day, int hour) => new(2026, 3, day, hour, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "2" });
        // 2026-03-10 is a Tuesday, 03-15 a Sunday, 03-16 a Monday (ISO week boundary), 03-17 a Tuesday.
        await _host.SeedAsync(
            Seed.Build(At(10, 9), 100, cpuCount: 8, toolVersion: "10.0.100"),
            Seed.Build(At(10, 10), 200, cpuCount: 8, toolVersion: "10.0.100"),
            Seed.Build(At(10, 11), 300, cpuCount: 8, toolVersion: "10.0.100"),
            Seed.Build(At(10, 12), 400, cpuCount: 8, toolVersion: "10.0.100"),
            Seed.Build(At(15, 12), 1000, cpuCount: 16, toolVersion: "10.0.200"),
            Seed.Build(At(16, 1), 2000, cpuCount: 16, toolVersion: "10.0.200"),
            Seed.Build(At(17, 10), 3000, cpuCount: 8, toolVersion: "10.0.200"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task GetBuildStats_ByDay_IsATimeSeriesWithHandComputedPercentiles()
    {
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "day" });

        rows.Select(r => r["day"]).ShouldBe(["2026-03-10", "2026-03-15", "2026-03-16", "2026-03-17"]);
        var first = rows[0];
        first.Long("count").ShouldBe(4);
        first.Long("avg_ms").ShouldBe(250);
        first.Long("p50_ms").ShouldBe(250);
        first.Long("p95_ms").ShouldBe(385);
        first.Long("max_ms").ShouldBe(400);
        first.Long("total_ms").ShouldBe(1000);
    }

    [Test]
    public async Task GetBuildStats_ByCpuCount_GroupsCorrectly()
    {
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "cpu_count" });

        rows.Select(r => r["cpu_count"]).ShouldBe(["8", "16"].OrderBy(x => int.Parse(x)).Select(x => x));
        var eight = rows.Single(r => r["cpu_count"] == "8");
        eight.Long("count").ShouldBe(5);
        eight.Long("p50_ms").ShouldBe(300);
        eight.Long("p95_ms").ShouldBe(2480);
        eight.Long("avg_ms").ShouldBe(800);
        var sixteen = rows.Single(r => r["cpu_count"] == "16");
        sixteen.Long("p50_ms").ShouldBe(1500);
        sixteen.Long("p95_ms").ShouldBe(1950);
    }

    [Test]
    public async Task GetBuildStats_ByToolVersion_GroupsCorrectly()
    {
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "tool_version" });

        rows.Select(r => r["tool_version"]).ShouldBe(["10.0.100", "10.0.200"]);
        rows[0].Long("count").ShouldBe(4);
        rows[1].Long("count").ShouldBe(3);
        rows[1].Long("p50_ms").ShouldBe(2000);
        rows[1].Long("max_ms").ShouldBe(3000);
    }

    [Test]
    public async Task GetBuildStats_ByWeek_RollsDaysUpAcrossTheWeekBoundary()
    {
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "week" });

        rows.Select(r => r["week"]).ShouldBe(["2026-03-09", "2026-03-16"]);
        // Week of 03-09: 100,200,300,400 (Tue) + 1000 (Sunday 03-15). Week of 03-16: 2000 (Mon), 3000.
        rows[0].Long("count").ShouldBe(5);
        rows[0].Long("total_ms").ShouldBe(2000);
        rows[0].Long("p50_ms").ShouldBe(300);
        rows[0].Long("p95_ms").ShouldBe(880);
        rows[0].Long("max_ms").ShouldBe(1000);
        rows[1].Long("count").ShouldBe(2);
        rows[1].Long("p50_ms").ShouldBe(2500);
        rows[1].Long("avg_ms").ShouldBe(2500);
    }

    [Test]
    public async Task GetBuildStats_ByDayAndCpuCount_KeepsColumnOrderAndGroupsEachPair()
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = "cpu_count,day" });
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "cpu_count,day" });

        result.Columns.ShouldBe(["cpu_count", "day", "count", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms"]);
        rows.Select(r => $"{r["cpu_count"]}/{r["day"]}").ShouldBe(["8/2026-03-10", "8/2026-03-17", "16/2026-03-15", "16/2026-03-16"]);
    }

    [Test]
    public async Task GetBuildStats_SortByStatAndPercentile_OrdersGroups()
    {
        var byTotal = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "day", sort = "total_ms desc" });
        var byP50 = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "day", sort = "p50_ms asc" });

        byTotal.Select(r => r["day"]).ShouldBe(["2026-03-17", "2026-03-16", "2026-03-15", "2026-03-10"]);
        byP50.Select(r => r["day"]).ShouldBe(["2026-03-10", "2026-03-15", "2026-03-16", "2026-03-17"]);
        byP50[0].Long("p50_ms").ShouldBe(250);
    }

    [Test]
    public async Task GetBuildStats_Filters_NarrowBeforeGrouping()
    {
        var rows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "day", cpu_count = 16, min_ms = 1500 });

        rows.Select(r => r["day"]).ShouldBe(["2026-03-16"]);
    }

    [Test]
    public async Task GetBuildStats_FollowingCursors_ReturnsEveryGroupExactlyOnce()
    {
        var first = await _client.CallAsync("get_build_stats", new { group_by = "day" });
        var all = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "day" });

        first.Rows.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();
        first.TotalRows.ShouldBe(4);
        all.Select(r => r["day"]).Distinct().Count().ShouldBe(4);
    }

    [Test]
    public async Task GetBuildStats_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_build_stats", new { group_by = "week" });
        var first = await _client.CallAsync("get_build_stats", new { group_by = "week" });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task GetBuildStats_Body_NeverContainsUserNameOrHostname()
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = "day", format = "json" });

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [TestCase("hostname")]
    [TestCase("user_name")]
    [TestCase("day,hostname")]
    public async Task GetBuildStats_GroupByUnsafeOrUnknownColumn_IsRejectedListingAllowedValues(string groupBy)
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = groupBy });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("group_by");
        result.ErrorText.ShouldContain("debugger_attached");
    }

    [Test]
    public async Task GetBuildStats_SortByColumnNotGrouped_ReturnsToolErrorNamingSort()
    {
        var result = await _client.CallAsync("get_build_stats", new { group_by = "day", sort = "branch" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("sort");
    }
}
