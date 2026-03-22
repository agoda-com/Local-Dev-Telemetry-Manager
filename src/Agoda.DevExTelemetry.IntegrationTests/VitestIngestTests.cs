using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class VitestIngestTests
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
    public async Task POST_Vitest_MapsTestCases()
    {
        var payload = TestFixtures.CreateVitestPayload(testCaseCount: 3);
        var response = await TestFixtures.PostJsonAsync(_client, "/vitest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var db = _factory.CreateDbContext();
        var cases = await db.TestCases.ToListAsync();
        cases.Count.ShouldBe(3);
    }

    [Test]
    public async Task POST_Vitest_StoresFilesInExtraData()
    {
        var payload = TestFixtures.CreateVitestPayload();
        await TestFixtures.PostJsonAsync(_client, "/vitest", payload);

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.FirstOrDefaultAsync();
        run.ShouldNotBeNull();
        run.ExtraData.ShouldNotBeNullOrEmpty();
        run.ExtraData.ShouldContain("utils.test.ts");
    }

    [Test]
    public async Task POST_Vitest_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateVitestPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/vitest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
