using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetTestFailuresTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetTestFailuresTests(DatabaseProvider provider) => _provider = provider;

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
            Seed.Run(T(5), [Seed.Case("A", "Failed", error: "Timeout waiting for Element"), Seed.Case("B", "Passed")]),
            Seed.Run(T(5), [Seed.Case("A", "Failed", error: "NullReference in Login"), Seed.Case("C", "Failed", className: "Web.Tests.Cart", error: "Expected 3 but was 4")]),
            Seed.Run(T(5), [Seed.Case("A", "Failed", error: "timeout again")], branch: "feature/x", environment: "Local"),
            Seed.Run(T(30), [Seed.Case("A", "Failed", error: "Timeout waiting for Element")]),
            Seed.Run(T(31), [Seed.Case("Z", "Failed", error: "other suite")], project: "Api.Tests", runner: "xUnit"));
        _client = await _host.ConnectAsync();
    }

    [Test]
    public async Task GetTestFailures_NoFilter_ReturnsFailedRowsNewestFirstWithColumns()
    {
        var result = await _client.CallAsync("get_test_failures", new { limit = 50 });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["received_at", "test_key", "class_name", "name", "branch", "environment", "test_run_id", "error"]);
        result.TotalRows.ShouldBe(6);
        result.Rows.ShouldAllBe(r => r["error"].Length > 0);
        var times = result.Rows.Select(r => r["received_at"]).ToList();
        times.ShouldBe(times.OrderByDescending(t => t).ToList());
        result.Rows[0]["test_key"].ShouldStartWith(Suite + ":");
    }

    [Test]
    public async Task GetTestFailures_SuiteFilter_ExcludesOtherSuites()
    {
        var result = await _client.CallAsync("get_test_failures", new { suite_id = OtherSuite });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0]["name"].ShouldBe("Z");
    }

    [Test]
    public async Task GetTestFailures_ErrorContains_IsCaseInsensitive()
    {
        var result = await _client.CallAsync("get_test_failures", new { error_contains = "TIMEOUT", limit = 50 });

        result.TotalRows.ShouldBe(3);
        result.Rows.ShouldAllBe(r => r["error"].ToLowerInvariant().Contains("timeout"));
    }

    [Test]
    public async Task GetTestFailures_ClassAndNameContains_Filter()
    {
        var byClass = await _client.CallAsync("get_test_failures", new { class_contains = "cart" });
        var byName = await _client.CallAsync("get_test_failures", new { name_contains = "Cart.C" });

        byClass.Rows.Select(r => r["name"]).ShouldBe(["C"]);
        byName.Rows.Select(r => r["name"]).ShouldBe(["C"]);
    }

    [Test]
    public async Task GetTestFailures_TestKey_ReturnsOnlyThatTest()
    {
        var result = await _client.CallAsync("get_test_failures", new { test_key = TestKey.Format(Suite, "Web.Tests.LoginTests.A"), limit = 50 });

        result.TotalRows.ShouldBe(4);
        result.Rows.ShouldAllBe(r => r["name"] == "A");
    }

    [Test]
    public async Task GetTestFailures_ScopeFilters_Apply()
    {
        var byEnv = await _client.CallAsync("get_test_failures", new { environment = "local" });
        var byBranch = await _client.CallAsync("get_test_failures", new { branch = "feature/x" });
        var byWindow = await _client.CallAsync("get_test_failures", new { days = 1 });

        byEnv.Rows.Select(r => r["error"]).ShouldBe(["timeout again"]);
        byBranch.TotalRows.ShouldBe(1);
        byWindow.TotalRows.ShouldBe(4);
    }

    [Test]
    public async Task GetTestFailures_KeysetCursorWithTiedTimestamps_ReturnsEveryRowExactlyOnce()
    {
        var all = await _client.CallAllPagesAsync("get_test_failures", new { limit = 2 });

        all.Count.ShouldBe(6);
        all.Select(r => r["test_run_id"] + "|" + r["test_key"]).Distinct().Count().ShouldBe(6);
    }

    [Test]
    public async Task GetTestFailures_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_test_failures", new { error_contains = "a" });
        var first = await _client.CallAsync("get_test_failures", new { error_contains = "a" });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task GetTestFailures_Output_NeverContainsUserOrHost()
    {
        var result = await _client.CallAsync("get_test_failures");

        (result.Summary + result.Body).ShouldNotContain(Seed.USER_NAME);
        (result.Summary + result.Body).ShouldNotContain(Seed.HOSTNAME);
    }

    [Test]
    public async Task GetTestFailures_UnknownSuite_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test_failures", new { suite_id = "deadbeef" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("suite_id");
    }
}
