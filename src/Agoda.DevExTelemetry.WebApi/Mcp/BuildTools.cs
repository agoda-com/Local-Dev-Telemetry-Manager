using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class BuildTools
{
    private readonly IInsightsToolRunner _runner;

    public BuildTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.LIST_BUILD_SERIES, Title = "List build series", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Build series in the window: one row per repository + project + metric type + reload type (hot and full reloads are separate series).
        Use series_id to filter get_builds and get_build_stats.
        Columns: series_id, repository, project, build_category, metric_type, reload_type, count, last_at, avg_ms, p50_ms, p95_ms.
        Filters: metric_type, build_category, reload_type. Sort: count (default, desc), avg_ms, p50_ms, p95_ms, last_at.
        """)]
    public Task<CallToolResult> ListBuildSeries(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Metric type, for example .Net, Gradle, vitehmr (list_scopes kind=metric_type).")] string? metric_type = null,
        [Description("Build category, for example API or Clientside.")] string? build_category = null,
        [Description("Reload type, for example hot or full.")] string? reload_type = null,
        [Description(ToolArgs.SORT)] string? sort = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.LIST_BUILD_SERIES,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("metric_type", metric_type)
                .With("build_category", build_category)
                .With("reload_type", reload_type)
                .With("sort", sort),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_BUILDS, Title = "Get builds", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Raw build metric rows, newest first.
        Columns: id, received_at, repository, project, build_category, metric_type, reload_type, time_ms, branch, environment, platform, os, cpu_count, tool_version, debugger_attached, commit_sha.
        Filters: series_id, metric_type, build_category, reload_type, min_ms, tool_version, platform, os, cpu_count.
        Pass an id to get_build for the full record with its extra data.
        """)]
    public Task<CallToolResult> GetBuilds(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this build series (series_id from list_build_series).")] string? series_id = null,
        [Description("Metric type, for example .Net, Gradle, vitehmr.")] string? metric_type = null,
        [Description("Build category, for example API or Clientside.")] string? build_category = null,
        [Description("Reload type, for example hot or full.")] string? reload_type = null,
        [Description("Only builds that took at least this many milliseconds.")] double? min_ms = null,
        [Description("Build tool version.")] string? tool_version = null,
        [Description("Platform, for example Linux or Windows.")] string? platform = null,
        [Description("Operating system string.")] string? os = null,
        [Description("Only machines with this CPU count.")] int? cpu_count = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_BUILDS,
            Filters(ToolArgs.Scope(repository, project, branch, environment, days, from, to),
                series_id, metric_type, build_category, reload_type, min_ms, tool_version, platform, os, cpu_count),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_BUILD_STATS, Title = "Get build stats", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Build time statistics grouped by any combination of: repository, project, metric_type, reload_type, day, week, branch, environment, platform, os, cpu_count, tool_version, debugger_attached.
        group_by is required, a comma list; group_by=day is a daily time series (day is a UTC yyyy-MM-dd, week is the ISO week's Monday).
        Columns: the group columns in the order given, then count, avg_ms, p50_ms, p95_ms, max_ms, total_ms.
        Filters: series_id, metric_type, build_category, reload_type, min_ms, tool_version, platform, os, cpu_count.
        Sort: any stat column or group column. Default: the group columns ascending.
        """)]
    public Task<CallToolResult> GetBuildStats(
        [Description("Comma list of group columns, for example 'metric_type,cpu_count'. Required.")] string group_by,
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this build series (series_id from list_build_series).")] string? series_id = null,
        [Description("Metric type, for example .Net, Gradle, vitehmr.")] string? metric_type = null,
        [Description("Build category, for example API or Clientside.")] string? build_category = null,
        [Description("Reload type, for example hot or full.")] string? reload_type = null,
        [Description("Only builds that took at least this many milliseconds.")] double? min_ms = null,
        [Description("Build tool version.")] string? tool_version = null,
        [Description("Platform, for example Linux or Windows.")] string? platform = null,
        [Description("Operating system string.")] string? os = null,
        [Description("Only machines with this CPU count.")] int? cpu_count = null,
        [Description(ToolArgs.SORT)] string? sort = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_BUILD_STATS,
            Filters(ToolArgs.Scope(repository, project, branch, environment, days, from, to),
                series_id, metric_type, build_category, reload_type, min_ms, tool_version, platform, os, cpu_count)
                .With("group_by", group_by)
                .With("sort", sort),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_BUILD, Title = "Get build", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        One build metric by id, as vertical rows. The time window does not apply.
        Columns: field, value. One row per get_builds column, then the build's extra data flattened as extra.<path> rows
        (nested objects use dots, arrays use [i]; for Gradle for example extra.RequestedTasks, extra.CacheRatio).
        """)]
    public Task<CallToolResult> GetBuild(
        [Description("The build id, from get_builds.")] string id,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_BUILD,
            new InsightsArgs().With("id", id),
            limit, cursor, format, cancellationToken);

    private static InsightsArgs Filters(
        InsightsArgs args, string? series_id, string? metric_type, string? build_category, string? reload_type,
        double? min_ms, string? tool_version, string? platform, string? os, int? cpu_count) =>
        args.With("series_id", series_id)
            .With("metric_type", metric_type)
            .With("build_category", build_category)
            .With("reload_type", reload_type)
            .With("min_ms", min_ms)
            .With("tool_version", tool_version)
            .With("platform", platform)
            .With("os", os)
            .With("cpu_count", cpu_count);
}
