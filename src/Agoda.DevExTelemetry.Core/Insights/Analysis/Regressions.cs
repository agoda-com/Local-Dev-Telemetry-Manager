using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// The windows and thresholds of one <c>find_regressions</c> call. The current window is
/// [<see cref="CurrentFrom"/>, <see cref="To"/>]; the baseline is [<see cref="BaselineFrom"/>, <see cref="CurrentFrom"/>).
/// <see cref="Scope"/> carries repository/project/branch/environment, and its window spans both.
/// </summary>
public sealed record RegressionQuery(
    InsightsScope Scope,
    DateTime BaselineFrom,
    DateTime CurrentFrom,
    DateTime To,
    double MinIncreasePct,
    int MinSamples)
{
    /// <summary>The p50 a current window must reach to count: before × (1 + min_increase_pct / 100).</summary>
    public double Threshold(double beforeP50) => beforeP50 * (1 + MinIncreasePct / 100.0);
}

/// <summary>
/// One flagged group. <see cref="Kind"/> is <c>build</c> (a build series), <c>test</c> (a test key) or
/// <c>suite</c> (whole suite runs, by run duration).
/// </summary>
public sealed record RegressionRow(
    string Kind,
    string SubjectId,
    string Repository,
    string Project,
    string Name,
    int BaselineSamples,
    int CurrentSamples,
    double BeforeP50Ms,
    double AfterP50Ms,
    DateTime? ShiftStartedOn,
    string Reason,
    string Evidence)
{
    public double ChangePct => BeforeP50Ms <= 0 ? 0 : (AfterP50Ms - BeforeP50Ms) / BeforeP50Ms * 100.0;
}

/// <summary>
/// Finds regressions of one kind. Rules every detector follows (issue #27, tool 18):
/// <list type="bullet">
/// <item>Flag a group when p50(current) ≥ <see cref="RegressionQuery.Threshold"/>(p50(baseline)) and both windows
/// have ≥ <see cref="RegressionQuery.MinSamples"/> samples.</item>
/// <item><c>ShiftStartedOn</c> is the first UTC day in the current window whose median is at or above the threshold
/// (the date at 00:00 UTC), or null if no single day is.</item>
/// <item>p50 is <see cref="Stats.Percentile"/> at 0.5. Load only narrow (group, timestamp, duration) projections.</item>
/// <item><c>Reason</c>: one line, e.g. "p50 1000 ms → 1350 ms (+35%) since 2026-03-14; 40 vs 30 samples".</item>
/// <item><c>Evidence</c>: a data tool call via <see cref="Analysis.Evidence.Call"/> covering baseline and current,
/// e.g. <c>get_build_stats series_id=… group_by=day from=… to=…</c>, <c>get_test test_key=… from=… to=…</c>,
/// <c>list_test_runs suite_id=… from=… to=…</c>.</item>
/// </list>
/// Register implementations with <c>[RegisterPerRequest(For = typeof(IRegressionDetector))]</c>.
/// </summary>
public interface IRegressionDetector
{
    /// <summary><c>build</c>, <c>test</c> or <c>suite</c>.</summary>
    string Kind { get; }

    Task<IReadOnlyList<RegressionRow>> FindAsync(RegressionQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// <c>find_regressions</c>: runs every <see cref="IRegressionDetector"/> (or those of <c>kind</c>) and ranks
/// the flagged groups by change %.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class FindRegressionsDataset : IInsightsDataset
{
    public const int DEFAULT_CURRENT_DAYS = 7;
    public const int DEFAULT_BASELINE_DAYS = 28;
    public const double DEFAULT_MIN_INCREASE_PCT = 20;
    public const int DEFAULT_MIN_SAMPLES = 10;

    private static readonly TableSchema<RegressionRow> Schema = new(
        ("kind", r => r.Kind),
        ("subject_id", r => r.SubjectId),
        ("repository", r => r.Repository),
        ("project", r => r.Project),
        ("name", r => r.Name),
        ("baseline_samples", r => r.BaselineSamples),
        ("current_samples", r => r.CurrentSamples),
        ("before_p50_ms", r => Stats.Ms(r.BeforeP50Ms)),
        ("after_p50_ms", r => Stats.Ms(r.AfterP50Ms)),
        ("change_pct", r => Math.Round(r.ChangePct, 1)),
        ("shift_started_on", r => r.ShiftStartedOn?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private readonly IEnumerable<IRegressionDetector> _detectors;
    private readonly TimeProvider _clock;
    private readonly IInsightsOptions _options;

    public FindRegressionsDataset(IEnumerable<IRegressionDetector> detectors, TimeProvider clock, IInsightsOptions options)
    {
        _detectors = detectors;
        _clock = clock;
        _options = options;
    }

    public string Name => DatasetNames.FIND_REGRESSIONS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var query = ParseQuery(args, _clock.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var kind = args.GetChoice("kind", "all", "build", "test", "suite") ?? "all";

        var detectors = _detectors
            .Where(d => kind == "all" || d.Kind == kind)
            .ToList();

        var rows = new List<RegressionRow>();
        foreach (var detector in detectors)
            rows.AddRange(await detector.FindAsync(query, cancellationToken));

        var ranked = rows
            .OrderByDescending(r => r.ChangePct)
            .ThenBy(r => r.Kind, StringComparer.Ordinal)
            .ThenBy(r => r.SubjectId, StringComparer.Ordinal)
            .ToList();

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageRows = ranked.Skip(offset).Take(page.Limit).ToList();
        return Schema.ToTable(pageRows, ranked.Count, Cursor.NextOffset(offset, pageRows.Count, ranked.Count)) with
        {
            Window = query.Scope.Window,
            Applied = $"{query.Scope.Describe()} kind={kind} current={query.CurrentFrom:yyyy-MM-dd}..{query.To:yyyy-MM-dd} " +
                      $"baseline={query.BaselineFrom:yyyy-MM-dd}..{query.CurrentFrom:yyyy-MM-dd} " +
                      $"min_increase_pct={query.MinIncreasePct} min_samples={query.MinSamples}"
        };
    }

    /// <summary>
    /// Reads <c>current_days</c> (7), <c>baseline_days</c> (28), <c>to</c> (now), <c>min_increase_pct</c> (20),
    /// <c>min_samples</c> (10) and the common scope. <c>days</c>/<c>from</c> are ignored: the windows define the span.
    /// </summary>
    public static RegressionQuery ParseQuery(InsightsArgs args, DateTime utcNow, int retentionDays)
    {
        var currentDays = args.GetInt("current_days", DEFAULT_CURRENT_DAYS);
        var baselineDays = args.GetInt("baseline_days", DEFAULT_BASELINE_DAYS);
        if (currentDays < 1) throw new InsightsArgumentException("current_days", "must be at least 1");
        if (baselineDays < 1) throw new InsightsArgumentException("baseline_days", "must be at least 1");
        var minIncrease = args.GetDouble("min_increase_pct", DEFAULT_MIN_INCREASE_PCT);
        var minSamples = args.GetInt("min_samples", DEFAULT_MIN_SAMPLES);
        if (minSamples < 1) throw new InsightsArgumentException("min_samples", "must be at least 1");

        var to = args.GetDateTime("to") ?? utcNow;
        var currentFrom = to.AddDays(-currentDays);
        var baselineFrom = currentFrom.AddDays(-baselineDays);

        var scopeArgs = args.Clone().Without("days").With("from", baselineFrom).With("to", to);
        var scope = InsightsScope.Resolve(scopeArgs, utcNow, retentionDays);
        return new RegressionQuery(scope, scope.Window.From, currentFrom, to, minIncrease, minSamples);
    }
}
