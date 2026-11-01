using System.Globalization;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One duration sample: when the run was received (UTC) and how long it took.</summary>
public readonly record struct DurationSample(DateTime At, double DurationMs);

/// <summary>The outcome of comparing the baseline and current samples of one group.</summary>
public sealed record WindowComparison(
    int BaselineSamples,
    int CurrentSamples,
    double BeforeP50Ms,
    double AfterP50Ms,
    DateTime? ShiftStartedOn);

/// <summary>
/// The statistics shared by the test and suite regression detectors: p50 per window, the threshold test and the
/// first day of the current window whose daily median reaches the threshold. All in memory, over narrow samples
/// the detectors loaded only for the groups that already passed the SQL sample-count filter.
/// </summary>
public static class RegressionMath
{
    /// <summary>
    /// Compares the samples of one group, or returns null when it is not a regression: fewer than
    /// <see cref="RegressionQuery.MinSamples"/> samples in a window, a non-positive baseline p50, or
    /// p50(current) below <see cref="RegressionQuery.Threshold"/>(p50(baseline)).
    /// </summary>
    public static WindowComparison? Compare(IReadOnlyCollection<DurationSample> samples, RegressionQuery query)
    {
        var baseline = samples.Where(s => s.At < query.CurrentFrom).ToList();
        var current = samples.Where(s => s.At >= query.CurrentFrom).ToList();
        if (baseline.Count < query.MinSamples || current.Count < query.MinSamples) return null;

        var before = Stats.Percentile(baseline.Select(s => s.DurationMs), 0.5)!.Value;
        var after = Stats.Percentile(current.Select(s => s.DurationMs), 0.5)!.Value;
        if (before <= 0) return null;

        var threshold = query.Threshold(before);
        if (after < threshold) return null;

        DateTime? shift = current
            .GroupBy(s => s.At.Date)
            .OrderBy(g => g.Key)
            .Where(g => Stats.Percentile(g.Select(s => s.DurationMs), 0.5)!.Value >= threshold)
            .Select(g => (DateTime?)DateTime.SpecifyKind(g.Key, DateTimeKind.Utc))
            .FirstOrDefault();

        return new WindowComparison(baseline.Count, current.Count, before, after, shift);
    }

    /// <summary>
    /// "p50 120 ms → 310 ms (+158%) since 2026-03-14; 12 vs 9 passing runs". <paramref name="sampleLabel"/> is
    /// appended after the counts, for example "passing runs".
    /// </summary>
    public static string Reason(WindowComparison c, string sampleLabel)
    {
        var before = Stats.Ms(c.BeforeP50Ms)!.Value;
        var after = Stats.Ms(c.AfterP50Ms)!.Value;
        var pct = (c.AfterP50Ms - c.BeforeP50Ms) / c.BeforeP50Ms * 100.0;
        var since = c.ShiftStartedOn.HasValue
            ? " since " + c.ShiftStartedOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;
        return string.Format(CultureInfo.InvariantCulture,
            "p50 {0} ms → {1} ms (+{2:0}%){3}; {4} vs {5} {6}",
            before, after, pct, since, c.BaselineSamples, c.CurrentSamples, sampleLabel);
    }
}
