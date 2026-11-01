using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One <c>find_failing_tests</c> row.</summary>
public sealed record FailingTestRow(
    TestAggregateRow Test,
    int Runs,
    double? FailRate,
    DateTime? FailingSince,
    DateTime? LastPassedAt,
    string? LatestError,
    string Branches,
    string Reason,
    string Evidence);

/// <summary>
/// <c>find_failing_tests</c>: tests that are broken rather than flaky. A test is listed when
/// <c>fail_rate &gt;= min_fail_rate</c> and <c>runs &gt;= min_runs</c>, or when its latest <c>recent_runs</c>
/// executions all failed. Here <c>runs</c> counts Passed + Failed executions; skipped, pending and unknown are ignored
/// everywhere. Candidates come from the SQL-only <see cref="ITestAnalysisQueries.GetCandidatesAsync"/> with
/// <see cref="TestStatusFilter.HasFailures"/>; the recent-runs rule uses the trailing failure streak of one streaming fold;
/// percentiles and sequences are computed for the returned page only.
/// Tests that are failing right now (latest execution failed) rank first, by the start of their failing streak,
/// oldest first; the rest follow by fail rate.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class FailingTestsDataset : IInsightsDataset
{
    public const double DEFAULT_MIN_FAIL_RATE = 0.8;
    public const int DEFAULT_MIN_RUNS = 3;
    public const int DEFAULT_RECENT_RUNS = 3;
    private const int RECENT_EXECUTIONS = 30;

    private static readonly TableSchema<FailingTestRow> Schema = new(
        ("test_key", r => r.Test.TestKey),
        ("suite_id", r => r.Test.SuiteId),
        ("repository", r => r.Test.Repository),
        ("project", r => r.Test.Project),
        ("test_runner", r => r.Test.TestRunner),
        ("class_name", r => r.Test.ClassName),
        ("name", r => r.Test.Name),
        ("runs", r => r.Runs),
        ("failed", r => r.Test.Failed),
        ("fail_rate", r => r.FailRate),
        ("failing_since", r => r.FailingSince),
        ("last_passed_at", r => r.LastPassedAt),
        ("latest_error", r => r.LatestError),
        ("branches", r => r.Branches),
        ("status_sequence", r => r.Test.StatusSequence),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private readonly ITestAggregateQueries _queries;
    private readonly ITestAnalysisQueries _analysis;
    private readonly ITestHistoryQueries _history;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public FailingTestsDataset(
        ITestAggregateQueries queries, ITestAnalysisQueries analysis, ITestHistoryQueries history,
        ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _analysis = analysis;
        _history = history;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.FIND_FAILING_TESTS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var minFailRate = TestAnalysisArgs.Rate(args, "min_fail_rate", DEFAULT_MIN_FAIL_RATE);
        var minRuns = TestAnalysisArgs.MinInt(args, "min_runs", DEFAULT_MIN_RUNS);
        var recentRuns = TestAnalysisArgs.MinInt(args, "recent_runs", DEFAULT_RECENT_RUNS, 1);
        // Executions of any status, so this only drops tests that can match neither rule.
        var filter = (await TestAnalysisArgs.FilterAsync(args, _catalog, cancellationToken)) with
        {
            Status = TestStatusFilter.HasFailures,
            MinRuns = Math.Min(minRuns, recentRuns)
        };

        var candidates = await _analysis.GetCandidatesAsync(scope, filter, cancellationToken);

        // The rate rule is decidable from the SQL counts. A test that fails the rate rule can only match by recent runs
        // if it has at least recent_runs failures, so the rest need no outcomes at all.
        var possible = candidates
            .Where(c => ByRate(c, minFailRate, minRuns) || (c.Failed >= recentRuns && c.Passed + c.Failed >= recentRuns))
            .ToList();

        // One streaming fold for those tests: when did the current streak start, when did it last pass, how long is it.
        var outcomes = await _analysis.GetOutcomesAsync(scope, possible.Select(c => (c.Suite, c.NameKey)).ToList(), cancellationToken);
        var ranked = possible
            .Select(c => (Test: c, Summary: outcomes.GetValueOrDefault((c.Suite, c.NameKey))))
            .Where(x => x.Summary != null)
            .Select(x => (x.Test, x.Summary, Rule: Match(x.Test, x.Summary!, minFailRate, minRuns, recentRuns)))
            .Where(x => x.Rule != null)
            .OrderBy(x => x.Summary!.FailingSince.HasValue ? 0 : 1)
            .ThenBy(x => x.Summary!.FailingSince ?? DateTime.MaxValue)
            .ThenByDescending(x => Stats.Ratio(x.Summary!.Failed, x.Summary.Runs))
            .ThenBy(x => x.Test.TestKey, StringComparer.Ordinal)
            .ToList();

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageItems = ranked.Skip(offset).Take(page.Limit).ToList();
        // Percentiles and sequences are computed for the returned page only.
        var enriched = await TestAnalysisArgs.EnrichAsync(_queries, scope, filter, pageItems.Select(x => x.Test).ToList(), cancellationToken);
        pageItems = pageItems.Where(x => enriched.ContainsKey(x.Test.TestKey)).ToList();
        var keys = pageItems.Select(x => Key(x.Test)).ToList();
        var streaks = keys.Count == 0
            ? new Dictionary<(TestSuiteIdentity, string), TestFailureStreak>()
            : (await _history.GetFailureStreaksAsync(scope, keys, cancellationToken)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var notFailingNow = pageItems.Where(x => !streaks.ContainsKey(Key(x.Test))).Select(x => Key(x.Test)).ToList();
        var recent = notFailingNow.Count == 0
            ? new Dictionary<(TestSuiteIdentity, string), IReadOnlyList<TestExecutionRow>>()
            : (await _history.GetRecentExecutionsAsync(scope, notFailingNow, RECENT_EXECUTIONS, cancellationToken)).ToDictionary(kv => kv.Key, kv => kv.Value);

        var rows = pageItems.Select(x =>
        {
            var key = Key(x.Test);
            var test = enriched[x.Test.TestKey];
            string? latestError;
            string branches;
            if (streaks.TryGetValue(key, out var streak))
            {
                latestError = streak.LatestError;
                branches = string.Join(";", streak.Branches);
            }
            else
            {
                // Not failing right now: the newest failure and the branches of the failures among the recent executions.
                var failures = recent.TryGetValue(key, out var list) ? list.Where(e => e.Status == "Failed").ToList() : [];
                latestError = failures.FirstOrDefault()?.ErrorFirstLine;
                branches = string.Join(";", failures.Select(e => e.Branch).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            }

            var s = x.Summary!;
            return new FailingTestRow(
                test, s.Runs, Stats.Ratio(s.Failed, s.Runs), s.FailingSince, s.LastPassedAt, latestError, branches,
                Reason(s, x.Rule!, recentRuns),
                Evidence.Call(DatasetNames.GET_TEST_FAILURES, Evidence.ScopeArgs(scope).With("test_key", test.TestKey)));
        }).ToList();

        return Schema.ToTable(rows, ranked.Count, Cursor.NextOffset(offset, rows.Count, ranked.Count)) with
        {
            Window = scope.Window,
            Applied = TestAnalysisArgs.Applied(scope, args, ("min_fail_rate", minFailRate), ("min_runs", minRuns), ("recent_runs", recentRuns))
        };
    }

    private static bool ByRate(TestCandidate t, double minFailRate, int minRuns)
    {
        var runs = t.Passed + t.Failed;
        return runs >= minRuns && runs > 0 && (double)t.Failed / runs >= minFailRate;
    }

    /// <summary>Which rule listed the test: "rate", "recent", "rate and recent", or null when neither.</summary>
    private static string? Match(TestCandidate t, OutcomeSummary s, double minFailRate, int minRuns, int recentRuns)
    {
        var byRate = ByRate(t, minFailRate, minRuns);
        // The latest recent_runs Passed/Failed executions all failed: the trailing failure streak is at least that long.
        var byRecent = s.TrailingFailures >= recentRuns;
        return (byRate, byRecent) switch { (true, true) => "rate and recent", (true, false) => "rate", (false, true) => "recent", _ => null };
    }

    private static (TestSuiteIdentity, string) Key(TestCandidate c) => (c.Suite, c.NameKey);

    private static string Reason(OutcomeSummary s, string rule, int recentRuns)
    {
        var inv = CultureInfo.InvariantCulture;
        var parts = new List<string>
        {
            $"failed {s.Failed.ToString(inv)} of {s.Runs.ToString(inv)} runs ({TestAnalysisArgs.Pct(Stats.Ratio(s.Failed, s.Runs))})"
        };
        if (s.FailingSince.HasValue)
            parts.Add($"failing since {s.FailingSince.Value.ToString("yyyy-MM-dd", inv)}" +
                      (s.LastPassedAt.HasValue ? $", last passed {s.LastPassedAt.Value.ToString("yyyy-MM-dd", inv)}" : ", never passed in the window"));
        else
            parts.Add($"latest run passed ({s.LastPassedAt?.ToString("yyyy-MM-dd", inv)})");
        if (rule.Contains("recent")) parts.Add($"last {recentRuns.ToString(inv)} runs all failed");
        return string.Join("; ", parts);
    }
}
