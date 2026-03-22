using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class WebpackIngestTests
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
    public async Task POST_Webpack_SetsClientsideCategory()
    {
        var payload = TestFixtures.CreateWebpackPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.BuildCategory.ShouldBe("Clientside");
    }

    [Test]
    public async Task POST_Webpack_FullBuild_SetsReloadTypeFull()
    {
        var payload = TestFixtures.CreateWebpackPayload(withHmrFeedback: false);
        await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ReloadType.ShouldBe("full");
    }

    [Test]
    public async Task POST_Webpack_HmrDevFeedback_SetsReloadTypeHot()
    {
        var payload = TestFixtures.CreateWebpackPayload(withHmrFeedback: true);
        await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ReloadType.ShouldBe("hot");
    }

    [Test]
    public async Task POST_Webpack_StoresDevFeedbackInExtraData()
    {
        var payload = TestFixtures.CreateWebpackPayload(withHmrFeedback: true);
        await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.FirstOrDefaultAsync();
        metric.ShouldNotBeNull();
        metric.ExtraData.ShouldNotBeNullOrEmpty();
        metric.ExtraData.ShouldContain("DevFeedback");
    }

    [Test]
    public async Task POST_Webpack_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateWebpackPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/webpack", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
