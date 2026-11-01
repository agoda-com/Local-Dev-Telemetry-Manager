using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetTestTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetTestTests(DatabaseProvider provider) => _provider = provider;

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static readonly string Suite = StableId.Suite("web-app", "Web.Tests", "NUnit");
    private static readonly string KeyA = TestKey.Format(Suite, "Web.Tests.LoginTests.A");
    private static DateTime T(double hoursAgo) => McpTestHost.Now.AddHours(-hoursAgo);
    private List<string> _tiedRunIds = null!;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        var tied = new[]
        {
            Seed.Run(T(24), [Seed.Case("A", "Passed", 10)]),
            Seed.Run(T(24), [Seed.Case("A", "Failed", 20, error: "boom\nsecond line")]),
            Seed.Run(T(24), [Seed.Case("A", "Passed", 30)]),
            Seed.Run(T(24), [Seed.Case("A", "Passed", 40)])
        };
        _tiedRunIds = tied.Select(r => r.Id).ToList();
        await _host.SeedAsync(
            [.. tied,
             Seed.Run(T(1), [Seed.Case("A", "Passed", 5), Seed.Case("B", "Failed", 7)], totalMs: 999),
             Seed.Run(T(48), [Seed.Case("A", "Failed", 50)], environment: "Local", branch: "feature/x", platform: "Windows", os: "Win11", cpuCount: 4),
             Seed.Run(T(2), [Seed.Case("Pay", className: "Web.Tests.Checkout"), Seed.Case("Refund", className: "Web.Tests.Checkout")])]);
        _client = await _host.ConnectAsync();
    }

    [Test]
    public async Task GetTest_TestKey_ReturnsExecutionsNewestFirstWithColumns()
    {
        var result = await _client.CallAsync("get_test", new { test_key = KeyA, limit = 50 });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["received_at", "test_run_id", "status", "duration_ms", "branch", "environment", "platform", "os", "cpu_count", "run_total_ms", "error"]);
        result.TotalRows.ShouldBe(6);
        result.Rows[0]["received_at"].ShouldBe("2026-03-18T11:00:00Z");
        result.Rows[0]["duration_ms"].ShouldBe("5");
        result.Rows[0]["run_total_ms"].ShouldBe("999");
        result.Rows[0]["environment"].ShouldBe("CI");
        var times = result.Rows.Select(r => r["received_at"]).ToList();
        times.ShouldBe(times.OrderByDescending(t => t).ToList());
        var last = result.Rows[^1];
        last["branch"].ShouldBe("feature/x");
        last["environment"].ShouldBe("Local");
        last["os"].ShouldBe("Win11");
        last["cpu_count"].ShouldBe("4");
        result.Rows.Single(r => r["status"] == "Failed" && r["branch"] == "main")["error"].ShouldBe("boom");
    }

    [Test]
    public async Task GetTest_StatusFilter_ReturnsOnlyThatStatus()
    {
        var result = await _client.CallAsync("get_test", new { test_key = KeyA, status = "failed" });

        result.Rows.Count.ShouldBe(2);
        result.Rows.ShouldAllBe(r => r["status"] == "Failed");
    }

    [Test]
    public async Task GetTest_SuiteAndUniqueNameContains_ReturnsHistory()
    {
        var result = await _client.CallAsync("get_test", new { suite_id = Suite, name_contains = "loginTESTS.a" });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldContain("test_run_id");
        result.TotalRows.ShouldBe(6);
    }

    [Test]
    public async Task GetTest_AmbiguousNameContains_ReturnsCandidatesAndNote()
    {
        var result = await _client.CallAsync("get_test", new { suite_id = Suite, name_contains = "Checkout" });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["test_key", "class_name", "name", "runs", "last_run_at"]);
        result.Rows.Select(r => r["test_key"]).ShouldBe(
            [TestKey.Format(Suite, "Web.Tests.Checkout.Pay"), TestKey.Format(Suite, "Web.Tests.Checkout.Refund")], ignoreOrder: true);
        result.Rows[0]["runs"].ShouldBe("1");
        result.Summary.ShouldContain("2 tests match name_contains; call get_test again with one test_key");
    }

    [Test]
    public async Task GetTest_NoNameMatch_ReturnsNoRowsWithNote()
    {
        var result = await _client.CallAsync("get_test", new { suite_id = Suite, name_contains = "nothing-like-this" });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Rows.ShouldBeEmpty();
        result.Summary.ShouldContain("no test matches");
    }

    [Test]
    public async Task GetTest_NoIdentification_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test");

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("test_key");
    }

    [Test]
    public async Task GetTest_UnknownSuite_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test", new { test_key = "deadbeef:Some.Test" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("test_key");
    }

    [Test]
    public async Task GetTest_BadStatus_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_test", new { test_key = KeyA, status = "weird" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("status");
    }

    [Test]
    public async Task GetTest_KeysetCursorWithTiedTimestamps_ReturnsEveryRowExactlyOnce()
    {
        var all = await _client.CallAllPagesAsync("get_test", new { test_key = KeyA, limit = 2 });

        all.Count.ShouldBe(6);
        all.Select(r => r["test_run_id"]).Distinct().Count().ShouldBe(6);
        _tiedRunIds.ShouldAllBe(id => all.Any(r => r["test_run_id"] == id));
    }

    [Test]
    public async Task GetTest_FirstPage_IsTruncatedWithCursor()
    {
        var result = await _client.CallAsync("get_test", new { test_key = KeyA, limit = 3 });

        result.Rows.Count.ShouldBe(3);
        result.Truncated.ShouldBeTrue();
        result.NextCursor.ShouldNotBeNull();
        result.TotalRows.ShouldBe(6);
    }

    [Test]
    public async Task GetTest_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_test", new { test_key = KeyA, limit = 2 });
        var first = await _client.CallAsync("get_test", new { test_key = KeyA, limit = 3 });
        using var http = _host.CreateClient();

        var (_, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task GetTest_Output_NeverContainsUserOrHost()
    {
        var result = await _client.CallAsync("get_test", new { test_key = KeyA, limit = 3 });

        (result.Summary + result.Body).ShouldNotContain(Seed.USER_NAME);
        (result.Summary + result.Body).ShouldNotContain(Seed.HOSTNAME);
    }
}
