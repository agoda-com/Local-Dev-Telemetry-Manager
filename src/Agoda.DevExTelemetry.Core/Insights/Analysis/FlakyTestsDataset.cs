using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One <c>find_flaky_tests</c> row.</summary>
public sealed record FlakyTestRow(
    TestAggregateRow Test,
    OutcomeSummary Outcomes,
    string TopErrors,
    string Reason,
    string Evidence)
{
    public double? FailRate => Stats.Ratio(Outcomes.Failed, Outcomes.Runs);
}

/// <summary>
/// <c>find_flaky_tests</c>: tests that both passed and failed in the window, ranked by flip rate.
/// <para>
/// Candidates come from <see cref="ITestAggregateQueries.QueryTestsAsync"/> with
/// <see cref="TestStatusFilter.HasPassesAndFailures"/>. Flips are counted from one batched narrow query
/// (<see cref="ITestAnalysisQueries.GetOutcomesAsync"/>) over Passed/Failed executions only: a flip is a status change
/// between consecutive executions on the same branch, ordered by time. <c>flip_rate = flips / (runs - branches)</c>,
/// that is flips over the number of consecutive same-branch pairs, which is <c>flips / (runs - 1)</c> for one branch.
/// A test that keeps flickering scores near 1; one that broke for a while and was fixed (PPPPFFFFPPPP) scores 2/11.
/// </para>
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class FlakyTestsDataset : IInsightsDataset
{
    public const int DEFAULT_MIN_RUNS = 5;
    public const double DEFAULT_MIN_FLIP_RATE = 0;
    public const int TOP_ERRORS = 3;

    private static readonly TableSchema<FlakyTestRow> Schema = new(
        ("test_key", r => r.Test.TestKey),
        ("suite_id", r => r.Test.SuiteId),
        ("repository", r => r.Test.Repository),
        ("project", r => r.Test.Project),
        ("test_runner", r => r.Test.TestRunner),
        ("class_name", r => r.Test.ClassName),
        ("name", r => r.Test.Name),
        ("runs", r => r.Outcomes.Runs),
        ("failed", r => r.Outcomes.Failed),
        ("fail_rate", r => r.FailRate),
        ("flips", r => r.Outcomes.Flips),
        ("flip_rate", r => r.Outcomes.FlipRate),
        ("last_failed_at", r => r.Test.LastFailedAt),
        ("status_sequence", r => r.Test.StatusSequence),
        ("top_errors", r => r.TopErrors),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private readonly ITestAggregateQueries _queries;
    private readonly ITestAnalysisQueries _analysis;
    private readonly ITestHistoryQueries _history;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public FlakyTestsDataset(
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

    public string Name => DatasetNames.FIND_FLAKY_TESTS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays, InsightsScope.ENV_CI);
        var minRuns = TestAnalysisArgs.MinInt(args, "min_runs", DEFAULT_MIN_RUNS);
        var minFlipRate = TestAnalysisArgs.Rate(args, "min_flip_rate", DEFAULT_MIN_FLIP_RATE);
        // MinRuns counts executions of any status, so it can only drop tests that cannot reach min_runs Passed+Failed runs.
        var filter = (await TestAnalysisArgs.FilterAsync(args, _catalog, cancellationToken)) with
        {
            Status = TestStatusFilter.HasPassesAndFailures,
            MinRuns = minRuns
        };

        var (candidates, _) = await _queries.QueryTestsAsync(
            scope, filter, new SortSpec("fail_rate", true), 0, int.MaxValue, cancellationToken);
        var outcomes = await _analysis.GetOutcomesAsync(scope, candidates.Select(Key).ToList(), cancellationToken);

        var ranked = candidates
            .Select(c => (Test: c, Summary: outcomes.TryGetValue(Key(c), out var list) ? OutcomeSummary.Of(list) : null))
            .Where(x => x.Summary is { Flips: > 0 } s && s.Runs >= minRuns && s.FlipRate >= minFlipRate)
            .OrderByDescending(x => x.Summary!.FlipRate)
            .ThenByDescending(x => Stats.Ratio(x.Summary!.Failed, x.Summary.Runs))
            .ThenBy(x => x.Test.TestKey, StringComparer.Ordinal)
            .ToList();

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageItems = ranked.Skip(offset).Take(page.Limit).ToList();
        var errors = pageItems.Count == 0
            ? new Dictionary<(TestSuiteIdentity, string), IReadOnlyList<TestErrorSummary>>()
            : (await _history.GetErrorSummariesAsync(scope, pageItems.Select(x => Key(x.Test)).ToList(), TOP_ERRORS, cancellationToken))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var rows = pageItems.Select(x =>
        {
            var topErrors = errors.TryGetValue(Key(x.Test), out var list)
                ? string.Join("; ", list.Select(e => $"{e.Error} ({e.Count})"))
                : string.Empty;
            return new FlakyTestRow(x.Test, x.Summary!, topErrors, Reason(x.Test, x.Summary!), EvidenceFor(scope, x.Test));
        }).ToList();

        var table = Schema.ToTable(rows, ranked.Count, Cursor.NextOffset(offset, rows.Count, ranked.Count)) with
        {
            Window = scope.Window,
            Applied = TestAnalysisArgs.Applied(scope, args, ("min_runs", minRuns), ("min_flip_rate", minFlipRate))
        };
        return scope.Environment == InsightsScope.ENV_CI
            ? table
            : table with
            {
                Notes = ["environment includes local runs: local fail-then-pass is often test-driven development, not flakiness; use environment=ci for a cleaner signal"]
            };
    }

    private static (TestSuiteIdentity, string) Key(TestAggregateRow r) =>
        (new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner), KeyName(r));

    // TestKey is suite_id:name_key; the name key is everything after the first colon.
    private static string KeyName(TestAggregateRow r) => r.TestKey[(r.SuiteId.Length + 1)..];

    private static string Reason(TestAggregateRow test, OutcomeSummary s)
    {
        var inv = CultureInfo.InvariantCulture;
        return $"{s.Flips.ToString(inv)} flips in {s.Runs.ToString(inv)} runs (flip rate {TestAnalysisArgs.Pct(s.FlipRate)}) on {s.Branches.ToString(inv)} " +
               $"{(s.Branches == 1 ? "branch" : "branches")}; failed {s.Failed.ToString(inv)} of {s.Runs.ToString(inv)} ({TestAnalysisArgs.Pct(Stats.Ratio(s.Failed, s.Runs))}); sequence {test.StatusSequence}";
    }

    private static string EvidenceFor(InsightsScope scope, TestAggregateRow r) =>
        Evidence.Call(DatasetNames.GET_TEST, Evidence.ScopeArgs(scope).With("test_key", r.TestKey));
}
