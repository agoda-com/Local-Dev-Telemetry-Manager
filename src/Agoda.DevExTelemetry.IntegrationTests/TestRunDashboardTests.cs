using System.Net;
using System.Text.Json;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class TestRunDashboardTests
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
    public async Task GetTestRunSummary_ReturnsDailyAggregates()
    {
        await SeedTestRuns(10, daysSpread: 3);

        var response = await _client.GetAsync("/api/test-runs/summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("totalRuns", out var totalRuns).ShouldBeTrue();
        totalRuns.GetInt32().ShouldBe(10);
    }

    [Test]
    public async Task GetTestRunSummary_FiltersEnvironment()
    {
        await SeedTestRunsWithEnvironment("Local", 5);
        await SeedTestRunsWithEnvironment("CI", 3);

        var response = await _client.GetAsync("/api/test-runs/summary?Environment=Local");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("totalRuns").GetInt32().ShouldBe(5);
    }

    [Test]
    public async Task GetTestRuns_ReturnsPaginatedResults()
    {
        await SeedTestRuns(25);

        var response = await _client.GetAsync("/api/test-runs?Page=2&PageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(25);
        json.RootElement.GetProperty("items").GetArrayLength().ShouldBe(10);
    }

    [Test]
    public async Task GetTestRunDetail_ReturnsTestCases()
    {
        var runId = Guid.NewGuid().ToString();
        await SeedTestRunWithCases(runId, 5);

        var response = await _client.GetAsync($"/api/test-runs/{runId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("testCases").GetArrayLength().ShouldBe(5);
    }

    [Test]
    public async Task GetTestRunDetail_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/test-runs/nonexistent-id");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task SeedTestRuns(int count, int daysSpread = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        for (int i = 0; i < count; i++)
        {
            db.TestRuns.Add(new TestRun
            {
                Id = Guid.NewGuid().ToString(),
                RunId = Guid.NewGuid().ToString(),
                ReceivedAt = DateTime.UtcNow.AddDays(-(i % daysSpread)),
                UserName = "testuser",
                Hostname = "dev-workstation",
                Platform = "Unix",
                Os = "Ubuntu",
                Branch = "main",
                ProjectName = "TestProject",
                Repository = "https://github.com/test/repo",
                RepositoryName = "test-repo",
                TestRunner = "NUnit",
                ExecutionEnvironment = "Local",
                TotalTests = 10,
                PassedTests = 8,
                FailedTests = 1,
                SkippedTests = 1,
                TotalDurationMs = 5000.0 + i * 100,
                SourceEndpoint = "/dotnet/nunit"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedTestRunsWithEnvironment(string environment, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        for (int i = 0; i < count; i++)
        {
            db.TestRuns.Add(new TestRun
            {
                Id = Guid.NewGuid().ToString(),
                RunId = Guid.NewGuid().ToString(),
                ReceivedAt = DateTime.UtcNow,
                UserName = "testuser",
                Hostname = "dev-workstation",
                Platform = "Unix",
                Os = "Ubuntu",
                Branch = "main",
                ProjectName = "TestProject",
                Repository = "https://github.com/test/repo",
                RepositoryName = "test-repo",
                TestRunner = "NUnit",
                ExecutionEnvironment = environment,
                TotalTests = 10,
                PassedTests = 8,
                FailedTests = 1,
                SkippedTests = 1,
                TotalDurationMs = 5000.0,
                SourceEndpoint = "/dotnet/nunit"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedTestRunWithCases(string runId, int caseCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var run = new TestRun
        {
            Id = runId,
            RunId = Guid.NewGuid().ToString(),
            ReceivedAt = DateTime.UtcNow,
            UserName = "testuser",
            Hostname = "dev-workstation",
            Platform = "Unix",
            Os = "Ubuntu",
            Branch = "main",
            ProjectName = "TestProject",
            Repository = "https://github.com/test/repo",
            RepositoryName = "test-repo",
            TestRunner = "NUnit",
            ExecutionEnvironment = "Local",
            TotalTests = caseCount,
            PassedTests = caseCount,
            FailedTests = 0,
            SkippedTests = 0,
            TotalDurationMs = 1000.0 * caseCount,
            SourceEndpoint = "/dotnet/nunit"
        };

        db.TestRuns.Add(run);

        for (int i = 0; i < caseCount; i++)
        {
            db.TestCases.Add(new TestCase
            {
                TestRunId = runId,
                Name = $"Test_{i}",
                FullName = $"Namespace.Class.Test_{i}",
                ClassName = "Namespace.Class",
                MethodName = $"Test_{i}",
                Status = "Passed",
                DurationMs = 1000.0
            });
        }

        await db.SaveChangesAsync();
    }
}
