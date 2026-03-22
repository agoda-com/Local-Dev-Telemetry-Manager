using System.Net;
using System.Text.Json;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ApiBuildDashboardTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ApiBuildDashboardTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task GetApiBuildSummary_ReturnsCorrelatedLifecycleData()
    {
        var today = DateTime.UtcNow.Date;
        await SeedBuildMetric(".Net", "API", today, 1000);
        await SeedBuildMetric(".AspNetStartup", "API", today, 500);
        await SeedBuildMetric(".AspNetResponse", "API", today, 200);

        var response = await _client.GetAsync("/api/build-metrics/api-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("avgCompileTimeMs", out _).ShouldBeTrue();
        json.RootElement.TryGetProperty("avgStartupTimeMs", out _).ShouldBeTrue();
        json.RootElement.TryGetProperty("avgFirstResponseTimeMs", out _).ShouldBeTrue();
    }

    [Test]
    public async Task GetApiBuildSummary_FiltersToApiCategoryOnly()
    {
        var today = DateTime.UtcNow.Date;
        await SeedBuildMetric(".Net", "API", today, 1000);
        await SeedBuildMetric("webpack", "Clientside", today, 3000);

        var response = await _client.GetAsync("/api/build-metrics/api-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("avgCompileTimeMs").GetDouble().ShouldBe(1000.0);
    }

    [Test]
    public async Task GetApiBuildSummary_CalculatesDailyAverages()
    {
        var today = DateTime.UtcNow.Date;
        await SeedBuildMetric(".Net", "API", today, 1000);
        await SeedBuildMetric(".Net", "API", today, 2000);
        await SeedBuildMetric(".Net", "API", today, 3000);

        var response = await _client.GetAsync("/api/build-metrics/api-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("avgCompileTimeMs").GetDouble().ShouldBe(2000.0);
    }

    private async Task SeedBuildMetric(string metricType, string buildCategory, DateTime date,
        double timeTakenMs, string? reloadType = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        db.BuildMetrics.Add(new BuildMetric
        {
            Id = Guid.NewGuid().ToString(),
            ReceivedAt = date.AddHours(10),
            UserName = "testuser",
            CpuCount = 8,
            Hostname = "dev-workstation",
            Platform = "Unix",
            Os = "Ubuntu",
            Branch = "main",
            ProjectName = "TestProject",
            Repository = "https://github.com/test/repo",
            RepositoryName = "test-repo",
            TimeTakenMs = timeTakenMs,
            MetricType = metricType,
            BuildCategory = buildCategory,
            ReloadType = reloadType,
            ExecutionEnvironment = "Local",
            SourceEndpoint = "/dotnet"
        });

        await db.SaveChangesAsync();
    }
}
