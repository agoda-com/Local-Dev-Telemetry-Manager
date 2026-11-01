using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>The per-test methods of <see cref="ITestHistoryQueries"/> that the analysis tools call.</summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class TestHistoryQueriesTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private static readonly TestSuiteIdentity Suite = new("web-app", "Web.Tests", "NUnit");
    private const string KEY = "Web.Tests.LoginTests.A";
    private static DateTime T(double hoursAgo) => McpTestHost.Now.AddHours(-hoursAgo);

    public TestHistoryQueriesTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        await _host.SeedAsync(
            Seed.Run(T(10), [Seed.Case("A", "Failed", error: "old failure")]),
            Seed.Run(T(9), [Seed.Case("A", "Passed")]),
            Seed.Run(T(8), [Seed.Case("A", "Failed", error: "Timeout\nstack 1")], branch: "main"),
            Seed.Run(T(7), [Seed.Case("A", "Skipped")]),
            Seed.Run(T(6), [Seed.Case("A", "Failed", error: "Timeout\nstack 2")], branch: "feature/x"),
            Seed.Run(T(5), [Seed.Case("A", "Failed", error: "Null ref")], branch: "main"));
    }

    [TearDown]
    public void TearDown() => _host.Dispose();

    private InsightsScope Scope() => new(null, null, null, InsightsScope.ENV_ALL, new InsightsWindow(T(100), McpTestHost.Now));

    [Test]
    public async Task GetFailureStreaks_FailuresAfterLastPass_ReturnsStreak()
    {
        using var scope = _host.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITestHistoryQueries>();

        var result = await queries.GetFailureStreaksAsync(Scope(), [(Suite, KEY)], CancellationToken.None);

        var streak = result[(Suite, KEY)];
        streak.FirstFailureAt.ShouldBe(T(8));
        streak.LastPassAt.ShouldBe(T(9));
        streak.LatestFailureAt.ShouldBe(T(5));
        streak.LatestError.ShouldBe("Null ref");
        streak.FailedExecutions.ShouldBe(3);
        streak.Branches.ShouldBe(["feature/x", "main"]);
    }

    [Test]
    public async Task GetErrorSummaries_FailedRows_MergesByFirstLineMostFrequentFirst()
    {
        using var scope = _host.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITestHistoryQueries>();

        var result = await queries.GetErrorSummariesAsync(Scope(), [(Suite, KEY)], 2, CancellationToken.None);

        var errors = result[(Suite, KEY)];
        errors.Select(e => (e.Error, e.Count)).ShouldBe([("Timeout", 2), ("Null ref", 1)]);
        errors[0].LastSeenAt.ShouldBe(T(6));
    }

    [Test]
    public async Task GetRecentExecutions_PerTestLimit_ReturnsNewestFirst()
    {
        using var scope = _host.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<ITestHistoryQueries>();

        var result = await queries.GetRecentExecutionsAsync(Scope(), [(Suite, KEY)], 2, CancellationToken.None);

        result[(Suite, KEY)].Select(r => r.ReceivedAt).ShouldBe([T(5), T(6)]);
    }
}
