using System.Globalization;
using System.Linq.Expressions;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary>
/// Build metric queries behind <c>list_build_series</c>, <c>get_builds</c>, <c>get_build_stats</c> and
/// <c>get_build</c>. None of them caps rows; callers page. No method returns UserName, Hostname or ExtraData
/// (except <see cref="GetAsync"/>, which returns ExtraData only).
/// </summary>
public interface IBuildQueries
{
    /// <summary>Columns <see cref="ListSeriesAsync"/> can sort by.</summary>
    static readonly string[] SeriesSortColumns = ["count", "avg_ms", "p50_ms", "p95_ms", "last_at"];

    /// <summary>Columns <see cref="GetStatsAsync"/> can group by.</summary>
    static readonly string[] StatsGroupColumns =
    [
        "repository", "project", "metric_type", "reload_type", "day", "week", "branch", "environment",
        "platform", "os", "cpu_count", "tool_version", "debugger_attached"
    ];

    /// <summary>Stat columns <see cref="GetStatsAsync"/> can sort by.</summary>
    static readonly string[] StatsStatColumns = ["count", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms"];

    /// <summary>
    /// One row per build series in the scope. Only <see cref="BuildFilter.MetricType"/>, <see cref="BuildFilter.BuildCategory"/>
    /// and <see cref="BuildFilter.ReloadType"/> are meant here, but every filter is honoured. <paramref name="sort"/>
    /// is one of <see cref="SeriesSortColumns"/> (default <c>count desc</c>); ties break by series id.
    /// </summary>
    Task<(IReadOnlyList<BuildSeriesRow> Rows, int Total)> ListSeriesAsync(
        InsightsScope scope, BuildFilter filter, SortSpec? sort, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Raw builds, newest first (ReceivedAt desc, Id desc). <paramref name="after"/> is the (ReceivedAt, Id) of the
    /// last row of the previous page. <c>HasMore</c> says whether a row follows this page. <c>Total</c> ignores <paramref name="after"/>.
    /// </summary>
    Task<(IReadOnlyList<BuildRow> Rows, int Total, bool HasMore)> GetBuildsAsync(
        InsightsScope scope, BuildFilter filter, (DateTime ReceivedAt, string Id)? after, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Statistics grouped by any of <see cref="StatsGroupColumns"/> (names must be valid; the caller validates).
    /// Sorted by a stat column or a group column; the default is the group columns ascending.
    /// </summary>
    Task<(IReadOnlyList<BuildStatsRow> Rows, int Total)> GetStatsAsync(
        InsightsScope scope, BuildFilter filter, IReadOnlyList<string> groupBy, SortSpec? sort, int offset, int limit,
        CancellationToken cancellationToken);

    /// <summary>One build and its raw ExtraData by id, regardless of the window; null when unknown.</summary>
    Task<BuildDetail?> GetAsync(string id, CancellationToken cancellationToken);
}

[RegisterPerRequest]
public sealed class BuildQueries : IBuildQueries
{
    private readonly TelemetryDbContext _db;

    public BuildQueries(TelemetryDbContext db)
    {
        _db = db;
    }

    // ---- list_build_series ----

    public async Task<(IReadOnlyList<BuildSeriesRow> Rows, int Total)> ListSeriesAsync(
        InsightsScope scope, BuildFilter filter, SortSpec? sort, int offset, int limit, CancellationToken cancellationToken)
    {
        sort ??= new SortSpec("count", true);
        var metrics = filter.Apply(scope.Apply(_db.BuildMetrics));

        // ReloadType is coalesced so a null and an empty reload type fall into the same group (one series).
        var groups = await metrics
            .GroupBy(b => new { b.RepositoryName, b.ProjectName, b.MetricType, ReloadType = b.ReloadType ?? "" })
            .Select(g => new
            {
                g.Key.RepositoryName,
                g.Key.ProjectName,
                g.Key.MetricType,
                g.Key.ReloadType,
                Category = g.Min(b => b.BuildCategory),
                Count = g.Count(),
                LastAt = g.Max(b => b.ReceivedAt),
                AvgMs = g.Average(b => b.TimeTakenMs)
            })
            .ToListAsync(cancellationToken);

        var series = groups
            .Select(g => (
                Row: new BuildSeriesRow(
                    StableId.Series(g.RepositoryName, g.ProjectName, g.MetricType, g.ReloadType),
                    g.RepositoryName, g.ProjectName, g.Category, g.MetricType,
                    g.ReloadType.Length == 0 ? null : g.ReloadType,
                    g.Count, DateTime.SpecifyKind(g.LastAt, DateTimeKind.Utc), g.AvgMs, 0, 0),
                Key: (g.RepositoryName, g.ProjectName, g.MetricType, g.ReloadType)))
            .ToList();

        var needsPercentiles = sort.Column is "p50_ms" or "p95_ms";
        Dictionary<(string, string, string, string), List<double>>? times = null;
        if (needsPercentiles)
        {
            // Sorting by a percentile needs every series' raw times: a narrow (key, TimeTakenMs) projection
            // for all series in scope, because SQL cannot compute a portable percentile.
            times = await LoadSeriesTimesAsync(metrics, null, cancellationToken);
            series = series.Select(s => (WithPercentiles(s.Row, times, s.Key), s.Key)).ToList();
        }

        IEnumerable<(BuildSeriesRow Row, (string, string, string, string) Key)> ordered = sort.Column switch
        {
            "avg_ms" => Order(series, s => s.Row.AvgMs, sort.Descending),
            "p50_ms" => Order(series, s => s.Row.P50Ms, sort.Descending),
            "p95_ms" => Order(series, s => s.Row.P95Ms, sort.Descending),
            "last_at" => Order(series, s => s.Row.LastAt, sort.Descending),
            _ => Order(series, s => s.Row.Count, sort.Descending)
        };

        var page = ordered.Skip(offset).Take(limit).ToList();
        if (!needsPercentiles && page.Count > 0)
        {
            // Percentiles only for the series on this page: a narrow projection limited to their repositories,
            // projects and metric types; exact keys are matched in memory.
            times = await LoadSeriesTimesAsync(metrics, page.Select(p => p.Key).ToList(), cancellationToken);
            page = page.Select(s => (WithPercentiles(s.Row, times, s.Key), s.Key)).ToList();
        }

        return (page.Select(p => p.Row).ToList(), series.Count);
    }

    private static BuildSeriesRow WithPercentiles(
        BuildSeriesRow row, Dictionary<(string, string, string, string), List<double>> times, (string, string, string, string) key) =>
        times.TryGetValue(key, out var values)
            ? row with { P50Ms = Stats.Percentile(values, 0.5) ?? 0, P95Ms = Stats.Percentile(values, 0.95) ?? 0 }
            : row;

    private static IOrderedEnumerable<(BuildSeriesRow Row, (string, string, string, string) Key)> Order<TKey>(
        IEnumerable<(BuildSeriesRow Row, (string, string, string, string) Key)> source,
        Func<(BuildSeriesRow Row, (string, string, string, string) Key), TKey> key, bool descending) =>
        descending
            ? source.OrderByDescending(key).ThenBy(s => s.Row.SeriesId, StringComparer.Ordinal)
            : source.OrderBy(key).ThenBy(s => s.Row.SeriesId, StringComparer.Ordinal);

    private static async Task<Dictionary<(string, string, string, string), List<double>>> LoadSeriesTimesAsync(
        IQueryable<BuildMetric> metrics, List<(string Repo, string Project, string Type, string Reload)>? only, CancellationToken ct)
    {
        if (only != null)
        {
            var repos = only.Select(k => k.Repo).Distinct().ToList();
            var projects = only.Select(k => k.Project).Distinct().ToList();
            var types = only.Select(k => k.Type).Distinct().ToList();
            metrics = metrics.Where(b => repos.Contains(b.RepositoryName) && projects.Contains(b.ProjectName) && types.Contains(b.MetricType));
        }

        var samples = await metrics
            .Select(b => new { b.RepositoryName, b.ProjectName, b.MetricType, ReloadType = b.ReloadType ?? "", b.TimeTakenMs })
            .ToListAsync(ct);
        return samples
            .GroupBy(s => (s.RepositoryName, s.ProjectName, s.MetricType, s.ReloadType))
            .ToDictionary(g => g.Key, g => g.Select(s => s.TimeTakenMs).ToList());
    }

    // ---- get_builds / get_build ----

    private static readonly Expression<Func<BuildMetric, BuildRow>> RowProjection = b => new BuildRow(
        b.Id, b.ReceivedAt, b.RepositoryName, b.ProjectName, b.BuildCategory, b.MetricType, b.ReloadType, b.TimeTakenMs,
        b.Branch, b.ExecutionEnvironment, b.Platform, b.Os, b.CpuCount, b.ToolVersion, b.IsDebuggerAttached, b.CommitSha);

    public async Task<(IReadOnlyList<BuildRow> Rows, int Total, bool HasMore)> GetBuildsAsync(
        InsightsScope scope, BuildFilter filter, (DateTime ReceivedAt, string Id)? after, int limit, CancellationToken cancellationToken)
    {
        var metrics = filter.Apply(scope.Apply(_db.BuildMetrics));
        var total = await metrics.CountAsync(cancellationToken);

        if (after.HasValue)
        {
            var (at, id) = after.Value;
            // Keyset on (ReceivedAt desc, Id desc): rows strictly after the cursor row in that order.
            metrics = metrics.Where(b => b.ReceivedAt < at || (b.ReceivedAt == at && string.Compare(b.Id, id) < 0));
        }

        var rows = await metrics
            .OrderByDescending(b => b.ReceivedAt)
            .ThenByDescending(b => b.Id)
            .Take(limit + 1)
            .Select(RowProjection)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = rows.Take(limit).Select(NormaliseRow).ToList();
        return (page, total, hasMore);
    }

    public async Task<BuildDetail?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var row = await _db.BuildMetrics.Where(b => b.Id == id).Select(RowProjection).FirstOrDefaultAsync(cancellationToken);
        if (row == null) return null;
        var extra = await _db.BuildMetrics.Where(b => b.Id == id).Select(b => b.ExtraData).FirstOrDefaultAsync(cancellationToken);
        return new BuildDetail(NormaliseRow(row), extra);
    }

    private static BuildRow NormaliseRow(BuildRow r) => r with
    {
        ReceivedAt = DateTime.SpecifyKind(r.ReceivedAt, DateTimeKind.Utc),
        ReloadType = string.IsNullOrEmpty(r.ReloadType) ? null : r.ReloadType
    };

    // ---- get_build_stats ----

    /// <summary>The SQL group key. Only the properties of the requested dimensions are bound, so only they are grouped on.</summary>
    public sealed class StatsKey
    {
        public string? Repository { get; set; }
        public string? Project { get; set; }
        public string? MetricType { get; set; }
        public string? ReloadType { get; set; }
        public DateTime? Day { get; set; }
        public string? Branch { get; set; }
        public string? Environment { get; set; }
        public string? Platform { get; set; }
        public string? Os { get; set; }
        public int? CpuCount { get; set; }
        public string? ToolVersion { get; set; }
        public bool? DebuggerAttached { get; set; }
    }

    public sealed class StatsSample
    {
        public StatsKey Key { get; set; } = new();
        public double Time { get; set; }
    }

    // dimension -> (StatsKey property, source expression over the BuildMetric parameter)
    private static (string Property, Expression Source) Dimension(string dim, ParameterExpression b)
    {
        Expression P(string name) => Expression.Property(b, name);
        return dim switch
        {
            "repository" => (nameof(StatsKey.Repository), P(nameof(BuildMetric.RepositoryName))),
            "project" => (nameof(StatsKey.Project), P(nameof(BuildMetric.ProjectName))),
            "metric_type" => (nameof(StatsKey.MetricType), P(nameof(BuildMetric.MetricType))),
            // A null and an empty reload type are one series, so group them together.
            "reload_type" => (nameof(StatsKey.ReloadType),
                Expression.Coalesce(P(nameof(BuildMetric.ReloadType)), Expression.Constant(string.Empty))),
            // day and week both group by UTC day in SQL; weeks are rolled up from days in memory.
            "day" or "week" => (nameof(StatsKey.Day),
                Expression.Convert(Expression.Property(P(nameof(BuildMetric.ReceivedAt)), nameof(DateTime.Date)), typeof(DateTime?))),
            "branch" => (nameof(StatsKey.Branch), P(nameof(BuildMetric.Branch))),
            "environment" => (nameof(StatsKey.Environment), P(nameof(BuildMetric.ExecutionEnvironment))),
            "platform" => (nameof(StatsKey.Platform), P(nameof(BuildMetric.Platform))),
            "os" => (nameof(StatsKey.Os), P(nameof(BuildMetric.Os))),
            "cpu_count" => (nameof(StatsKey.CpuCount), Expression.Convert(P(nameof(BuildMetric.CpuCount)), typeof(int?))),
            "tool_version" => (nameof(StatsKey.ToolVersion), P(nameof(BuildMetric.ToolVersion))),
            "debugger_attached" => (nameof(StatsKey.DebuggerAttached),
                Expression.Convert(P(nameof(BuildMetric.IsDebuggerAttached)), typeof(bool?))),
            _ => throw new InsightsArgumentException("group_by", $"'{dim}' is not a group column; use one of: {string.Join(", ", IBuildQueries.StatsGroupColumns)}")
        };
    }

    private static MemberInitExpression KeyBody(IReadOnlyList<string> groupBy, ParameterExpression b)
    {
        var bindings = new List<MemberBinding>();
        var seen = new HashSet<string>();
        foreach (var dim in groupBy)
        {
            var (property, source) = Dimension(dim, b);
            if (seen.Add(property))
                bindings.Add(Expression.Bind(typeof(StatsKey).GetProperty(property)!, source));
        }
        return Expression.MemberInit(Expression.New(typeof(StatsKey)), bindings);
    }

    private static object? Extract(string dim, StatsKey k) => dim switch
    {
        "repository" => k.Repository,
        "project" => k.Project,
        "metric_type" => k.MetricType,
        "reload_type" => string.IsNullOrEmpty(k.ReloadType) ? null : k.ReloadType,
        "day" => Format(k.Day!.Value),
        "week" => Format(Monday(k.Day!.Value)),
        "branch" => k.Branch,
        "environment" => k.Environment,
        "platform" => k.Platform,
        "os" => k.Os,
        "cpu_count" => k.CpuCount,
        "tool_version" => k.ToolVersion,
        "debugger_attached" => k.DebuggerAttached,
        _ => null
    };

    private static string Format(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The Monday of the ISO week containing <paramref name="d"/>.</summary>
    private static DateTime Monday(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static string Signature(IReadOnlyList<object?> values) =>
        string.Join("\u001f", values.Select(v => v == null ? "\u0000" : Convert.ToString(v, CultureInfo.InvariantCulture)));

    public async Task<(IReadOnlyList<BuildStatsRow> Rows, int Total)> GetStatsAsync(
        InsightsScope scope, BuildFilter filter, IReadOnlyList<string> groupBy, SortSpec? sort, int offset, int limit,
        CancellationToken cancellationToken)
    {
        if (groupBy.Count == 0) throw new InsightsArgumentException("group_by", "is required");
        var metrics = filter.Apply(scope.Apply(_db.BuildMetrics));

        var p = Expression.Parameter(typeof(BuildMetric), "b");
        var keyLambda = Expression.Lambda<Func<BuildMetric, StatsKey>>(KeyBody(groupBy, p), p);

        // Count, sum and max are grouped in SQL; avg is total / count. Percentiles are added below.
        var aggregates = await metrics
            .GroupBy(keyLambda)
            .Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(b => b.TimeTakenMs), Max = g.Max(b => b.TimeTakenMs) })
            .ToListAsync(cancellationToken);

        // Roll the SQL groups up to the requested dimensions (this merges days into weeks; count, sum and max combine exactly).
        var merged = aggregates
            .Select(a => (Values: (IReadOnlyList<object?>)groupBy.Select(d => Extract(d, a.Key)).ToList(), a.Count, a.Total, a.Max))
            .GroupBy(a => Signature(a.Values))
            .Select(g => (
                Signature: g.Key,
                g.First().Values,
                Count: g.Sum(x => (long)x.Count),
                Total: g.Sum(x => x.Total),
                Max: g.Max(x => x.Max)))
            .ToList();

        var spec = sort;
        IEnumerable<(string Signature, IReadOnlyList<object?> Values, long Count, double Total, double Max)> ordered;
        Dictionary<string, List<double>>? times = null;
        var groupIndex = spec == null ? -1 : IndexOf(groupBy, spec.Column);
        if (spec != null && spec.Column is "p50_ms" or "p95_ms")
        {
            // Sorting by a percentile needs every group's raw times: one narrow (group dims, TimeTakenMs)
            // projection for the whole scope, because SQL cannot compute a portable percentile.
            times = await LoadTimesAsync(metrics, groupBy, null, cancellationToken);
            var pct = spec.Column == "p50_ms" ? 0.5 : 0.95;
            var keyed = merged.Select(m => (m, P: Stats.Percentile(times.GetValueOrDefault(m.Signature) ?? [], pct) ?? 0)).ToList();
            ordered = (spec.Descending ? keyed.OrderByDescending(k => k.P) : keyed.OrderBy(k => k.P))
                .ThenBy(k => k.m.Values, ValuesComparer.Instance).Select(k => k.m);
        }
        else if (spec != null && groupIndex >= 0)
        {
            ordered = (spec.Descending
                ? merged.OrderByDescending(m => m.Values[groupIndex], ValueComparer.Instance)
                : merged.OrderBy(m => m.Values[groupIndex], ValueComparer.Instance))
                .ThenBy(m => m.Values, ValuesComparer.Instance);
        }
        else if (spec != null)
        {
            ordered = (spec.Column switch
            {
                "avg_ms" => Order(merged, m => m.Total / m.Count, spec.Descending),
                "max_ms" => Order(merged, m => m.Max, spec.Descending),
                "total_ms" => Order(merged, m => m.Total, spec.Descending),
                _ => Order(merged, m => (double)m.Count, spec.Descending)
            }).ThenBy(m => m.Values, ValuesComparer.Instance);
        }
        else
        {
            ordered = merged.OrderBy(m => m.Values, ValuesComparer.Instance);
        }

        var page = ordered.Skip(offset).Take(limit).ToList();
        if (page.Count > 0 && times == null)
            times = await LoadTimesAsync(metrics, groupBy, page.Select(m => m.Values).ToList(), cancellationToken);

        var rows = page.Select(m =>
        {
            var values = times?.GetValueOrDefault(m.Signature) ?? [];
            return new BuildStatsRow(groupBy, m.Values, m.Count, m.Total / m.Count,
                Stats.Percentile(values, 0.5) ?? 0, Stats.Percentile(values, 0.95) ?? 0, m.Max, m.Total);
        }).ToList();
        return (rows, merged.Count);
    }

    private static int IndexOf(IReadOnlyList<string> list, string item)
    {
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i], item, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static IOrderedEnumerable<T> Order<T>(IEnumerable<T> source, Func<T, double> key, bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);

    /// <summary>
    /// Raw build times per group, for percentiles: a narrow projection of (group dims, TimeTakenMs). With
    /// <paramref name="only"/> it is limited to the page's groups where the dimension allows an SQL filter.
    /// </summary>
    private static async Task<Dictionary<string, List<double>>> LoadTimesAsync(
        IQueryable<BuildMetric> metrics, IReadOnlyList<string> groupBy, List<IReadOnlyList<object?>>? only, CancellationToken ct)
    {
        var b = Expression.Parameter(typeof(BuildMetric), "b");
        if (only != null)
            metrics = Restrict(metrics, groupBy, only);

        var sample = Expression.Lambda<Func<BuildMetric, StatsSample>>(
            Expression.MemberInit(Expression.New(typeof(StatsSample)),
                Expression.Bind(typeof(StatsSample).GetProperty(nameof(StatsSample.Key))!, KeyBody(groupBy, b)),
                Expression.Bind(typeof(StatsSample).GetProperty(nameof(StatsSample.Time))!, Expression.Property(b, nameof(BuildMetric.TimeTakenMs)))),
            b);

        var samples = await metrics.Select(sample).ToListAsync(ct);
        return samples
            .GroupBy(s => Signature(groupBy.Select(d => Extract(d, s.Key)).ToList()))
            .ToDictionary(g => g.Key, g => g.Select(s => s.Time).ToList());
    }

    private static IQueryable<BuildMetric> Restrict(IQueryable<BuildMetric> metrics, IReadOnlyList<string> groupBy, List<IReadOnlyList<object?>> only)
    {
        for (var i = 0; i < groupBy.Count; i++)
        {
            var index = i;
            var distinct = only.Select(v => v[index]).Distinct().ToList();
            switch (groupBy[i])
            {
                case "repository": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.RepositoryName)); break; }
                case "project": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.ProjectName)); break; }
                case "metric_type": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.MetricType)); break; }
                case "branch": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.Branch)); break; }
                case "environment": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.ExecutionEnvironment)); break; }
                case "platform": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.Platform)); break; }
                case "os": { var l = Strings(distinct); metrics = metrics.Where(m => l.Contains(m.Os)); break; }
                case "cpu_count": { var l = distinct.OfType<int>().ToList(); metrics = metrics.Where(m => l.Contains(m.CpuCount)); break; }
                case "day" or "week":
                {
                    var days = distinct.Select(v => DateTime.ParseExact((string)v!, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)).ToList();
                    var from = days.Min();
                    var to = days.Max().AddDays(groupBy[i] == "week" ? 7 : 1);
                    metrics = metrics.Where(m => m.ReceivedAt >= from && m.ReceivedAt < to);
                    break;
                }
                // reload_type, tool_version, debugger_attached: matched in memory.
            }
        }
        return metrics;
    }

    private static List<string> Strings(List<object?> values) => values.OfType<string>().ToList();

    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;
            return x is string a && y is string b
                ? string.CompareOrdinal(a, b)
                : ((IComparable)x).CompareTo(y);
        }
    }

    private sealed class ValuesComparer : IComparer<IReadOnlyList<object?>>
    {
        public static readonly ValuesComparer Instance = new();

        public int Compare(IReadOnlyList<object?>? x, IReadOnlyList<object?>? y)
        {
            for (var i = 0; i < x!.Count; i++)
            {
                var c = ValueComparer.Instance.Compare(x[i], y![i]);
                if (c != 0) return c;
            }
            return 0;
        }
    }
}
