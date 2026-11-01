using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class OverviewTools
{
    private readonly IInsightsToolRunner _runner;

    public OverviewTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.GET_OVERVIEW, Title = "Overview", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: start here. The top rows (default 5) of find_failing_tests, find_flaky_tests, find_slow_tests,
        find_slow_builds and find_regressions for a scope, with each tool's default thresholds.
        Columns: section (the analysis tool), rank, subject_id (test_key, series_id or suite_id), repository, project,
        reason, evidence (the data tool call that shows it). Call the section's tool for more rows or other thresholds,
        and explain_build_time with a series_id to see why a build is slow.
        """)]
    public Task<CallToolResult> GetOverview(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT + " Default: each tool's own (find_flaky_tests uses ci).")] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Rows per section. Default 5.")] int? top = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_OVERVIEW,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to).With("top", top),
            limit, cursor, format, cancellationToken);
}
