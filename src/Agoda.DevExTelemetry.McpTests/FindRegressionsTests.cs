using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>
/// The find_regressions envelope: windows, arguments and ranking. The detectors for each kind have their
/// own fixtures (build, test and suite regression tests).
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindRegressionsTests
{
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindRegressionsTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider);
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task FindRegressions_NoData_ReturnsEmptyTableWithColumns()
    {
        var result = await _client.CallAsync("find_regressions");

        result.IsError.ShouldBeFalse(result.ErrorText);
        result.Rows.ShouldBeEmpty();
        result.Columns.ShouldBe([
            "kind", "subject_id", "repository", "project", "name", "baseline_samples", "current_samples",
            "before_p50_ms", "after_p50_ms", "change_pct", "shift_started_on", "reason", "evidence"
        ]);
    }

    [Test]
    public async Task FindRegressions_Defaults_Cover35DaysEndingNow()
    {
        var result = await _client.CallAsync("find_regressions");

        result.Summary.ShouldContain("current=2026-03-11..2026-03-18");
        result.Summary.ShouldContain("baseline=2026-02-11..2026-03-11");
        result.Summary.ShouldContain("min_increase_pct=20 min_samples=10");
    }

    [Test]
    public async Task FindRegressions_OverriddenWindows_AreApplied()
    {
        var result = await _client.CallAsync("find_regressions",
            new { current_days = 3, baseline_days = 10, min_increase_pct = 50.0, min_samples = 4 });

        result.Summary.ShouldContain("current=2026-03-15..2026-03-18");
        result.Summary.ShouldContain("baseline=2026-03-05..2026-03-15");
        result.Summary.ShouldContain("min_increase_pct=50 min_samples=4");
    }

    [Test]
    public async Task FindRegressions_BadKind_ReturnsToolError()
    {
        var result = await _client.CallAsync("find_regressions", new { kind = "users" });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("kind");
    }

    [Test]
    public async Task FindRegressions_ZeroMinSamples_ReturnsToolError()
    {
        var result = await _client.CallAsync("find_regressions", new { min_samples = 0 });

        result.IsError.ShouldBeTrue();
        result.ErrorText.ShouldContain("min_samples");
    }
}
