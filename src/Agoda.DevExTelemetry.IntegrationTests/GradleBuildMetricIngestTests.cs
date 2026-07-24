using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class GradleBuildMetricIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public GradleBuildMetricIngestTests(DatabaseProvider provider) => _provider = provider;

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

    [Test]
    public async Task POST_Gradle_WithFullContract_QueuesCompleteBuildMetric()
    {
        var payload = TestFixtures.CreateGradleBuildMetricPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/gradle", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.MetricType.ShouldBe("Gradle");
        metric.TimeTakenMs.ShouldBe(1842);
        metric.BuildCategory.ShouldBe("API");
        metric.ReloadType.ShouldBeNull();
        metric.UserName.ShouldBe("developer");
        metric.CpuCount.ShouldBe(12);
        metric.Hostname.ShouldBe("workstation");
        metric.Platform.ShouldBe("Linux");
        metric.Os.ShouldBe("Linux 6.8.0 amd64");
        metric.Branch.ShouldBe("feature/build-metrics");
        metric.ProjectName.ShouldBe("sample-service");
        metric.Repository.ShouldBe("https://example.com/group/sample-service.git");
        metric.RepositoryName.ShouldBe("sample-service");
        metric.ToolVersion.ShouldBe("0.1.0");
        metric.CommitSha.ShouldBe("6f9c4bd4a2d0f5cfa3fa8e199cc0c630f56a9898");
        metric.IsDebuggerAttached.ShouldBeFalse();
        metric.ExecutionEnvironment.ShouldBe("Local");
        metric.SourceEndpoint.ShouldBe("/gradle");

        using var extraData = JsonDocument.Parse(metric.ExtraData!);
        extraData.RootElement.GetProperty("date").GetString()
            .ShouldBe("2026-07-23T07:30:00Z");
        extraData.RootElement.GetProperty("ide").GetString().ShouldBe("IntelliJ IDEA");
        extraData.RootElement.GetProperty("buildKind").GetString().ShouldBe("incremental");
        extraData.RootElement.GetProperty("requestedTasks")[0].GetString().ShouldBe("build");
        extraData.RootElement.GetProperty("taskCount").GetInt32().ShouldBe(42);
        extraData.RootElement.GetProperty("executedTaskCount").GetInt32().ShouldBe(14);
        extraData.RootElement.GetProperty("upToDateTaskCount").GetInt32().ShouldBe(25);
        extraData.RootElement.GetProperty("fromCacheTaskCount").GetInt32().ShouldBe(3);
        extraData.RootElement.GetProperty("failedTaskCount").GetInt32().ShouldBe(0);
        extraData.RootElement.GetProperty("compileTaskCount").GetInt32().ShouldBe(8);
        extraData.RootElement.GetProperty("compileTimeMs").GetInt64().ShouldBe(913);
        extraData.RootElement.GetProperty("taskTimeMs").GetInt64().ShouldBe(3276);
        extraData.RootElement.GetProperty("projects")[1]
            .GetProperty("projectPath").GetString().ShouldBe(":api");

        var rawPayload = await db.RawPayloads.SingleAsync();
        rawPayload.Endpoint.ShouldBe("/gradle");
        rawPayload.PayloadJson.ShouldContain("\"projects\"");
        rawPayload.PayloadJson.ShouldContain("\"compileTimeMs\":533");
    }

    [Test]
    public async Task POST_Gradle_WithNullableGitFields_AcceptsPayload()
    {
        var payload = TestFixtures.CreateGradleBuildMetricPayload(
            branch: null, commitSha: null, repository: null, repositoryName: null);

        var response = await TestFixtures.PostJsonAsync(_client, "/gradle", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();
        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Branch.ShouldBeEmpty();
        metric.CommitSha.ShouldBeNull();
        metric.Repository.ShouldBeEmpty();
        metric.RepositoryName.ShouldBeEmpty();
    }

    [TestCase("NotGradle", "1842")]
    [TestCase("gradle", "1842")]
    [TestCase("Gradle", null)]
    [TestCase("Gradle", "")]
    [TestCase("Gradle", "1.5")]
    [TestCase("Gradle", "-1")]
    [TestCase("Gradle", "9223372036854775808")]
    public async Task POST_Gradle_WithInvalidContract_ReturnsBadRequestWithoutPersisting(
        string type, string? timeTaken)
    {
        var payload = TestFixtures.CreateGradleBuildMetricPayload(type, timeTaken);

        var response = await TestFixtures.PostJsonAsync(_client, "/gradle", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.DrainBackgroundQueuesAsync();
        using var db = _factory.CreateDbContext();
        (await db.BuildMetrics.CountAsync()).ShouldBe(0);
        (await db.RawPayloads.CountAsync()).ShouldBe(0);
    }
}
