using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary>
/// Filters shared by <c>get_builds</c> and <c>get_build_stats</c>. Null means "not filtered".
/// <see cref="Series"/> is a resolved <c>series_id</c>.
/// </summary>
public sealed record BuildFilter
{
    public BuildSeriesIdentity? Series { get; init; }
    public string? MetricType { get; init; }
    public string? BuildCategory { get; init; }
    public string? ReloadType { get; init; }
    public double? MinMs { get; init; }
    public string? ToolVersion { get; init; }
    public string? Platform { get; init; }
    public string? Os { get; init; }
    public int? CpuCount { get; init; }

    public IQueryable<BuildMetric> Apply(IQueryable<BuildMetric> metrics)
    {
        if (Series != null) metrics = metrics.InSeries(Series);
        if (MetricType != null) metrics = metrics.Where(b => b.MetricType == MetricType);
        if (BuildCategory != null) metrics = metrics.Where(b => b.BuildCategory == BuildCategory);
        if (ReloadType != null) metrics = metrics.Where(b => b.ReloadType == ReloadType);
        if (MinMs != null) metrics = metrics.Where(b => b.TimeTakenMs >= MinMs);
        if (ToolVersion != null) metrics = metrics.Where(b => b.ToolVersion == ToolVersion);
        if (Platform != null) metrics = metrics.Where(b => b.Platform == Platform);
        if (Os != null) metrics = metrics.Where(b => b.Os == Os);
        if (CpuCount != null) metrics = metrics.Where(b => b.CpuCount == CpuCount);
        return metrics;
    }

    /// <summary>The filters in summary form.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Series != null) parts.Add($"series_id={Series.SeriesId}");
        if (MetricType != null) parts.Add($"metric_type={MetricType}");
        if (BuildCategory != null) parts.Add($"build_category={BuildCategory}");
        if (ReloadType != null) parts.Add($"reload_type={ReloadType}");
        if (MinMs != null) parts.Add($"min_ms={MinMs}");
        if (ToolVersion != null) parts.Add($"tool_version={ToolVersion}");
        if (Platform != null) parts.Add($"platform={Platform}");
        if (Os != null) parts.Add($"os={Os}");
        if (CpuCount != null) parts.Add($"cpu_count={CpuCount}");
        return string.Join(" ", parts);
    }

    /// <summary>Reads the filters; <paramref name="series"/> resolves <c>series_id</c> (null when it was not given).</summary>
    public static BuildFilter FromArgs(InsightsArgs args, BuildSeriesIdentity? series) => new()
    {
        Series = series,
        MetricType = args.GetString("metric_type"),
        BuildCategory = args.GetString("build_category"),
        ReloadType = args.GetString("reload_type"),
        MinMs = args.GetDouble("min_ms"),
        ToolVersion = args.GetString("tool_version"),
        Platform = args.GetString("platform"),
        Os = args.GetString("os"),
        CpuCount = args.GetInt("cpu_count")
    };
}

/// <summary>One series of builds in the window, with its timing statistics.</summary>
public sealed record BuildSeriesRow(
    string SeriesId,
    string Repository,
    string Project,
    string BuildCategory,
    string MetricType,
    string? ReloadType,
    int Count,
    DateTime LastAt,
    double AvgMs,
    double P50Ms,
    double P95Ms);

/// <summary>One build metric row (no user or host name, no payload).</summary>
public sealed record BuildRow(
    string Id,
    DateTime ReceivedAt,
    string Repository,
    string Project,
    string BuildCategory,
    string MetricType,
    string? ReloadType,
    double TimeMs,
    string Branch,
    string Environment,
    string Platform,
    string Os,
    int CpuCount,
    string? ToolVersion,
    bool DebuggerAttached,
    string? CommitSha)
{
    /// <summary>The series this build belongs to.</summary>
    public string SeriesId => StableId.Series(Repository, Project, MetricType, ReloadType);
}

/// <summary>A build row plus its raw <c>ExtraData</c> JSON text.</summary>
public sealed record BuildDetail(BuildRow Build, string? ExtraData);

/// <summary>
/// One group of <c>get_build_stats</c>. <see cref="Values"/> lines up with the requested group-by list:
/// strings, <c>int</c> (cpu_count), <c>bool</c> (debugger_attached); <c>day</c> and <c>week</c> are <c>yyyy-MM-dd</c> strings.
/// </summary>
public sealed record BuildStatsRow(
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<object?> Values,
    long Count,
    double AvgMs,
    double P50Ms,
    double P95Ms,
    double MaxMs,
    double TotalMs)
{
    /// <summary>The value of one group column, by name.</summary>
    public object? Value(string dimension)
    {
        var i = -1;
        for (var k = 0; k < GroupBy.Count; k++)
            if (GroupBy[k] == dimension) { i = k; break; }
        return i < 0 ? null : Values[i];
    }
}
