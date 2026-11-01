using System.Net;
using System.Text;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Analysis;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class PlaywrightIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public PlaywrightIngestTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task Playwright_WhenTestPassesOnRetry_StoresFailedAndPassedAttempts()
    {
        var payload = CreatePayload(
            Attempt("flaky", "failed", retry: 0, errorMessage: "locator not visible"),
            Attempt("flaky", "passed", retry: 1));

        var response = await TestFixtures.PostJsonAsync(_client, "/playwright", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using (var db = _factory.CreateDbContext())
        {
            var run = await db.TestRuns.SingleAsync();
            run.TestRunner.ShouldBe("Playwright");
            run.SourceEndpoint.ShouldBe("/playwright");
            run.TotalTests.ShouldBe(2);
            run.PassedTests.ShouldBe(1);
            run.FailedTests.ShouldBe(1);

            var cases = await db.TestCases.OrderBy(c => c.Id).ToListAsync();
            cases.Select(c => (c.Name, c.FullName, c.ClassName, c.Status)).ShouldBe(new (string, string?, string?, string)[]
            {
                ("flaky", "a.spec.tsx > flaky", "src/a.spec.tsx", "Failed"),
                ("flaky", "a.spec.tsx > flaky", "src/a.spec.tsx", "Passed")
            });
            cases[0].ErrorMessage.ShouldBe("locator not visible");
            cases[1].ErrorMessage.ShouldBeNull();
        }

        // find_flaky_tests picks its candidates from this query: one test key with both outcomes.
        using var scope = _factory.Services.CreateScope();
        var analysis = scope.ServiceProvider.GetRequiredService<ITestAnalysisQueries>();
        var now = DateTime.UtcNow;
        var candidates = await analysis.GetCandidatesAsync(
            new InsightsScope(null, null, null, InsightsScope.ENV_ALL, new InsightsWindow(now.AddHours(-1), now.AddHours(1))),
            new TestsFilter { Status = TestStatusFilter.HasPassesAndFailures },
            CancellationToken.None);

        var candidate = candidates.ShouldHaveSingleItem();
        candidate.NameKey.ShouldBe("a.spec.tsx > flaky");
        candidate.Suite.TestRunner.ShouldBe("Playwright");
        candidate.Passed.ShouldBe(1);
        candidate.Failed.ShouldBe(1);
    }

    [TestCase("timedOut")]
    [TestCase("interrupted")]
    public async Task Playwright_WhenStatusTimedOut_StoresFailed(string status)
    {
        var payload = CreatePayload(Attempt("slow", status, retry: 0, errorMessage: "Test timeout of 30000ms exceeded."));

        var response = await TestFixtures.PostJsonAsync(_client, "/playwright", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.SingleAsync();
        run.FailedTests.ShouldBe(1);
        run.PassedTests.ShouldBe(0);
        var testCase = await db.TestCases.SingleAsync();
        testCase.Status.ShouldBe("Failed");
        testCase.ErrorMessage.ShouldBe("Test timeout of 30000ms exceeded.");
    }

    [TestCase("{ \"id\": \"broken\", \"testcases\": [ ")]
    [TestCase("{ \"id\": \"broken\", \"testcases\": \"not a list\" }")]
    [TestCase("{ \"id\": \"broken\", \"timestamp\": \"yesterday\" }")]
    public async Task Playwright_WhenBodyMalformed_Returns400(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/playwright", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.DrainBackgroundQueuesAsync();
        using var db = _factory.CreateDbContext();
        (await db.TestRuns.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Playwright_TruncatesLongErrorMessages()
    {
        var payload = CreatePayload(Attempt("fails", "failed", retry: 0, errorMessage: new string('x', 5000)));

        await TestFixtures.PostJsonAsync(_client, "/playwright", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.SingleAsync();
        testCase.ErrorMessage!.Length.ShouldBe(4096);
    }

    private static object Attempt(string name, string status, int retry, string? errorMessage = null) =>
        new
        {
            name,
            fullName = $"a.spec.tsx > {name}",
            file = "src/a.spec.tsx",
            project = "chromium",
            status,
            retry,
            duration = 100,
            startTime = "2026-11-02T00:00:00.000Z",
            errorMessage
        };

    private static object CreatePayload(params object[] testcases) =>
        new
        {
            id = Guid.NewGuid().ToString(),
            userName = "dev",
            cpuCount = 8,
            hostname = "dev-box",
            platform = "Linux",
            os = "6.18.0",
            timeTaken = 1234,
            branch = "main",
            projectName = "demo",
            repository = "https://github.com/example/demo.git",
            repositoryName = "demo",
            timestamp = 1793577600000,
            builtAt = "2026-11-02T00:00:00.000Z",
            type = "playwright",
            playwrightVersion = "1.62.0",
            runId = "run-pw",
            workers = 2,
            retries = 1,
            files = new[] { new { name = "src/a.spec.tsx", status = "passed", duration = 900 } },
            testcases
        };
}
