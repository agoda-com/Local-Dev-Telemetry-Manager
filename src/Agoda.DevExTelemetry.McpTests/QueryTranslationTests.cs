using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Builds;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>
/// Proves the shared query building blocks translate to SQL on both providers: the test-key
/// projection, a GROUP BY on it with aggregates, and sorting and paging on an aggregate.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class QueryTranslationTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;

    public QueryTranslationTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public void SetUp() => _host = new McpTestHost(_provider);

    [TearDown]
    public void TearDown() => _host.Dispose();

    [Test]
    public async Task Facts_GroupedByNameKey_AggregatesSortsAndPagesInSql()
    {
        var at = McpTestHost.Now.AddDays(-1);
        await _host.SeedAsync(
            Seed.Run(at, [
                Seed.Case("Fast", durationMs: 10),
                Seed.Case("Slow", durationMs: 900, fullName: "Web.Tests.LoginTests.Slow(1)"),
                Seed.Case("NoClass", durationMs: 50, className: null)
            ]),
            Seed.Run(at.AddHours(1), [
                Seed.Case("Fast", "Failed", durationMs: 30),
                Seed.Case("Slow", durationMs: 1100, fullName: "Web.Tests.LoginTests.Slow(1)")
            ]));

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
        var window = InsightsScope.Resolve(new InsightsArgs(), McpTestHost.Now, 90);

        var rows = await window.Apply(db.TestCases).Facts()
            .GroupBy(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey })
            .Select(g => new
            {
                g.Key.NameKey,
                Runs = g.Count(),
                Failed = g.Count(f => f.Status == "Failed"),
                TotalMs = g.Sum(f => f.DurationMs ?? 0),
                LastRunAt = g.Max(f => f.ReceivedAt)
            })
            .OrderByDescending(r => r.TotalMs)
            .ThenBy(r => r.NameKey)
            .Skip(0)
            .Take(3)
            .ToListAsync();

        rows.Select(r => r.NameKey).ShouldBe(["Web.Tests.LoginTests.Slow(1)", "NoClass", "Web.Tests.LoginTests.Fast"]);
        rows[2].Runs.ShouldBe(2);
        rows[2].Failed.ShouldBe(1);
        rows[2].TotalMs.ShouldBe(40);
    }

    [Test]
    public async Task Facts_NameKey_MatchesTestKeyNameKey()
    {
        await _host.SeedAsync(Seed.Run(McpTestHost.Now.AddDays(-1), [
            Seed.Case("A", fullName: "Full.A"),
            Seed.Case("B", className: "Cls"),
            Seed.Case("C", className: null),
            Seed.Case("D", className: "", fullName: "")
        ]));

        using var scope = _host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var keys = await db.TestCases.Facts().Select(f => f.NameKey).OrderBy(k => k).ToListAsync();
        var expected = await db.TestCases
            .Select(c => new { c.FullName, c.ClassName, c.Name }).ToListAsync();

        keys.ShouldBe(expected.Select(e => TestKey.NameKey(e.FullName, e.ClassName, e.Name)).OrderBy(k => k, StringComparer.Ordinal));
    }

    [Test]
    public async Task SuiteCatalog_ResolvesSuiteIdAndTestKey()
    {
        await _host.SeedAsync(Seed.Run(McpTestHost.Now.AddDays(-1), [Seed.Case("A")], repository: "r", project: "p", runner: "Vitest"));
        using var scope = _host.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ITestSuiteCatalog>();
        var suiteId = StableId.Suite("r", "p", "Vitest");

        var suite = await catalog.ResolveAsync(suiteId, CancellationToken.None);
        var (keySuite, nameKey) = await catalog.ResolveTestKeyAsync(TestKey.Format(suiteId, "Web.Tests.LoginTests.A"), CancellationToken.None);

        suite.ShouldBe(new TestSuiteIdentity("r", "p", "Vitest"));
        keySuite.ShouldBe(suite);
        nameKey.ShouldBe("Web.Tests.LoginTests.A");
        await Should.ThrowAsync<InsightsArgumentException>(() => catalog.ResolveAsync("deadbeef", CancellationToken.None));
    }

    [Test]
    public async Task SeriesCatalog_ResolvesSeriesAndFiltersNullReloadType()
    {
        await _host.SeedAsync(
            Seed.Build(McpTestHost.Now.AddDays(-1), 100, metricType: ".Net"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 200, metricType: "vitehmr", category: "Clientside", reloadType: "hot"),
            Seed.Build(McpTestHost.Now.AddDays(-1), 900, metricType: "vitehmr", category: "Clientside", reloadType: "full"));
        using var scope = _host.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IBuildSeriesCatalog>();
        var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

        var all = await catalog.ListAsync(CancellationToken.None);
        var net = await catalog.ResolveAsync(StableId.Series("web-app", "Web.Api", ".Net", null), CancellationToken.None);
        var hot = await catalog.ResolveAsync(StableId.Series("web-app", "Web.Api", "vitehmr", "hot"), CancellationToken.None);

        all.Count.ShouldBe(3);
        (await db.BuildMetrics.InSeries(net).Select(b => b.TimeTakenMs).ToListAsync()).ShouldBe([100]);
        (await db.BuildMetrics.InSeries(hot).Select(b => b.TimeTakenMs).ToListAsync()).ShouldBe([200]);
    }
}
