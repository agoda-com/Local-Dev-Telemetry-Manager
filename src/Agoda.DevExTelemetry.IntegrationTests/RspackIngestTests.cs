using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class RspackIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public RspackIngestTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task POST_Rspack_TypeRspack_PersistsRspackMetric()
    {
        var payload = TestFixtures.CreateWebpackPayload(type: "rspack", sessionId: "session-rspack");
        var response = await TestFixtures.PostJsonAsync(_client, "/rspack", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.MetricType.ShouldBe("rspack");
        metric.SourceEndpoint.ShouldBe("/rspack");
        metric.SessionId.ShouldBe("session-rspack");
    }

    [Test]
    public async Task POST_Rspack_TypeRsbuild_PersistsRsbuildMetric()
    {
        var payload = TestFixtures.CreateWebpackPayload(type: "rsbuild");
        var response = await TestFixtures.PostJsonAsync(_client, "/rspack", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.MetricType.ShouldBe("rsbuild");
        metric.SourceEndpoint.ShouldBe("/rspack");
    }

    [Test]
    public async Task POST_Webpack_TypeRsbuild_StillAccepted_ForBackCompat()
    {
        var payload = TestFixtures.CreateWebpackPayload(type: "rsbuild");
        var response = await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.MetricType.ShouldBe("rsbuild");
        metric.SourceEndpoint.ShouldBe("/webpack");
    }
}
