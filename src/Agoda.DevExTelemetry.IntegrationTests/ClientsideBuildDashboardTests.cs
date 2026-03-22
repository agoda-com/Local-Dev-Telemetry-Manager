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
public class ClientsideBuildDashboardTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ClientsideBuildDashboardTests(DatabaseProvider provider) => _provider = provider;

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
    public async Task GetClientsideSummary_ReturnsHotVsFullCounts()
    {
        var today = DateTime.UtcNow.Date;
        for (int i = 0; i < 5; i++)
            await SeedClientsideMetric(today, "hot", 200);
        for (int i = 0; i < 3; i++)
            await SeedClientsideMetric(today, "full", 5000);

        var response = await _client.GetAsync("/api/build-metrics/clientside-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("hotReloadPercent", out _).ShouldBeTrue();
        json.RootElement.TryGetProperty("totalReloadsToday", out _).ShouldBeTrue();
    }

    [Test]
    public async Task GetClientsideSummary_CalculatesHotReloadPercent()
    {
        var today = DateTime.UtcNow.Date;
        for (int i = 0; i < 5; i++)
            await SeedClientsideMetric(today, "hot", 200);
        for (int i = 0; i < 3; i++)
            await SeedClientsideMetric(today, "full", 5000);

        var response = await _client.GetAsync("/api/build-metrics/clientside-summary");
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        var hotPercent = json.RootElement.GetProperty("hotReloadPercent").GetDouble();
        hotPercent.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task GetClientsideSummary_ReturnsDailyAvgTimes()
    {
        var today = DateTime.UtcNow.Date;
        await SeedClientsideMetric(today, "hot", 100);
        await SeedClientsideMetric(today, "hot", 200);
        await SeedClientsideMetric(today, "full", 4000);
        await SeedClientsideMetric(today, "full", 6000);

        var response = await _client.GetAsync("/api/build-metrics/clientside-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("avgHotReloadTimeMs").GetDouble().ShouldBe(150.0);
        json.RootElement.GetProperty("avgFullReloadTimeMs").GetDouble().ShouldBe(5000.0);
    }

    [Test]
    public async Task GetClientsideSummary_FiltersToClientsideCategoryOnly()
    {
        var today = DateTime.UtcNow.Date;
        await SeedClientsideMetric(today, "full", 3000);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
            db.BuildMetrics.Add(new BuildMetric
            {
                Id = Guid.NewGuid().ToString(),
                ReceivedAt = today.AddHours(10),
                UserName = "testuser",
                CpuCount = 8,
                Hostname = "dev-workstation",
                Platform = "Unix",
                Os = "Ubuntu",
                Branch = "main",
                ProjectName = "TestProject",
                Repository = "https://github.com/test/repo",
                RepositoryName = "test-repo",
                TimeTakenMs = 1000,
                MetricType = ".Net",
                BuildCategory = "API",
                ExecutionEnvironment = "Local",
                SourceEndpoint = "/dotnet"
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/build-metrics/clientside-summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("totalReloadsToday").GetInt32().ShouldBe(1);
    }

    private async Task SeedClientsideMetric(DateTime date, string reloadType, double timeTakenMs)
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
            MetricType = "webpack",
            BuildCategory = "Clientside",
            ReloadType = reloadType,
            ExecutionEnvironment = "Local",
            SourceEndpoint = "/webpack"
        });

        await db.SaveChangesAsync();
    }
}
