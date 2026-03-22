using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class GradleIngestTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task POST_Gradle_ParsesStringDurationMs()
    {
        var payload = TestFixtures.CreateGradlePayload(durationMs: "5000");
        var response = await TestFixtures.PostJsonAsync(_client, "/gradletalaiot", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.TimeTakenMs.ShouldBe(5000.0);
    }

    [Test]
    public async Task POST_Gradle_ParsesStringCpuCount()
    {
        var payload = TestFixtures.CreateGradlePayload(cpuCount: "8");
        await TestFixtures.PostJsonAsync(_client, "/gradletalaiot", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.CpuCount.ShouldBe(8);
    }

    [Test]
    public async Task POST_Gradle_SetsApiCategory()
    {
        var payload = TestFixtures.CreateGradlePayload();
        await TestFixtures.PostJsonAsync(_client, "/gradletalaiot", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.BuildCategory.ShouldBe("API");
        metric.MetricType.ShouldBe("GradleTalaiot");
    }

    [Test]
    public async Task POST_Gradle_StoresCacheStatsInExtraData()
    {
        var payload = TestFixtures.CreateGradlePayload();
        await TestFixtures.PostJsonAsync(_client, "/gradletalaiot", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ExtraData.ShouldNotBeNullOrEmpty();
        metric.ExtraData.ShouldContain("CacheRatio");
    }

    [Test]
    public async Task POST_Gradle_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateGradlePayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/gradletalaiot", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
