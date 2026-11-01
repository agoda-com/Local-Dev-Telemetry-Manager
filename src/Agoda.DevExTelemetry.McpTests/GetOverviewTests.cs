using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GetOverviewTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public GetOverviewTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task GetOverview_NoData_ReturnsColumnsAndNoRows()
    {
        var result = await _client.CallAsync("get_overview");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Columns.ShouldBe(["section", "rank", "subject_id", "repository", "project", "reason", "evidence"]);
        result.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task GetOverview_ZeroTop_ReturnsToolError()
    {
        var result = await _client.CallAsync("get_overview", new { top = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("top");
    }
}
