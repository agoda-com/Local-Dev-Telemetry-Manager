using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class JestIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public JestIngestTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task POST_Jest_FlattensNestedTestResults()
    {
        var payload = TestFixtures.CreateJestPayload(suiteCount: 2, testPerSuite: 3);
        var response = await TestFixtures.PostJsonAsync(_client, "/jest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var cases = await db.TestCases.ToListAsync();
        cases.Count.ShouldBe(6);
    }

    [Test]
    public async Task POST_Jest_UsesTestFilePathAsClassName()
    {
        var payload = TestFixtures.CreateJestPayload(suiteCount: 1, testPerSuite: 1);
        await TestFixtures.PostJsonAsync(_client, "/jest", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.FirstOrDefaultAsync();
        testCase.ShouldNotBeNull();
        testCase.ClassName.ShouldBe("/src/tests/suite1.test.ts");
    }

    [Test]
    public async Task POST_Jest_UsesAncestorTitlesForFullName()
    {
        var payload = TestFixtures.CreateJestPayload(suiteCount: 1, testPerSuite: 1);
        await TestFixtures.PostJsonAsync(_client, "/jest", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.FirstOrDefaultAsync();
        testCase.ShouldNotBeNull();
        testCase.FullName!.ShouldContain("Suite 1");
        testCase.FullName.ShouldContain(">");
    }

    [Test]
    public async Task POST_Jest_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateJestPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/jest", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
