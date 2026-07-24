using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class KtorIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public KtorIngestTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory(_provider, enableRawPayloadStorage: true);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestCase(".KtorStartup")]
    [TestCase(".KtorResponse")]
    public async Task POST_Ktor_WithSupportedType_QueuesCompleteBuildMetric(string type)
    {
        var payload = TestFixtures.CreateKtorPayload(type);
        var response = await TestFixtures.PostJsonAsync(_client, "/ktor", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.MetricType.ShouldBe(type);
        metric.TimeTakenMs.ShouldBe(2431);
        metric.BuildCategory.ShouldBe("API");
        metric.ReloadType.ShouldBeNull();
        metric.UserName.ShouldBe("developer");
        metric.CpuCount.ShouldBe(12);
        metric.Hostname.ShouldBe("workstation");
        metric.Platform.ShouldBe("Linux");
        metric.Os.ShouldBe("Linux 6.8.0 amd64");
        metric.Branch.ShouldBe("feature/startup-metrics");
        metric.ProjectName.ShouldBe("sample-service");
        metric.Repository.ShouldBe("https://example.com/group/sample-service.git");
        metric.RepositoryName.ShouldBe("sample-service");
        metric.ToolVersion.ShouldBe("0.1.0");
        metric.CommitSha.ShouldBe("6f9c4bd4a2d0f5cfa3fa8e199cc0c630f56a9898");
        metric.IsDebuggerAttached.ShouldBeFalse();
        metric.ExecutionEnvironment.ShouldBe("Local");
        metric.SourceEndpoint.ShouldBe("/ktor");

        using var extraData = JsonDocument.Parse(metric.ExtraData!);
        extraData.RootElement.GetProperty("date").GetString()
            .ShouldBe("2026-07-23T07:30:00Z");
        extraData.RootElement.GetProperty("tags").GetProperty("team").GetString()
            .ShouldBe("supply");

        var rawPayload = await db.RawPayloads.SingleAsync();
        rawPayload.Endpoint.ShouldBe("/ktor");
        rawPayload.PayloadJson.ShouldContain($"\"type\":\"{type}\"");
        rawPayload.PayloadJson.ShouldContain("\"tags\"");
    }

    [Test]
    public async Task POST_Ktor_WithNullableGitFieldsAndEmptyTags_AcceptsPayload()
    {
        var payload = TestFixtures.CreateKtorPayload(
            branch: null,
            commitSha: null,
            repository: null,
            repositoryName: null,
            tags: new Dictionary<string, string>());

        var response = await TestFixtures.PostJsonAsync(_client, "/ktor", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();
        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Branch.ShouldBeEmpty();
        metric.CommitSha.ShouldBeNull();
        metric.Repository.ShouldBeEmpty();
        metric.RepositoryName.ShouldBeEmpty();

        using var extraData = JsonDocument.Parse(metric.ExtraData!);
        extraData.RootElement.GetProperty("tags").GetRawText().ShouldBe("{}");
    }

    [TestCase(".AspNetStartup", "2431")]
    [TestCase("KtorStartup", "2431")]
    [TestCase(".KtorStartup", null)]
    [TestCase(".KtorStartup", "")]
    [TestCase(".KtorStartup", "invalid")]
    [TestCase(".KtorStartup", "NaN")]
    [TestCase(".KtorStartup", "Infinity")]
    [TestCase(".KtorStartup", "-1")]
    public async Task POST_Ktor_WithInvalidContract_ReturnsBadRequestWithoutPersisting(
        string type, string? timeTaken)
    {
        var payload = TestFixtures.CreateKtorPayload(type, timeTaken);

        var response = await TestFixtures.PostJsonAsync(_client, "/ktor", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.DrainBackgroundQueuesAsync();
        using var db = _factory.CreateDbContext();
        (await db.BuildMetrics.CountAsync()).ShouldBe(0);
        (await db.RawPayloads.CountAsync()).ShouldBe(0);
    }
}
