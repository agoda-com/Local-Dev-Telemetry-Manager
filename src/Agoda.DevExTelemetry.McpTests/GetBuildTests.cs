using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetBuildTests
{
    private const string GRADLE_EXTRA =
        """{"RequestedTasks":["assemble","test"],"CacheRatio":0.85,"Daemon":{"Reused":true,"Jvm":{"Version":"21"}},"Failed":false,"Note":null,"Count":3}""";

    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetBuildTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "5" });
        await _host.SeedAsync(
            Seed.Build(McpTestHost.Now.AddDays(-1), 1234, id: "gradle1", metricType: "Gradle", extraData: GRADLE_EXTRA, commitSha: "abc"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 10, id: "bad1", extraData: "this is { not json"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 10, id: "long1", extraData: new string('x', 3000)),
            Seed.Build(McpTestHost.Now.AddDays(-1), 10, id: "arr1", extraData: "[1,2]"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 10, id: "none1"),
            Seed.Build(McpTestHost.Now.AddDays(-400), 77, id: "old1"));
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    private static Dictionary<string, string> Fields(IEnumerable<CsvRow> rows) => rows.ToDictionary(r => r["field"], r => r["value"]);

    [Test]
    public async Task GetBuild_GradlePayload_FlattensExtraData()
    {
        var rows = await _client.CallAllPagesAsync("get_build", new { id = "gradle1" });
        var fields = Fields(rows);

        rows[0]["field"].ShouldBe("id");
        fields["time_ms"].ShouldBe("1234");
        fields["metric_type"].ShouldBe("Gradle");
        fields["commit_sha"].ShouldBe("abc");
        fields["extra.RequestedTasks[0]"].ShouldBe("assemble");
        fields["extra.RequestedTasks[1]"].ShouldBe("test");
        fields["extra.CacheRatio"].ShouldBe("0.85");
        fields["extra.Daemon.Reused"].ShouldBe("true");
        fields["extra.Daemon.Jvm.Version"].ShouldBe("21");
        fields["extra.Failed"].ShouldBe("false");
        fields["extra.Note"].ShouldBe("");
        fields["extra.Count"].ShouldBe("3");
    }

    [Test]
    public async Task GetBuild_InvalidJson_IsOneExtraRowWithRawText()
    {
        var fields = Fields(await _client.CallAllPagesAsync("get_build", new { id = "bad1" }));

        fields["extra"].ShouldBe("this is { not json");
        fields.Keys.Count(k => k.StartsWith("extra")).ShouldBe(1);
    }

    [Test]
    public async Task GetBuild_LongInvalidJson_IsCutTo2000Characters()
    {
        var fields = Fields(await _client.CallAllPagesAsync("get_build", new { id = "long1" }));

        fields["extra"].Length.ShouldBe(2000);
    }

    [Test]
    public async Task GetBuild_NonObjectJson_IsOneExtraRowWithRawText()
    {
        var fields = Fields(await _client.CallAllPagesAsync("get_build", new { id = "arr1" }));

        fields["extra"].ShouldBe("[1,2]");
    }

    [Test]
    public async Task GetBuild_NoExtraData_HasOnlyTheBuildFields()
    {
        var rows = await _client.CallAllPagesAsync("get_build", new { id = "none1" });

        rows.Count.ShouldBe(16);
        rows.Select(r => r["field"]).ShouldNotContain(f => f.StartsWith("extra"));
    }

    [Test]
    public async Task GetBuild_OlderThanRetention_IsStillFound()
    {
        var fields = Fields(await _client.CallAllPagesAsync("get_build", new { id = "old1" }));

        fields["time_ms"].ShouldBe("77");
    }

    [Test]
    public async Task GetBuild_UnknownId_ReturnsToolErrorNamingId()
    {
        var result = await _client.CallAsync("get_build", new { id = "nope" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("id");
    }

    [Test]
    public async Task GetBuild_FollowingCursors_ReturnsEveryFieldExactlyOnce()
    {
        var first = await _client.CallAsync("get_build", new { id = "gradle1" });
        var all = await _client.CallAllPagesAsync("get_build", new { id = "gradle1" });

        first.Rows.Count.ShouldBe(5);
        first.NextCursor.ShouldNotBeNull();
        all.Count.ShouldBe(first.TotalRows);
        all.Select(r => r["field"]).Distinct().Count().ShouldBe(all.Count);
    }

    [Test]
    public async Task GetBuild_Export_ReturnsSameRowsAsTool()
    {
        var toolRows = await _client.CallAllPagesAsync("get_build", new { id = "gradle1" });
        var first = await _client.CallAsync("get_build", new { id = "gradle1" });
        using var http = _host.CreateClient();

        var (columns, exportRows) = await http.GetCsvAsync(first.ExportUrl);

        columns.ShouldBe(["field", "value"]);
        exportRows.Select(r => r.ToString()).ShouldBe(toolRows.Select(r => r.ToString()));
    }

    [Test]
    public async Task GetBuild_Body_NeverContainsUserNameOrHostname()
    {
        var all = await _client.CallAllPagesAsync("get_build", new { id = "gradle1" });

        all.ShouldNotContain(r => r["value"].Contains(Seed.USER_NAME) || r["value"].Contains(Seed.HOSTNAME));
        all.Select(r => r["field"]).ShouldNotContain("user_name");
        all.Select(r => r["field"]).ShouldNotContain("hostname");
    }
}
