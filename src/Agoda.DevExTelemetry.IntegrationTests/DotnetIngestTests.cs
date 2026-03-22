using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class DotnetIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public DotnetIngestTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory(_provider);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task POST_Dotnet_Build_CreatesBuildMetric_WithApiCategory()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(type: ".Net");
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.BuildCategory.ShouldBe("API");
        metric.MetricType.ShouldBe(".Net");
    }

    [Test]
    public async Task POST_Dotnet_AspNetStartup_CreatesCorrectMetricType()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(type: ".AspNetStartup");
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.MetricType.ShouldBe(".AspNetStartup");
    }

    [Test]
    public async Task POST_Dotnet_AspNetResponse_CreatesCorrectMetricType()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(type: ".AspNetResponse");
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.MetricType.ShouldBe(".AspNetResponse");
    }

    [Test]
    public async Task POST_Dotnet_ParsesTimeTakenString()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(timeTaken: "1234");
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.TimeTakenMs.ShouldBe(1234.0);
    }

    [Test]
    public async Task POST_Dotnet_ConvertsPlatformIntToString()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(platform: 4);
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.Platform.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task POST_Dotnet_DetectsLocalEnvironment_WhenDebuggerAttached()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload(isDebuggerAttached: true);
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ExecutionEnvironment.ShouldBe("Local");
    }

    [Test]
    public async Task POST_Dotnet_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateDotnetBuildPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeOneOf("", "null");
    }
}
