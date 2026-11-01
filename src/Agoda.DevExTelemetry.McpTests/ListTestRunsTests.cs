using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ListTestRunsTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public ListTestRunsTests(DatabaseProvider provider) => _provider = provider;

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static readonly string Suite = StableId.Suite("web-app", "Web.Tests", "NUnit");
    private static readonly string OtherSuite = StableId.Suite("web-app", "Api.Tests", "xUnit");
    private static DateTime T(double hoursAgo) => McpTestHost.Now.AddHours(-hoursAgo);

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        await _host.SeedAsync(
            Seed.Run(T(1), [Seed.Case("A"), Seed.Case("B", "Failed")], totalMs: 1234),
            Seed.Run(T(2), [Seed.Case("A"), Seed.Case("B", "Skipped")]),
            Seed.Run(T(2), [Seed.Case("A")]),
            Seed.Run(T(2), [Seed.Case("A", "Failed")]),
            Seed.Run(T(3), [Seed.Case("A")], branch: "feature/x", environment: "Local"),
            Seed.Run(T(4), [Seed.Case("Z", "Failed")], project: "Api.Tests", runner: "xUnit"));
        _client = await _host.ConnectAsync();
    }

    [Test]
    public async Task ListTestRuns_NoFilter_ReturnsRunsNewestFirstWithColumns()
    {
        var result = await _client.CallAsync("list_test_runs", new { limit = 50 });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["test_run_id", "suite_id", "received_at", "branch", "environment", "platform", "os", "cpu_count", "total", "passed", "failed", "skipped", "total_ms"]);
        result.TotalRows.ShouldBe(6);
        result.Rows[0]["suite_id"].ShouldBe(Suite);
        result.Rows[0]["total"].ShouldBe("2");
        result.Rows[0]["passed"].ShouldBe("1");
        result.Rows[0]["failed"].ShouldBe("1");
        result.Rows[0]["total_ms"].ShouldBe("1234");
        result.Rows[^1]["suite_id"].ShouldBe(OtherSuite);
    }

    [Test]
    public async Task ListTestRuns_SuiteFilter_ReturnsOnlyThatSuite()
    {
        var result = await _client.CallAsync("list_test_runs", new { suite_id = OtherSuite });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0]["failed"].ShouldBe("1");
    }

    [Test]
    public async Task ListTestRuns_StatusFilter_SplitsFailedAndPassed()
    {
        var failed = await _client.CallAsync("list_test_runs", new { status = "failed" });
        var passed = await _client.CallAsync("list_test_runs", new { status = "passed", limit = 50 });

        failed.TotalRows.ShouldBe(3);
        passed.TotalRows.ShouldBe(3);
        passed.Rows.ShouldAllBe(r => r["failed"] == "0");
    }

    [Test]
    public async Task ListTestRuns_RunnerAndScopeFilters_Apply()
    {
        var runner = await _client.CallAsync("list_test_runs", new { test_runner = "xUnit" });
        var env = await _client.CallAsync("list_test_runs", new { environment = "local" });

        runner.TotalRows.ShouldBe(1);
        env.Rows.Single()["branch"].ShouldBe("feature/x");
    }

    [Test]
    public async Task ListTestRuns_KeysetCursorWithTiedTimestamps_ReturnsEveryRowExactlyOnce()
    {
        var all = await _client.CallAllPagesAsync("list_test_runs", new { limit = 2 });

        all.Count.ShouldBe(6);
        all.Select(r => r["test_run_id"]).Distinct().Count().ShouldBe(6);
        var times = all.Select(r => r["received_at"]).ToList();
        times.ShouldBe(times.OrderByDescending(t => t).ToList());
    }

    [Test]
    public async Task ListTestRuns_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("list_test_runs", new { limit = 2 });
        var first = await _client.CallAsync("list_test_runs");
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task ListTestRuns_Output_NeverContainsUserOrHost()
    {
        var result = await _client.CallAsync("list_test_runs");

        (result.Summary + result.Body).ShouldNotContain(Seed.USER_NAME);
        (result.Summary + result.Body).ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task ListTestRuns_BadStatus_ReturnsToolError()
    {
        var result = await _client.CallAsync("list_test_runs", new { status = "weird" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("status");
    }
}
