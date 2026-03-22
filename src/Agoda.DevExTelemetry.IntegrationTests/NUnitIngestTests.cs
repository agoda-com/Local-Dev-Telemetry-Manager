using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class NUnitIngestTests
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
    public async Task POST_NUnit_CreatesTestRunAndTestCases()
    {
        var payload = TestFixtures.CreateNUnitPayload(testCaseCount: 3);
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet/nunit", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var db = _factory.CreateDbContext();
        var runs = await db.TestRuns.ToListAsync();
        var cases = await db.TestCases.ToListAsync();

        runs.Count.ShouldBe(1);
        cases.Count.ShouldBe(3);
    }

    [Test]
    public async Task POST_NUnit_ConvertsDurationSecondsToMs()
    {
        var id = Guid.NewGuid().ToString();
        var payload = new
        {
            id,
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            nUnitTestCases = new[]
            {
                new
                {
                    id = "tc-1", name = "Test_1", fullName = "NS.Class.Test_1",
                    className = "NS.Class", methodName = "Test_1",
                    result = "Passed", duration = 1.5,
                    startTime = "2026-03-22T10:00:00Z", endTime = "2026-03-22T10:00:02Z",
                    errorMessage = (string?)null
                }
            }
        };

        await TestFixtures.PostJsonAsync(_client, "/dotnet/nunit", payload);

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.FirstOrDefaultAsync();
        testCase.ShouldNotBeNull();
        testCase.DurationMs.ShouldBe(1500.0);
    }

    [Test]
    public async Task POST_NUnit_NormalizesTestStatus()
    {
        var payload = new
        {
            id = Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            nUnitTestCases = new object[]
            {
                new { id = "tc-1", name = "T1", result = "Passed", duration = 1.0 },
                new { id = "tc-2", name = "T2", result = "Failed", duration = 1.0 },
                new { id = "tc-3", name = "T3", result = "Skipped", duration = 0.0 }
            }
        };

        await TestFixtures.PostJsonAsync(_client, "/dotnet/nunit", payload);

        using var db = _factory.CreateDbContext();
        var cases = await db.TestCases.OrderBy(c => c.Name).ToListAsync();
        cases[0].Status.ShouldBe("Passed");
        cases[1].Status.ShouldBe("Failed");
        cases[2].Status.ShouldBe("Skipped");
    }

    [Test]
    public async Task POST_NUnit_ComputesAggregateCounts()
    {
        var payload = TestFixtures.CreateNUnitPayload(testCaseCount: 6);
        await TestFixtures.PostJsonAsync(_client, "/dotnet/nunit", payload);

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.FirstOrDefaultAsync();
        run.ShouldNotBeNull();
        run.TotalTests.ShouldBe(6);
        run.PassedTests.ShouldNotBeNull();
        run.FailedTests.ShouldNotBeNull();
        run.SkippedTests.ShouldNotBeNull();
        (run.PassedTests + run.FailedTests + run.SkippedTests).ShouldBe(6);
    }

    [Test]
    public async Task POST_NUnit_Returns200_WithEmptyBody()
    {
        var payload = TestFixtures.CreateNUnitPayload();
        var response = await TestFixtures.PostJsonAsync(_client, "/dotnet/nunit", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
