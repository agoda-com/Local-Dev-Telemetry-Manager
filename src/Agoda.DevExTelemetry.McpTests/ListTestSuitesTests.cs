using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ListTestSuitesTests
{
    private static readonly string[] Columns =
    [
        "suite_id", "repository", "project", "test_runner", "runs", "ci_runs", "local_runs", "last_run_at",
        "tests_in_last_run", "avg_run_ms", "p95_run_ms", "failed_runs", "distinct_tests", "branches"
    ];

    private static readonly string NUnitSuite = StableId.Suite("web-app", "Web.Tests", "NUnit");
    private static readonly string VitestSuite = StableId.Suite("web-app", "Web.Client", "Vitest");

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public ListTestSuitesTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "10" });
        var now = McpTestHost.Now;
        await _host.SeedAsync(
            Seed.Run(now.AddDays(-3), [Seed.Case("A"), Seed.Case("B", "Failed"), Seed.Case("C")], environment: "CI", totalMs: 1000),
            Seed.Run(now.AddDays(-2), [Seed.Case("A"), Seed.Case("B"), Seed.Case("C"), Seed.Case("D"), Seed.Case("E")], environment: "CI", totalMs: 2000),
            Seed.Run(now.AddDays(-1), [Seed.Case("A"), Seed.Case("B", "Failed"), Seed.Case("C"), Seed.Case("D")],
                environment: "Local", branch: "feature/x", totalMs: 3000),
            Seed.Run(now.AddDays(-40), [Seed.Case("Old1"), Seed.Case("Old2")], environment: "CI", totalMs: 9000),
            Seed.Run(now.AddDays(-1), [Seed.Case("W", className: "src/a.test.ts", fullName: "src/a.test.ts > w")],
                project: "Web.Client", runner: "Vitest", environment: "Local", totalMs: 500));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task ListTestSuites_Default_ReturnsColumnsAndCountsPerSuite()
    {
        var result = await _client.CallAsync("list_test_suites", new { limit = 10 });

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(Columns);
        result.Rows.Select(r => r["suite_id"]).ShouldBe([NUnitSuite, VitestSuite]);

        var nunit = result.Rows[0];
        nunit["repository"].ShouldBe("web-app");
        nunit["project"].ShouldBe("Web.Tests");
        nunit["test_runner"].ShouldBe("NUnit");
        nunit["runs"].ShouldBe("3");
        nunit["ci_runs"].ShouldBe("2");
        nunit["local_runs"].ShouldBe("1");
        DateTime.Parse(nunit["last_run_at"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
            .ShouldBe(McpTestHost.Now.AddDays(-1));
        nunit["tests_in_last_run"].ShouldBe("4");
        nunit["avg_run_ms"].ShouldBe("2000");
        nunit["p95_run_ms"].ShouldBe("2900");
        nunit["failed_runs"].ShouldBe("2");
        nunit["distinct_tests"].ShouldBe("5");
        nunit["branches"].ShouldBe("2");
    }

    [Test]
    public async Task ListTestSuites_RunOutsideWindow_IsExcluded()
    {
        var narrow = await _client.CallAsync("list_test_suites", new { days = 14, limit = 10 });
        var wide = await _client.CallAsync("list_test_suites", new { days = 60, limit = 10 });

        narrow.Rows[0]["runs"].ShouldBe("3");
        narrow.Rows[0]["distinct_tests"].ShouldBe("5");
        wide.Rows.Single(r => r["suite_id"] == NUnitSuite)["runs"].ShouldBe("4");
        wide.Rows.Single(r => r["suite_id"] == NUnitSuite)["distinct_tests"].ShouldBe("7");
    }

    [Test]
    public async Task ListTestSuites_EnvironmentFilter_CountsOnlyThatEnvironment()
    {
        var ci = await _client.CallAsync("list_test_suites", new { environment = "ci", limit = 10 });
        var local = await _client.CallAsync("list_test_suites", new { environment = "local", limit = 10 });

        ci.Rows.Select(r => r["suite_id"]).ShouldBe([NUnitSuite]);
        ci.Rows[0]["runs"].ShouldBe("2");
        ci.Rows[0]["local_runs"].ShouldBe("0");
        ci.Rows[0]["tests_in_last_run"].ShouldBe("5");
        ci.Rows[0]["branches"].ShouldBe("1");
        local.Rows.Select(r => r["suite_id"]).ShouldBe([NUnitSuite, VitestSuite]);
        local.Rows[0]["ci_runs"].ShouldBe("0");
    }

    [Test]
    public async Task ListTestSuites_TestRunnerFilter_ReturnsThatRunner()
    {
        var result = await _client.CallAsync("list_test_suites", new { test_runner = "Vitest" });

        result.Rows.Select(r => r["suite_id"]).ShouldBe([VitestSuite]);
        result.Rows[0]["tests_in_last_run"].ShouldBe("1");
    }

    [Test]
    public async Task ListTestSuites_SortByColumn_OrdersAndBreaksTiesBySuiteId()
    {
        var byRuns = await _client.CallAsync("list_test_suites", new { sort = "runs asc", limit = 10 });
        var byTests = await _client.CallAsync("list_test_suites", new { sort = "distinct_tests asc", limit = 10 });
        var byBranches = await _client.CallAsync("list_test_suites", new { sort = "-branches", limit = 10 });

        byRuns.Rows.Select(r => r["suite_id"]).ShouldBe([VitestSuite, NUnitSuite]);
        byTests.Rows.Select(r => r["suite_id"]).ShouldBe([VitestSuite, NUnitSuite]);
        byBranches.Rows.Select(r => r["suite_id"]).ShouldBe([NUnitSuite, VitestSuite]);

        // Equal values tie-break by suite id.
        var tie = await _client.CallAsync("list_test_suites", new { sort = "tests_in_last_run asc", days = 14, limit = 10, environment = "local" });
        tie.Rows.Count.ShouldBe(2);
        var tied = await _client.CallAsync("list_test_suites", new { sort = "ci_runs asc", environment = "local", limit = 10 });
        tied.Rows.Select(r => r["suite_id"]).ShouldBe(new[] { NUnitSuite, VitestSuite }.OrderBy(s => s, StringComparer.Ordinal));
    }

    [Test]
    public async Task ListTestSuites_FollowingCursors_ReturnsEverySuiteOnce()
    {
        var first = await _client.CallAsync("list_test_suites", new { limit = 1 });
        var all = await _client.CallAllPagesAsync("list_test_suites", new { limit = 1 });

        first.Rows.Count.ShouldBe(1);
        first.Truncated.ShouldBeTrue();
        all.Select(r => r["suite_id"]).ShouldBe([NUnitSuite, VitestSuite]);
    }

    [Test]
    public async Task ListTestSuites_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("list_test_suites", new { limit = 1 });
        var first = await _client.CallAsync("list_test_suites", new { limit = 1 });
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task ListTestSuites_Output_NeverContainsUserOrHostname()
    {
        var csv = await _client.CallAsync("list_test_suites", new { limit = 10 });
        var json = await _client.CallAsync("list_test_suites", new { limit = 10, format = "json" });

        csv.Body.ShouldNotContain(Seed.USER_NAME);
        csv.Body.ShouldNotContain(Seed.HOSTNAME);
        json.Body.ShouldNotContain(Seed.USER_NAME);
        json.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [TestCase("sort", "bogus")]
    [TestCase("environment", "prod")]
    [TestCase("cursor", "not-a-cursor")]
    public async Task ListTestSuites_BadArgument_ReturnsToolErrorNamingIt(string name, string value)
    {
        var result = ToolResult.Parse(await _client.CallToolAsync("list_test_suites", new Dictionary<string, object?> { [name] = value }));

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain(name);
    }
}
