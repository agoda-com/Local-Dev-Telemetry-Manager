using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights.Builds;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One group of one top dimension of <c>explain_build_time</c>.</summary>
public sealed record ExplainBuildRow(
    string Dimension,
    string Value,
    long Count,
    double AvgMs,
    double P50Ms,
    double P95Ms,
    double? RatioToFastest,
    double DimensionSpread,
    int DimensionRank,
    string Reason,
    string Evidence);

/// <summary>
/// <c>explain_build_time</c>: for one build series, ranks the dimensions (cpu_count, os, platform, tool_version,
/// branch, environment, debugger_attached) by the p50 gap between their slowest and fastest group, counting only
/// groups with at least <c>min_samples</c> builds.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class BuildExplainDataset : IInsightsDataset
{
    public const int DEFAULT_MIN_SAMPLES = 5;
    public const int DEFAULT_TOP_DIMENSIONS = 3;

    public static readonly string[] Dimensions =
        ["cpu_count", "os", "platform", "tool_version", "branch", "environment", "debugger_attached"];

    private static readonly TableSchema<ExplainBuildRow> Schema = new(
        ("dimension", r => r.Dimension),
        ("value", r => r.Value),
        ("count", r => r.Count),
        ("avg_ms", r => Stats.Ms(r.AvgMs)),
        ("p50_ms", r => Stats.Ms(r.P50Ms)),
        ("p95_ms", r => Stats.Ms(r.P95Ms)),
        ("ratio_to_fastest", r => r.RatioToFastest),
        ("dimension_spread", r => Math.Round(r.DimensionSpread, 4)),
        ("dimension_rank", r => r.DimensionRank),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private readonly IBuildQueries _queries;
    private readonly IBuildSeriesCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public BuildExplainDataset(IBuildQueries queries, IBuildSeriesCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.EXPLAIN_BUILD_TIME;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var seriesId = args.GetString("series_id")
            ?? throw new InsightsArgumentException("series_id", "is required; list_build_series shows the series ids");
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var minSamples = args.GetInt("min_samples", DEFAULT_MIN_SAMPLES);
        if (minSamples < 1) throw new InsightsArgumentException("min_samples", "must be at least 1");
        var top = args.GetInt("top_dimensions", DEFAULT_TOP_DIMENSIONS);
        if (top < 1) throw new InsightsArgumentException("top_dimensions", "must be at least 1");

        var series = await _catalog.ResolveAsync(seriesId, cancellationToken);
        var filter = new BuildFilter { Series = series };

        var candidates = new List<(string Dimension, List<BuildStatsRow> Groups, double Spread)>();
        foreach (var dimension in Dimensions)
        {
            var (groups, _) = await _queries.GetStatsAsync(scope, filter, [dimension], null, 0, int.MaxValue, cancellationToken);
            var qualifying = groups
                .Where(g => g.Count >= minSamples)
                .OrderByDescending(g => g.P50Ms)
                .ThenBy(g => Label(g.Values[0]), StringComparer.Ordinal)
                .ToList();
            if (qualifying.Count < 2) continue;
            var fastest = qualifying[^1].P50Ms;
            if (fastest <= 0) continue;
            candidates.Add((dimension, qualifying, qualifying[0].P50Ms / fastest));
        }

        // OrderByDescending is stable, so equal spreads keep the dimension order above.
        var chosen = candidates.OrderByDescending(c => c.Spread).Take(top).ToList();
        var rows = new List<ExplainBuildRow>();
        for (var rank = 0; rank < chosen.Count; rank++)
        {
            var (dimension, groups, spread) = chosen[rank];
            var fastest = groups[^1];
            var evidence = Evidence.Call(DatasetNames.GET_BUILD_STATS,
                Evidence.ScopeArgs(scope).With("series_id", series.SeriesId).With("group_by", dimension));
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                rows.Add(new ExplainBuildRow(
                    dimension, Label(g.Values[0]), g.Count, g.AvgMs, g.P50Ms, g.P95Ms,
                    Stats.Ratio(g.P50Ms, fastest.P50Ms), spread, rank + 1,
                    ReasonFor(dimension, g, i == 0, i == groups.Count - 1, fastest, g.P50Ms / fastest.P50Ms), evidence));
            }
        }

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageRows = rows.Skip(offset).Take(page.Limit).ToList();
        var notes = rows.Count == 0
            ? new[] { $"No dimension has two groups with at least {minSamples} builds, so the spread looks like noise; try more days or a lower min_samples." }
            : [];
        return Schema.ToTable(pageRows, rows.Count, Cursor.NextOffset(offset, pageRows.Count, rows.Count)) with
        {
            Window = scope.Window,
            Notes = notes,
            Applied = $"{scope.Describe()} series_id={series.SeriesId} min_samples={minSamples} top_dimensions={top}"
        };
    }

    private static string Label(object? value) => value switch
    {
        null => "(none)",
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "(none)"
    };

    private static string Describe(string dimension, object? value) => dimension switch
    {
        "cpu_count" => $"{Label(value)}-core machines",
        "os" => $"os {Label(value)}",
        "platform" => $"platform {Label(value)}",
        "tool_version" => $"tool version {Label(value)}",
        "branch" => $"branch {Label(value)}",
        "environment" => $"{Label(value)} environment",
        "debugger_attached" => value is true ? "builds with a debugger attached" : "builds without a debugger",
        _ => $"{dimension} {Label(value)}"
    };

    private static string ReasonFor(string dimension, BuildStatsRow group, bool slowest, bool fastest, BuildStatsRow fastestGroup, double ratio)
    {
        var name = Describe(dimension, group.Values[0]);
        var ms = Stats.Ms(group.P50Ms);
        if (fastest) return $"{name}: fastest, p50 {ms} ms";
        var times = ratio.ToString("0.0", CultureInfo.InvariantCulture);
        return slowest
            ? $"{name}: p50 {times}× the {Describe(dimension, fastestGroup.Values[0])} (p50 {ms} ms vs {Stats.Ms(fastestGroup.P50Ms)} ms)"
            : $"{name}: p50 {times}× the fastest ({ms} ms)";
    }
}
