using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class ViteIngestTests
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
    public async Task POST_Vite_TypeVite_SetsReloadTypeFull()
    {
        var payload = TestFixtures.CreateVitePayload(type: "vite");
        var response = await TestFixtures.PostJsonAsync(_client, "/vite", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ReloadType.ShouldBe("full");
    }

    [Test]
    public async Task POST_Vite_TypeViteHmr_SetsReloadTypeHot()
    {
        var payload = TestFixtures.CreateVitePayload(type: "vitehmr");
        await TestFixtures.PostJsonAsync(_client, "/vite", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ReloadType.ShouldBe("hot");
    }

    [Test]
    public async Task POST_Vite_StoresBundleStatsInExtraData()
    {
        var payload = TestFixtures.CreateVitePayload();
        await TestFixtures.PostJsonAsync(_client, "/vite", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ExtraData.ShouldNotBeNullOrEmpty();
        metric.ExtraData.ShouldContain("BundleStats");
    }

    [Test]
    public async Task POST_Vite_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateVitePayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/vite", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
