using System.Net;
using System.Text.Json;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class FilterTests
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
    public async Task GetFilters_ReturnsDistinctValues()
    {
        await SeedDiverseData();

        var response = await _client.GetAsync("/api/filters");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.GetProperty("projects").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
        json.RootElement.GetProperty("repositories").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
        json.RootElement.GetProperty("branches").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task GetFilters_ReturnsAlphabeticOrder()
    {
        await SeedDiverseData();

        var response = await _client.GetAsync("/api/filters");
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        var projects = json.RootElement.GetProperty("projects")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        projects.ShouldBe(projects.OrderBy(p => p).ToList());
    }

    private async Task SeedDiverseData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        db.BuildMetrics.Add(CreateBuildMetric("AlphaProject", "alpha-repo", "main", ".Net"));
        db.BuildMetrics.Add(CreateBuildMetric("BetaProject", "beta-repo", "develop", "webpack"));

        db.TestRuns.Add(CreateTestRun("AlphaProject", "alpha-repo", "main", "NUnit"));
        db.TestRuns.Add(CreateTestRun("BetaProject", "beta-repo", "feature/x", "Jest"));

        await db.SaveChangesAsync();
    }

    private static BuildMetric CreateBuildMetric(string project, string repo, string branch, string metricType) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            ReceivedAt = DateTime.UtcNow,
            UserName = "testuser",
            CpuCount = 8,
            Hostname = "dev-workstation",
            Platform = "Unix",
            Os = "Ubuntu",
            Branch = branch,
            ProjectName = project,
            Repository = $"https://github.com/test/{repo}",
            RepositoryName = repo,
            TimeTakenMs = 1000,
            MetricType = metricType,
            BuildCategory = metricType == ".Net" ? "API" : "Clientside",
            ExecutionEnvironment = "Local",
            SourceEndpoint = "/dotnet"
        };

    private static TestRun CreateTestRun(string project, string repo, string branch, string runner) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            RunId = Guid.NewGuid().ToString(),
            ReceivedAt = DateTime.UtcNow,
            UserName = "testuser",
            Hostname = "dev-workstation",
            Platform = "Unix",
            Os = "Ubuntu",
            Branch = branch,
            ProjectName = project,
            Repository = $"https://github.com/test/{repo}",
            RepositoryName = repo,
            TestRunner = runner,
            ExecutionEnvironment = "Local",
            TotalTests = 10,
            PassedTests = 10,
            FailedTests = 0,
            SkippedTests = 0,
            TotalDurationMs = 5000,
            SourceEndpoint = "/dotnet/nunit"
        };
}
