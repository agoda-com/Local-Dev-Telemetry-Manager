using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetTestRunTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetTestRunTests(DatabaseProvider provider) => _provider = provider;

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private const string RUN_ID = "9f0000000000000000000000000000aa";
    private static readonly string Suite = StableId.Suite("web-app", "Web.Tests", "NUnit");

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        await _host.SeedAsync(
            // Old enough to be outside the default 14-day window: the window must not apply to a run id.
            Seed.Run(McpTestHost.Now.AddDays(-30),
                [Seed.Case("Fast", "Passed", 10), Seed.Case("Slow", "Passed", 900), Seed.Case("Bad", "Failed", 50, error: "bang\ndetail"),
                 Seed.Case("Worse", "Failed", 70), Seed.Case("Skip", "Skipped", null), Seed.Case("Mid", "Passed", 300)],
                id: RUN_ID, branch: "release", totalMs: 51234),
            Seed.Run(McpTestHost.Now.AddDays(-1), [Seed.Case("Other")]));
        _client = await _host.ConnectAsync();
    }

    [Test]
    public async Task GetTestRun_RunId_ReturnsHeaderInSummaryAndCasesInDashboardOrder()
    {
        var result = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, limit = 50 });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["test_key", "class_name", "name", "status", "duration_ms", "error"]);
        result.Summary.ShouldContain($"run {RUN_ID} suite {Suite} (web-app / Web.Tests / NUnit)");
        result.Summary.ShouldContain("branch=release environment=CI");
        result.Summary.ShouldContain("total=6 passed=3 failed=2 skipped=1 total_ms=51234");
        result.Rows.Select(r => r["name"]).ShouldBe(["Worse", "Bad", "Slow", "Mid", "Fast", "Skip"]);
        result.Rows.Single(r => r["name"] == "Bad")["error"].ShouldBe("bang");
        result.Rows[0]["test_key"].ShouldBe(TestKey.Format(Suite, "Web.Tests.LoginTests.Worse"));
        result.TotalRows.ShouldBe(6);
    }

    [Test]
    public async Task GetTestRun_Output_NeverContainsUserOrHost()
    {
        var result = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, limit = 3 });

        (result.Summary + result.Body).ShouldNotContain(Seed.USER_NAME);
        (result.Summary + result.Body).ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task GetTestRun_SortDuration_OrdersByDurationDescendingOrAscending()
    {
        var desc = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, sort = "duration_ms", limit = 50 });
        var asc = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, sort = "duration_ms asc", limit = 50 });

        desc.Rows.Select(r => r["name"]).First().ShouldBe("Slow");
        asc.Rows.Select(r => r["name"]).Last().ShouldBe("Slow");
    }

    [Test]
    public async Task GetTestRun_SortName_OrdersByName()
    {
        var result = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, sort = "name asc", limit = 50 });

        result.Rows.Select(r => r["name"]).ShouldBe(["Bad", "Fast", "Mid", "Skip", "Slow", "Worse"]);
    }

    [Test]
    public async Task GetTestRun_StatusAndNameFilters_Apply()
    {
        var failed = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, status = "failed" });
        var named = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, name_contains = "SLOW" });

        failed.Rows.Select(r => r["name"]).ShouldBe(["Worse", "Bad"]);
        failed.TotalRows.ShouldBe(2);
        named.Rows.Select(r => r["name"]).ShouldBe(["Slow"]);
    }

    [Test]
    public async Task GetTestRun_UnknownId_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test_run", new { test_run_id = "nope" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("test_run_id");
    }

    [Test]
    public async Task GetTestRun_BadSort_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, sort = "error" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("sort");
    }

    [Test]
    public async Task GetTestRun_OffsetCursor_ReturnsEveryCaseExactlyOnce()
    {
        var first = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, limit = 3 });
        var all = await _client.CallAllPagesAsync("get_test_run", new { test_run_id = RUN_ID, limit = 3 });

        first.Rows.Count.ShouldBe(3);
        first.Truncated.ShouldBeTrue();
        all.Count.ShouldBe(6);
        all.Select(r => r["name"]).Distinct().Count().ShouldBe(6);
    }

    [Test]
    public async Task GetTestRun_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_test_run", new { test_run_id = RUN_ID, limit = 3 });
        var first = await _client.CallAsync("get_test_run", new { test_run_id = RUN_ID, limit = 3 });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }
}
