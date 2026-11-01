using System.Globalization;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Insights.Builds;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// The <c>build</c> detector of <c>find_regressions</c>: groups are build series, samples are TimeTakenMs.
/// SQL counts pick the series with enough samples in both windows; only those are loaded (a narrow projection)
/// so the p50s and daily medians, which SQL cannot compute portably, are computed in memory.
/// </summary>
[RegisterPerRequest(For = typeof(IRegressionDetector))]
public sealed class BuildRegressionDetector : IRegressionDetector
{
    private readonly TelemetryDbContext _db;

    public BuildRegressionDetector(TelemetryDbContext db)
    {
        _db = db;
    }

    public string Kind => "build";

    public async Task<IReadOnlyList<RegressionRow>> FindAsync(RegressionQuery query, CancellationToken cancellationToken)
    {
        var currentFrom = query.CurrentFrom;
        var metrics = query.Scope.Apply(_db.BuildMetrics);

        var counts = await metrics
            .GroupBy(b => new { b.RepositoryName, b.ProjectName, b.MetricType, ReloadType = b.ReloadType ?? "" })
            .Select(g => new
            {
                g.Key.RepositoryName,
                g.Key.ProjectName,
                g.Key.MetricType,
                g.Key.ReloadType,
                Baseline = g.Sum(b => b.ReceivedAt < currentFrom ? 1 : 0),
                Current = g.Sum(b => b.ReceivedAt >= currentFrom ? 1 : 0)
            })
            .ToListAsync(cancellationToken);

        var eligible = counts
            .Where(c => c.Baseline >= query.MinSamples && c.Current >= query.MinSamples)
            .ToList();
        if (eligible.Count == 0) return [];

        var repos = eligible.Select(c => c.RepositoryName).Distinct().ToList();
        var projects = eligible.Select(c => c.ProjectName).Distinct().ToList();
        var types = eligible.Select(c => c.MetricType).Distinct().ToList();
        var samples = await metrics
            .Where(b => repos.Contains(b.RepositoryName) && projects.Contains(b.ProjectName) && types.Contains(b.MetricType))
            .Select(b => new { b.RepositoryName, b.ProjectName, b.MetricType, ReloadType = b.ReloadType ?? "", b.ReceivedAt, b.TimeTakenMs })
            .ToListAsync(cancellationToken);
        var bySeries = samples.ToLookup(s => (s.RepositoryName, s.ProjectName, s.MetricType, s.ReloadType));

        var rows = new List<RegressionRow>();
        foreach (var series in eligible)
        {
            var all = bySeries[(series.RepositoryName, series.ProjectName, series.MetricType, series.ReloadType)].ToList();
            var baseline = all.Where(s => s.ReceivedAt < currentFrom).Select(s => s.TimeTakenMs).ToList();
            var current = all.Where(s => s.ReceivedAt >= currentFrom).ToList();

            var before = Stats.Percentile(baseline, 0.5) ?? 0;
            var after = Stats.Percentile(current.Select(s => s.TimeTakenMs), 0.5) ?? 0;
            if (before <= 0) continue;
            var threshold = query.Threshold(before);
            if (after < threshold) continue;

            var shift = current
                .GroupBy(s => s.ReceivedAt.Date)
                .OrderBy(g => g.Key)
                .Where(g => (Stats.Percentile(g.Select(s => s.TimeTakenMs), 0.5) ?? 0) >= threshold)
                .Select(g => (DateTime?)DateTime.SpecifyKind(g.Key, DateTimeKind.Utc))
                .FirstOrDefault();

            var seriesId = StableId.Series(series.RepositoryName, series.ProjectName, series.MetricType, series.ReloadType);
            var reload = series.ReloadType.Length == 0 ? null : series.ReloadType;
            var changePct = (after - before) / before * 100.0;
            var since = shift.HasValue ? $" since {shift.Value:yyyy-MM-dd}" : "";
            rows.Add(new RegressionRow(
                Kind, seriesId, series.RepositoryName, series.ProjectName,
                reload == null ? series.MetricType : $"{series.MetricType} ({reload})",
                series.Baseline, series.Current, before, after, shift,
                $"p50 {Stats.Ms(before)} ms → {Stats.Ms(after)} ms ({changePct.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)}%){since}; " +
                $"{series.Baseline} baseline vs {series.Current} current builds",
                Evidence.Call(DatasetNames.GET_BUILD_STATS,
                    Evidence.ScopeArgs(query.Scope).With("series_id", seriesId).With("group_by", "day"))));
        }
        return rows;
    }
}
