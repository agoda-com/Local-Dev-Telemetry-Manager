using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class JUnitIngestTests
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
    public async Task POST_JUnit_CreatesTestRunAndTestCases()
    {
        var payload = TestFixtures.CreateJUnitPayload(testCaseCount: 3);
        var response = await TestFixtures.PostJsonAsync(_client, "/junit", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var runs = await db.TestRuns.ToListAsync();
        var cases = await db.TestCases.ToListAsync();

        runs.Count.ShouldBe(1);
        cases.Count.ShouldBe(3);
    }

    [Test]
    public async Task POST_JUnit_DurationIsAlreadyMs()
    {
        var payload = TestFixtures.CreateJUnitPayload(testCaseCount: 1);
        await TestFixtures.PostJsonAsync(_client, "/junit", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.FirstOrDefaultAsync();
        testCase.ShouldNotBeNull();
        testCase.DurationMs.ShouldBe(42.0);
    }

    [Test]
    public async Task POST_JUnit_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateJUnitPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/junit", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
