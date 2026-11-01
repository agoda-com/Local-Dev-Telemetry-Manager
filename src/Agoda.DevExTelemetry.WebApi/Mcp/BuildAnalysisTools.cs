using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class BuildAnalysisTools
{
    private readonly IInsightsToolRunner _runner;

    public BuildAnalysisTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.FIND_SLOW_BUILDS, Title = "Find slow builds", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: build series ranked by sort_by (p50 default, or p95), slowest first.
        Columns: those of list_build_series (series_id, repository, project, build_category, metric_type, reload_type, count, last_at,
        avg_ms, p50_ms, p95_ms), then trend_pct (p50 of the second half of the window against the first half, as a % change;
        empty when either half has fewer than min_samples builds), reason, evidence (get_build_stats by day for the series).
        Thresholds: min_samples (default 10; series with fewer builds in the window are left out).
        Filters: metric_type, build_category, reload_type.
        """)]
    public Task<CallToolResult> FindSlowBuilds(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Rank by p50 (default) or p95.")] string? sort_by = null,
        [Description("Minimum builds in the window for a series to be listed. Default 10.")] int? min_samples = null,
        [Description("Metric type, for example .Net, Gradle, vitehmr.")] string? metric_type = null,
        [Description("Build category, for example API or Clientside.")] string? build_category = null,
        [Description("Reload type, for example hot or full.")] string? reload_type = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.FIND_SLOW_BUILDS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("sort_by", sort_by)
                .With("min_samples", min_samples)
                .With("metric_type", metric_type)
                .With("build_category", build_category)
                .With("reload_type", reload_type),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.EXPLAIN_BUILD_TIME, Title = "Explain build time", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: which dimension best explains the spread of one build series' times. Tries cpu_count, os, platform, tool_version,
        branch, environment and debugger_attached, using only groups with at least min_samples builds (default 5), and ranks the
        dimensions that have at least two such groups by slowest group p50 / fastest group p50. Returns the top_dimensions (default 3).
        Columns: dimension, value, count, avg_ms, p50_ms, p95_ms, ratio_to_fastest, dimension_spread, dimension_rank, reason, evidence
        (get_build_stats grouped by the dimension). One row per group, slowest first within a dimension.
        An empty table with a note means the variation is noise: no dimension has two qualifying groups.
        """)]
    public Task<CallToolResult> ExplainBuildTime(
        [Description("The build series (series_id from list_build_series or find_slow_builds). Required.")] string series_id,
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Minimum builds in a group for it to count. Default 5.")] int? min_samples = null,
        [Description("How many dimensions to return. Default 3.")] int? top_dimensions = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.EXPLAIN_BUILD_TIME,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("series_id", series_id)
                .With("min_samples", min_samples)
                .With("top_dimensions", top_dimensions),
            limit, cursor, format, cancellationToken);
}
