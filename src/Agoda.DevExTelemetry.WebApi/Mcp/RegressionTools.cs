using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class RegressionTools
{
    private readonly IInsightsToolRunner _runner;

    public RegressionTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.FIND_REGRESSIONS, Title = "Find regressions", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: what got slower. Compares a current window (last current_days, default 7) with the baseline before it
        (baseline_days, default 28) for build series, test keys and whole suite runs. A group is flagged when its p50 rose
        by >= min_increase_pct (default 20) and both windows have >= min_samples (default 10) samples.
        Columns: kind (build | test | suite), subject_id (series_id, test_key or suite_id), repository, project, name,
        baseline_samples, current_samples, before_p50_ms, after_p50_ms, change_pct, shift_started_on (first day in the
        current window whose median is above the threshold), reason, evidence (the data tool call that shows it).
        Ranked by change_pct, largest first.
        Filters: kind, repository, project, branch, environment. Thresholds: min_increase_pct (20), min_samples (10),
        current_days (7), baseline_days (28).
        """)]
    public Task<CallToolResult> FindRegressions(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT + " Default all.")] string? environment = null,
        [Description("build | test | suite | all (default).")] string? kind = null,
        [Description("Length of the current window in days. Default 7.")] int? current_days = null,
        [Description("Length of the baseline window before it, in days. Default 28.")] int? baseline_days = null,
        [Description("End of the current window, ISO-8601 UTC. Default now.")] string? to = null,
        [Description("Minimum p50 increase in percent. Default 20.")] double? min_increase_pct = null,
        [Description("Minimum samples in each window. Default 10.")] int? min_samples = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.FIND_REGRESSIONS,
            ToolArgs.Scope(repository, project, branch, environment, null, null, to)
                .With("kind", kind)
                .With("current_days", current_days)
                .With("baseline_days", baseline_days)
                .With("min_increase_pct", min_increase_pct)
                .With("min_samples", min_samples),
            limit, cursor, format, cancellationToken);
}
