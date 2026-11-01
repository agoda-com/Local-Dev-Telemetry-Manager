using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetTestsTests
{
    private static readonly string[] Columns =
    [
        "test_key", "suite_id", "repository", "project", "test_runner", "class_name", "name", "runs", "passed", "failed",
        "skipped", "fail_rate", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms", "first_seen_at", "last_run_at",
        "last_status", "last_failed_at", "status_sequence"
    ];

    private static readonly string NUnitSuite = StableId.Suite("web-app", "Web.Tests", "NUnit");
    private static readonly string VitestSuite = StableId.Suite("web-app", "Web.Client", "Vitest");
    private static readonly string JUnitSuite = StableId.Suite("jvm-app", "Jvm.Tests", "JUnit");
    private static readonly string LongSuite = StableId.Suite("web-app", "Web.Long", "NUnit");

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetTestsTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "100" });
        var t = McpTestHost.Now.AddDays(-5);

        // NUnit suite. Tests: A (5 runs), B (always failed), C (fails last), D (skipped/pending, no class), E (slow, passes).
        // A in class AClass, B and C in BClass, E in CClass.
        const string a = "Web.Tests.AClass", b = "Web.Tests.BClass", c = "Web.Tests.CClass";
        var runs = new List<Core.Models.Entities.TestRun>
        {
            Seed.Run(t.AddDays(0), [Seed.Case("A", "Passed", 10, a), Seed.Case("D", "Skipped", null, null), Seed.Case("E", "Passed", 1000, c)]),
            Seed.Run(t.AddDays(1), [Seed.Case("A", "Passed", 20, a), Seed.Case("B", "Failed", 50, b), Seed.Case("E", "Passed", 2000, c)]),
            Seed.Run(t.AddDays(2), [Seed.Case("A", "Failed", 30, a), Seed.Case("B", "Failed", 50, b), Seed.Case("C", "Passed", 5, b)]),
            Seed.Run(t.AddDays(3), [Seed.Case("A", "Passed", 40, a), Seed.Case("C", "Passed", 5, b)]),
            Seed.Run(t.AddDays(4), [Seed.Case("A", "Passed", 100, a), Seed.Case("C", "Failed", 5, b), Seed.Case("D", "Pending", null, null)])
        };

        // Vitest: ClassName = file path, FullName = "file > describe > name".
        const string file = "src/login.test.ts";
        runs.Add(Seed.Run(t.AddDays(1), [
            Seed.Case("works", "Passed", 7, file, fullName: $"{file} > login > works"),
            Seed.Case("Shared", "Passed", 7, "Shared.Class", fullName: "Shared.Class.Shared")
        ], project: "Web.Client", runner: "Vitest"));

        // NUnit style in a second suite: FullName = Namespace.Class.Method, same name key as the first suite's "Shared".
        runs.Add(Seed.Run(t.AddDays(1), [
            Seed.Case("Shared", "Failed", 9, "Shared.Class", fullName: "Shared.Class.Shared")
        ], repository: "other-app", project: "Other.Tests", runner: "NUnit"));

        // JUnit: ClassName = com.x.Class, FullName null.
        runs.Add(Seed.Run(t.AddDays(2), [
            Seed.Case("test1", "Passed", 11, "com.x.Class"),
            Seed.Case("test1", "Failed", 12, "com.x.Class")
        ], repository: "jvm-app", project: "Jvm.Tests", runner: "JUnit"));

        // 35 runs of one test in its own suite: fails at index 5 and 34.
        for (var i = 0; i < 35; i++)
        {
            runs.Add(Seed.Run(McpTestHost.Now.AddHours(-(36 - i)),
                [Seed.Case("Long", i is 5 or 34 ? "Failed" : "Passed", 10 + i, "Web.Long.Cls")],
                project: "Web.Long"));
        }

        await _host.SeedAsync(runs.ToArray());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static CsvRow Row(ToolResult result, string name) => result.Rows.Single(r => r["name"] == name);

    private Task<ToolResult> NUnit(object? extra = null) => Call(NUnitSuite, extra);

    private async Task<ToolResult> Call(string suiteId, object? extra = null)
    {
        var args = new Dictionary<string, object?> { ["suite_id"] = suiteId, ["limit"] = 100 };
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties()) args[p.Name] = p.GetValue(extra);
        var result = ToolResult.Parse(await _client.CallToolAsync("get_tests", args));
        result.IsError.ShouldBeFalse(result.ErrorText);
        return result;
    }

    [Test]
    public async Task GetTests_Default_ReturnsColumnsInOrderAndTotalMsDescending()
    {
        var result = await NUnit();

        result.Columns.ShouldBe(Columns);
        result.Rows.Select(r => r["name"]).ShouldBe(["E", "A", "B", "C", "D"]);
        result.TotalRows.ShouldBe(5);
        var a = Row(result, "A");
        a["test_key"].ShouldBe($"{NUnitSuite}:Web.Tests.AClass.A");
        a["suite_id"].ShouldBe(NUnitSuite);
        a["repository"].ShouldBe("web-app");
        a["project"].ShouldBe("Web.Tests");
        a["test_runner"].ShouldBe("NUnit");
        a["class_name"].ShouldBe("Web.Tests.AClass");
        a["runs"].ShouldBe("5");
        a["passed"].ShouldBe("4");
        a["failed"].ShouldBe("1");
        a["skipped"].ShouldBe("0");
        a["fail_rate"].ShouldBe("0.2");
        a["avg_ms"].ShouldBe("40");
        a["max_ms"].ShouldBe("100");
        a["total_ms"].ShouldBe("200");
        a["last_status"].ShouldBe("Passed");
        DateTime.Parse(a["first_seen_at"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
            .ShouldBe(McpTestHost.Now.AddDays(-5));
        DateTime.Parse(a["last_failed_at"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal)
            .ShouldBe(McpTestHost.Now.AddDays(-3));
    }

    [Test]
    public async Task GetTests_PercentileColumns_MatchHandComputedValues()
    {
        var result = await NUnit();

        // A: 10,20,30,40,100 -> p50 30, p95 = 40 + 60 * 0.8 = 88
        Row(result, "A")["p50_ms"].ShouldBe("30");
        Row(result, "A")["p95_ms"].ShouldBe("88");
        // E: 1000, 2000 -> p50 1500, p95 = 1000 + 1000 * 0.95
        Row(result, "E")["p50_ms"].ShouldBe("1500");
        Row(result, "E")["p95_ms"].ShouldBe("1950");
        Row(result, "B")["p50_ms"].ShouldBe("50");
        Row(result, "D")["p50_ms"].ShouldBe("");
        Row(result, "D")["avg_ms"].ShouldBe("");
    }

    [Test]
    public async Task GetTests_StatusSequence_IsOldestFirstWithOneLetterPerResult()
    {
        var result = await NUnit();

        Row(result, "A")["status_sequence"].ShouldBe("PPFPP");
        Row(result, "B")["status_sequence"].ShouldBe("FF");
        Row(result, "C")["status_sequence"].ShouldBe("PPF");
        Row(result, "D")["status_sequence"].ShouldBe("S?");
        Row(result, "D")["last_status"].ShouldBe("Pending");
    }

    [Test]
    public async Task GetTests_MoreThan30Results_KeepsTheLast30OldestFirst()
    {
        var result = await Call(LongSuite);

        var row = result.Rows.Single();
        row["runs"].ShouldBe("35");
        row["status_sequence"].ShouldBe("F" + new string('P', 28) + "F");
    }

    [TestCase("has_failures", "A,B,C")]
    [TestCase("has_passes_and_failures", "A,C")]
    [TestCase("last_failed", "B,C")]
    [TestCase("always_failed", "B")]
    public async Task GetTests_StatusFilter_SelectsTheRightTests(string status, string expected)
    {
        var result = await NUnit(new { status, sort = "total_ms asc" });

        result.Rows.Select(r => r["name"]).OrderBy(n => n).ShouldBe(expected.Split(','));
    }

    [TestCase("runs")]
    [TestCase("passed")]
    [TestCase("failed")]
    [TestCase("skipped")]
    [TestCase("fail_rate")]
    [TestCase("avg_ms")]
    [TestCase("p50_ms")]
    [TestCase("p95_ms")]
    [TestCase("max_ms")]
    [TestCase("total_ms")]
    public async Task GetTests_SortColumn_OrdersBothDirections(string column)
    {
        double Value(CsvRow r) => r[column].Length == 0 ? -1 : double.Parse(r[column], CultureInfo.InvariantCulture);

        var desc = await NUnit(new { sort = $"{column} desc" });
        var asc = await NUnit(new { sort = $"{column} asc" });

        var d = desc.Rows.Select(Value).ToList();
        var a = asc.Rows.Select(Value).ToList();
        d.Distinct().Count().ShouldBeGreaterThan(1, "the fixture must have differing values");
        d.ShouldBe(d.OrderByDescending(v => v).ToList());
        a.ShouldBe(a.OrderBy(v => v).ToList());
        desc.Rows.Select(r => r["name"]).OrderBy(n => n).ShouldBe(asc.Rows.Select(r => r["name"]).OrderBy(n => n));
        desc.Rows[0]["name"].ShouldNotBe(asc.Rows[0]["name"]);
    }

    [Test]
    public async Task GetTests_SortByP95_OrdersByPercentileAndPagesWithCursor()
    {
        var first = await NUnit(new { sort = "p95_ms", limit = 2 });
        var all = await _client.CallAllPagesAsync("get_tests", new { suite_id = NUnitSuite, sort = "p95_ms", limit = 2 });

        first.Rows.Count.ShouldBe(2);
        first.Truncated.ShouldBeTrue();
        all.Select(r => r["name"]).ShouldBe(["E", "A", "B", "C", "D"]);
        all.Select(r => r["p95_ms"]).ShouldBe(["1950", "88", "50", "5", ""]);
    }

    [Test]
    public async Task GetTests_MinRuns_ExcludesTestsWithTooFewRuns()
    {
        var result = await NUnit(new { min_runs = 3 });

        result.Rows.Select(r => r["name"]).OrderBy(n => n).ShouldBe(["A", "C"]);
        result.TotalRows.ShouldBe(2);
    }

    [Test]
    public async Task GetTests_MinAvgMs_ExcludesFastTests()
    {
        var result = await NUnit(new { min_avg_ms = 45 });

        result.Rows.Select(r => r["name"]).OrderBy(n => n).ShouldBe(["B", "E"]);
    }

    [Test]
    public async Task GetTests_NameContains_IsCaseInsensitiveOnNameOrKey()
    {
        (await NUnit(new { name_contains = "e" })).Rows.Select(r => r["name"]).ShouldBe(["E"]);
        (await NUnit(new { name_contains = "CCLASS" })).Rows.Select(r => r["name"]).ShouldBe(["E"]);
    }

    [Test]
    public async Task GetTests_ClassContains_FiltersOnClass()
    {
        var result = await NUnit(new { class_contains = "bclass", sort = "runs asc" });

        result.Rows.Select(r => r["name"]).OrderBy(n => n).ShouldBe(["B", "C"]);
    }

    [Test]
    public async Task GetTests_TestRunnerWithoutSuite_SpansSuites()
    {
        var result = await _client.CallAsync("get_tests", new { test_runner = "Vitest", limit = 100 });

        result.Rows.Select(r => r["suite_id"]).Distinct().ShouldBe([VitestSuite]);
        result.Rows.Count.ShouldBe(2);
    }

    [Test]
    public async Task GetTests_GroupByClass_RollsUpTheSameColumns()
    {
        var result = await NUnit(new { group_by = "class", sort = "runs desc" });

        result.Columns.ShouldBe(Columns);
        result.Rows.Select(r => r["class_name"]).ShouldBe(["Web.Tests.AClass", "Web.Tests.BClass", "(none)", "Web.Tests.CClass"], ignoreOrder: true);
        result.Rows.ShouldAllBe(r => r["name"] == "");

        var b = result.Rows.Single(r => r["class_name"] == "Web.Tests.BClass");
        b["test_key"].ShouldBe($"{NUnitSuite}:Web.Tests.BClass");
        b["runs"].ShouldBe("5");
        b["passed"].ShouldBe("2");
        b["failed"].ShouldBe("3");
        b["total_ms"].ShouldBe("115");
        b["status_sequence"].ShouldBe("FFPF");
        b["last_status"].ShouldBe("Failed");

        var none = result.Rows.Single(r => r["class_name"] == "(none)");
        none["test_key"].ShouldBe($"{NUnitSuite}:(none)");
        none["runs"].ShouldBe("2");
        none["status_sequence"].ShouldBe("S?");
        result.Rows.Single(r => r["class_name"] == "Web.Tests.AClass")["p95_ms"].ShouldBe("88");
        result.Rows.Single(r => r["class_name"] == "Web.Tests.CClass")["status_sequence"].ShouldBe("PP");
    }

    [Test]
    public async Task GetTests_GroupByClassWithStatusFilter_AppliesToClassTotals()
    {
        var result = await NUnit(new { group_by = "class", status = "last_failed" });

        result.Rows.Select(r => r["class_name"]).ShouldBe(["Web.Tests.BClass"]);
    }

    [Test]
    public async Task GetTests_DifferentRunnersAndSuites_NeverMerge()
    {
        var result = await _client.CallAsync("get_tests", new { limit = 100, sort = "runs asc" });

        // Vitest: key is FullName, class is the file path.
        var vitest = result.Rows.Single(r => r["test_key"] == $"{VitestSuite}:src/login.test.ts > login > works");
        vitest["class_name"].ShouldBe("src/login.test.ts");
        vitest["name"].ShouldBe("works");

        // Same name key in two suites: two rows with different suite ids.
        var shared = result.Rows.Where(r => r["test_key"].EndsWith(":Shared.Class.Shared")).ToList();
        shared.Count.ShouldBe(2);
        shared.Select(r => r["suite_id"]).Distinct().Count().ShouldBe(2);
        shared.ShouldAllBe(r => r["runs"] == "1");

        // JUnit: ClassName.Name, two executions in one run are one test.
        var junit = result.Rows.Single(r => r["test_key"] == $"{JUnitSuite}:com.x.Class.test1");
        junit["runs"].ShouldBe("2");
        junit["passed"].ShouldBe("1");
        junit["failed"].ShouldBe("1");

        result.Rows.Select(r => r["test_key"]).Distinct().Count().ShouldBe(result.Rows.Count);
        result.Rows.Select(r => r["suite_id"]).Distinct().Count().ShouldBe(5);
    }

    [Test]
    public async Task GetTests_Window_ExcludesOlderExecutions()
    {
        var result = await _client.CallAsync("get_tests", new { suite_id = NUnitSuite, days = 3, limit = 100 });

        // Only runs at -2 and -1 days (t+3, t+4).
        Row(result, "A")["runs"].ShouldBe("2");
        result.Rows.Select(r => r["name"]).ShouldNotContain("E");
    }

    [Test]
    public async Task GetTests_FollowingCursors_ReturnsEveryRowExactlyOnce()
    {
        var first = await _client.CallAsync("get_tests", new { limit = 2 });
        var all = await _client.CallAllPagesAsync("get_tests", new { limit = 2 });

        first.Rows.Count.ShouldBe(2);
        all.Count.ShouldBe(first.TotalRows);
        all.Select(r => r["test_key"]).Distinct().Count().ShouldBe(all.Count);
    }

    [Test]
    public async Task GetTests_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_tests", new { sort = "failed desc", limit = 2 });
        var first = await _client.CallAsync("get_tests", new { sort = "failed desc", limit = 2 });
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(Columns);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task GetTests_Output_NeverContainsUserOrHostname()
    {
        var result = await NUnit(new { format = "json" });
        var csv = await NUnit();

        result.Body.ShouldNotContain(Seed.USER_NAME);
        result.Body.ShouldNotContain(Seed.HOSTNAME);
        csv.Body.ShouldNotContain(Seed.USER_NAME);
        csv.Body.ShouldNotContain(Seed.HOSTNAME);
    }

    [TestCase("status", "bogus")]
    [TestCase("sort", "bogus")]
    [TestCase("group_by", "user")]
    [TestCase("suite_id", "deadbeef")]
    [TestCase("min_runs", "many")]
    [TestCase("cursor", "not-a-cursor")]
    public async Task GetTests_BadArgument_ReturnsToolErrorNamingIt(string name, string value)
    {
        var result = ToolResult.Parse(await _client.CallToolAsync("get_tests", new Dictionary<string, object?> { [name] = value }));

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain(name);
    }
}
