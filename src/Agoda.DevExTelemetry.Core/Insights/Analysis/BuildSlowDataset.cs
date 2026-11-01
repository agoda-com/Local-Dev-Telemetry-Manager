using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights.Builds;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One ranked series of <c>find_slow_builds</c>: the <c>list_build_series</c> row plus its trend.</summary>
public sealed record SlowBuildRow(BuildSeriesRow Series, double? TrendPct, string Reason, string Evidence);

/// <summary>
/// <c>find_slow_builds</c>: build series ranked by p50 (default) or p95. The trend is the p50 of the second half
/// of the window against the first half, as a % change; null when either half has fewer than <c>min_samples</c> builds.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class BuildSlowDataset : IInsightsDataset
{
    public const int DEFAULT_MIN_SAMPLES = 10;

    private static readonly TableSchema<SlowBuildRow> Schema = new(
        ("series_id", r => r.Series.SeriesId),
        ("repository", r => r.Series.Repository),
        ("project", r => r.Series.Project),
        ("build_category", r => r.Series.BuildCategory),
        ("metric_type", r => r.Series.MetricType),
        ("reload_type", r => r.Series.ReloadType),
        ("count", r => r.Series.Count),
        ("last_at", r => r.Series.LastAt),
        ("avg_ms", r => Stats.Ms(r.Series.AvgMs)),
        ("p50_ms", r => Stats.Ms(r.Series.P50Ms)),
        ("p95_ms", r => Stats.Ms(r.Series.P95Ms)),
        ("trend_pct", r => r.TrendPct.HasValue ? Math.Round(r.TrendPct.Value, 1) : null),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private static readonly string[] SeriesDimensions = ["repository", "project", "metric_type", "reload_type"];

    private readonly IBuildQueries _queries;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public BuildSlowDataset(IBuildQueries queries, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.FIND_SLOW_BUILDS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var sortBy = args.GetChoice("sort_by", "p50", "p95") ?? "p50";
        var minSamples = args.GetInt("min_samples", DEFAULT_MIN_SAMPLES);
        if (minSamples < 1) throw new InsightsArgumentException("min_samples", "must be at least 1");
        var filter = new BuildFilter
        {
            MetricType = args.GetString("metric_type"),
            BuildCategory = args.GetString("build_category"),
            ReloadType = args.GetString("reload_type")
        };

        // Rank every series (the query sorts by the percentile and breaks ties by series id), then drop the small ones.
        var (all, _) = await _queries.ListSeriesAsync(scope, filter, new SortSpec(sortBy + "_ms", true), 0, int.MaxValue, cancellationToken);
        var ranked = all.Where(s => s.Count >= minSamples).ToList();

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageSeries = ranked.Skip(offset).Take(page.Limit).ToList();

        var trends = pageSeries.Count == 0
            ? new Dictionary<string, double?>()
            : await TrendsAsync(scope, filter, minSamples, cancellationToken);

        var rows = pageSeries.Select(s =>
        {
            var trend = trends.GetValueOrDefault(s.SeriesId);
            return new SlowBuildRow(s, trend, Reason(s, sortBy, trend, minSamples), EvidenceFor(scope, s));
        }).ToList();

        return Schema.ToTable(rows, ranked.Count, Cursor.NextOffset(offset, rows.Count, ranked.Count)) with
        {
            Window = scope.Window,
            Applied = string.Join(" ", new[] { scope.Describe(), filter.Describe(), $"sort_by={sortBy} min_samples={minSamples}" }
                .Where(s => s.Length > 0))
        };
    }

    /// <summary>
    /// p50 of the second half against the first half per series: two grouped stats calls (one per half of the
    /// window) rather than one call per series. Series with fewer than <paramref name="minSamples"/> in a half get null.
    /// </summary>
    private async Task<Dictionary<string, double?>> TrendsAsync(
        InsightsScope scope, BuildFilter filter, int minSamples, CancellationToken ct)
    {
        var middle = scope.Window.From + (scope.Window.To - scope.Window.From) / 2;
        var first = scope with { Window = new InsightsWindow(scope.Window.From, middle.AddTicks(-1)) };
        var second = scope with { Window = new InsightsWindow(middle, scope.Window.To) };

        var (firstRows, _) = await _queries.GetStatsAsync(first, filter, SeriesDimensions, null, 0, int.MaxValue, ct);
        var (secondRows, _) = await _queries.GetStatsAsync(second, filter, SeriesDimensions, null, 0, int.MaxValue, ct);

        var before = firstRows.ToDictionary(SeriesIdOf, r => r);
        var result = new Dictionary<string, double?>();
        foreach (var after in secondRows)
        {
            var id = SeriesIdOf(after);
            if (!before.TryGetValue(id, out var earlier)) continue;
            if (earlier.Count < minSamples || after.Count < minSamples || earlier.P50Ms <= 0) continue;
            result[id] = (after.P50Ms - earlier.P50Ms) / earlier.P50Ms * 100.0;
        }
        return result;
    }

    private static string SeriesIdOf(BuildStatsRow r) =>
        StableId.Series((string)r.Values[0]!, (string)r.Values[1]!, (string)r.Values[2]!, r.Values[3] as string);

    private static string Reason(BuildSeriesRow s, string sortBy, double? trend, int minSamples)
    {
        var ms = sortBy == "p95" ? s.P95Ms : s.P50Ms;
        var other = sortBy == "p95" ? $"p50 {Stats.Ms(s.P50Ms)} ms" : $"p95 {Stats.Ms(s.P95Ms)} ms";
        var trendText = trend.HasValue
            ? $"p50 {trend.Value.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)}% in the second half of the window"
            : $"no trend (fewer than {minSamples} builds in a half of the window)";
        return $"{sortBy} {Stats.Ms(ms)} ms over {s.Count} builds ({other}); {trendText}";
    }

    private static string EvidenceFor(InsightsScope scope, BuildSeriesRow s) =>
        Evidence.Call(DatasetNames.GET_BUILD_STATS,
            Evidence.ScopeArgs(scope).With("series_id", s.SeriesId).With("group_by", "day"));
}
