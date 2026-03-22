using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ScalaTestIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ScalaTestIngestTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task POST_ScalaTest_HandlesGzipBody()
    {
        var payload = TestFixtures.CreateScalaTestPayload();
        var response = await TestFixtures.PostGzippedJsonAsync(_client, "/scala/scalatest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.FirstOrDefaultAsync();
        run.ShouldNotBeNull();
    }

    [Test]
    public async Task POST_ScalaTest_MapsRepositoryUrl()
    {
        var payload = TestFixtures.CreateScalaTestPayload();
        await TestFixtures.PostJsonAsync(_client, "/scala/scalatest", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.FirstOrDefaultAsync();
        run.ShouldNotBeNull();
        run.Repository.ShouldBe("https://github.com/test/scala-repo");
    }

    [Test]
    public async Task POST_ScalaTest_UsesPayloadLevelAggregates()
    {
        var payload = TestFixtures.CreateScalaTestPayload(testCaseCount: 2);
        await TestFixtures.PostJsonAsync(_client, "/scala/scalatest", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.FirstOrDefaultAsync();
        run.ShouldNotBeNull();
        run.TotalTests.ShouldBe(3);
        run.PassedTests.ShouldBe(2);
        run.FailedTests.ShouldBe(1);
        run.SkippedTests.ShouldBe(0);
    }

    [Test]
    public async Task POST_ScalaTest_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateScalaTestPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/scala/scalatest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
