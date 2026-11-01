using Agoda.DevExTelemetry.McpTests.Infrastructure;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>Acceptance: "Each description names its columns, filters and default thresholds."</summary>
[TestFixture]
public class ToolDescriptionTests
{
    [Test]
    public async Task ListTools_EveryDescriptionNamesItsColumnsAndFilters()
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite);
        await using var client = await host.ConnectAsync();

        var tools = await client.ListToolsAsync();

        foreach (var tool in tools)
        {
            tool.Description.ShouldContain("Columns", Case.Sensitive, tool.Name);
            tool.Description.ShouldContain("Filters", Case.Sensitive, tool.Name);
        }
    }

    [TestCase("find_slow_tests", "min_runs")]
    [TestCase("find_flaky_tests", "min_runs")]
    [TestCase("find_failing_tests", "min_fail_rate")]
    [TestCase("find_failing_tests", "recent_runs")]
    [TestCase("find_slow_builds", "min_samples")]
    [TestCase("explain_build_time", "min_samples")]
    [TestCase("find_regressions", "min_increase_pct")]
    [TestCase("find_regressions", "min_samples")]
    public async Task ListTools_AnalysisDescriptionNamesThreshold(string toolName, string threshold)
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite);
        await using var client = await host.ConnectAsync();

        var tools = await client.ListToolsAsync();

        tools.Single(t => t.Name == toolName).Description.ShouldContain(threshold);
    }
}
