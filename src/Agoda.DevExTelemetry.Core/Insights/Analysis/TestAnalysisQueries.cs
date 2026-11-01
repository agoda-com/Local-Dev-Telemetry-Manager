using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One Passed or Failed execution of a test, in the order the flip and streak maths needs.</summary>
public readonly record struct TestOutcome(DateTime At, int CaseId, string Branch, bool Failed);

/// <summary>Narrow batched queries shared by <c>find_slow_tests</c>, <c>find_flaky_tests</c> and <c>find_failing_tests</c>.</summary>
public interface ITestAnalysisQueries
{
    /// <summary>
    /// The summed duration (ms) of every test execution in the scope's window, per suite. SQL GROUP BY over the scope
    /// only (no name or class filters), so a test's share of its suite is against the whole suite.
    /// </summary>
    Task<IReadOnlyDictionary<TestSuiteIdentity, double>> GetSuiteTotalsAsync(InsightsScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Cheap candidate selection for the flaky and failing tools: one SQL GROUP BY row per test (key, runs, passed,
    /// failed, last run, last failure) with the filter's text filters applied to executions and its
    /// <see cref="TestsFilter.MinRuns"/> and <see cref="TestsFilter.Status"/> applied to the groups. No percentiles,
    /// no sequences, no paging: callers rank the candidates and enrich only the page they return.
    /// </summary>
    Task<IReadOnlyList<TestCandidate>> GetCandidatesAsync(InsightsScope scope, TestsFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// For many tests at once, what their Passed and Failed executions add up to. The executions are streamed in
    /// (suite, name key, ReceivedAt, CaseId) order and folded one test at a time, so memory is O(tests) for the
    /// result plus O(branches) for the test being folded, never O(executions). Skipped, Pending and Unknown are left
    /// out in SQL. Tests without such executions are absent.
    /// </summary>
    Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), OutcomeSummary>> GetOutcomesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, CancellationToken cancellationToken);
}

/// <summary>One test from <see cref="ITestAnalysisQueries.GetCandidatesAsync"/>: its key and the SQL-only counts.</summary>
public sealed record TestCandidate(
    TestSuiteIdentity Suite, string NameKey, string TestKey, int Runs, int Passed, int Failed, DateTime LastRunAt, DateTime? LastFailedAt);

[RegisterPerRequest]
public sealed class TestAnalysisQueries : ITestAnalysisQueries
{
    private const int KEY_CHUNK = 500;
    private readonly TelemetryDbContext _db;

    public TestAnalysisQueries(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<TestSuiteIdentity, double>> GetSuiteTotalsAsync(InsightsScope scope, CancellationToken cancellationToken)
    {
        var rows = await scope.Apply(_db.TestCases).Facts()
            .GroupBy(f => new { f.Repository, f.Project, f.TestRunner })
            .Select(g => new { g.Key.Repository, g.Key.Project, g.Key.TestRunner, Total = g.Sum(f => f.DurationMs) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner), r => r.Total ?? 0);
    }

    public async Task<IReadOnlyList<TestCandidate>> GetCandidatesAsync(InsightsScope scope, TestsFilter filter, CancellationToken cancellationToken)
    {
        var facts = TestAggregateQueries.ApplyExecutionFilters(scope.Apply(_db.TestCases).Facts(), filter);
        var groups = facts
            .GroupBy(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey })
            .Select(g => new
            {
                g.Key.Repository,
                g.Key.Project,
                g.Key.TestRunner,
                g.Key.NameKey,
                Runs = g.Count(),
                Passed = g.Count(f => f.Status == "Passed"),
                Failed = g.Count(f => f.Status == "Failed"),
                LastRunAt = g.Max(f => f.ReceivedAt),
                LastFailedAt = g.Where(f => f.Status == "Failed").Max(f => (DateTime?)f.ReceivedAt)
            });
        if (filter.MinRuns is { } minRuns) groups = groups.Where(a => a.Runs >= minRuns);
        groups = filter.Status switch
        {
            TestStatusFilter.HasFailures => groups.Where(a => a.Failed > 0),
            TestStatusFilter.HasPassesAndFailures => groups.Where(a => a.Passed > 0 && a.Failed > 0),
            TestStatusFilter.AlwaysFailed => groups.Where(a => a.Failed > 0 && a.Passed == 0),
            TestStatusFilter.LastFailed => groups.Where(a => a.LastFailedAt != null && a.LastFailedAt == a.LastRunAt),
            _ => groups
        };

        var suiteIds = new Dictionary<TestSuiteIdentity, string>();
        var result = new List<TestCandidate>();
        await foreach (var a in groups.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var suite = new TestSuiteIdentity(a.Repository, a.Project, a.TestRunner);
            if (!suiteIds.TryGetValue(suite, out var suiteId)) suiteIds[suite] = suiteId = suite.SuiteId;
            result.Add(new TestCandidate(
                suite, a.NameKey, TestKey.Format(suiteId, a.NameKey), a.Runs, a.Passed, a.Failed,
                DateTime.SpecifyKind(a.LastRunAt, DateTimeKind.Utc),
                a.LastFailedAt.HasValue ? DateTime.SpecifyKind(a.LastFailedAt.Value, DateTimeKind.Utc) : null));
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), OutcomeSummary>> GetOutcomesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, CancellationToken cancellationToken)
    {
        var wanted = tests.ToHashSet();
        var result = new Dictionary<(TestSuiteIdentity, string), OutcomeSummary>();
        var facts = scope.Apply(_db.TestCases).Facts().Where(f => f.Status == "Passed" || f.Status == "Failed");
        foreach (var chunk in wanted.Select(t => t.NameKey).Distinct().Chunk(KEY_CHUNK))
        {
            var keys = chunk.ToList();
            var query = facts.Where(f => keys.Contains(f.NameKey))
                .OrderBy(f => f.Repository).ThenBy(f => f.Project).ThenBy(f => f.TestRunner).ThenBy(f => f.NameKey)
                .ThenBy(f => f.ReceivedAt).ThenBy(f => f.CaseId)
                .Select(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey, f.Branch, f.ReceivedAt, f.CaseId, f.Status });

            (TestSuiteIdentity, string)? current = null;
            var fold = new OutcomeFold();
            await foreach (var r in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                var key = (new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner), r.NameKey);
                if (current != key)
                {
                    if (current is { } done && wanted.Contains(done)) result[done] = fold.Build();
                    current = key;
                    fold = new OutcomeFold();
                }

                // A key chunk can match the same name key in a suite the caller did not ask about.
                if (!wanted.Contains(key)) continue;
                fold.Add(new TestOutcome(DateTime.SpecifyKind(r.ReceivedAt, DateTimeKind.Utc), r.CaseId, r.Branch, r.Status == "Failed"));
            }

            if (current is { } last && wanted.Contains(last)) result[last] = fold.Build();
        }

        return result;
    }
}

/// <summary>What the Passed/Failed executions of one test add up to.</summary>
/// <param name="Runs">Passed + Failed executions.</param>
/// <param name="Flips">Status changes between consecutive executions on the same branch, summed over branches.</param>
/// <param name="Pairs">Consecutive same-branch pairs: Runs minus the number of branches. The flip rate's denominator.</param>
/// <param name="FailingSince">Time of the first failure of the trailing run of failures (across branches, in time order), or null when the latest execution passed.</param>
/// <param name="LastPassedAt">Time of the latest pass, or null.</param>
/// <param name="TrailingFailures">Length of the trailing run of failures: how many of the latest executions, in time order across branches, all failed.</param>
public sealed record OutcomeSummary(
    int Runs, int Failed, int Flips, int Pairs, int Branches, DateTime? FailingSince, DateTime? LastPassedAt, int TrailingFailures = 0)
{
    /// <summary>flips / pairs, where pairs = runs - branches (each branch's first run has no predecessor); null without pairs.</summary>
    public double? FlipRate => Stats.Ratio(Flips, Pairs);

    /// <summary>Folds executions given in (ReceivedAt, CaseId) order.</summary>
    public static OutcomeSummary Of(IReadOnlyList<TestOutcome> ordered)
    {
        var fold = new OutcomeFold();
        foreach (var o in ordered) fold.Add(o);
        return fold.Build();
    }
}

/// <summary>
/// Streaming fold of one test's executions, which must arrive in (ReceivedAt, CaseId) order across branches.
/// State is O(branches): the last status per branch plus a few counters.
/// </summary>
internal sealed class OutcomeFold
{
    private readonly Dictionary<string, bool> _lastByBranch = new(StringComparer.Ordinal);
    private int _runs, _failed, _flips, _trailing;
    private DateTime? _failingSince, _lastPassed;

    public void Add(TestOutcome o)
    {
        _runs++;
        if (_lastByBranch.TryGetValue(o.Branch, out var previous) && previous != o.Failed) _flips++;
        _lastByBranch[o.Branch] = o.Failed;
        if (o.Failed)
        {
            _failed++;
            _trailing++;
            _failingSince ??= o.At;
        }
        else
        {
            _trailing = 0;
            _failingSince = null;
            _lastPassed = o.At;
        }
    }

    public OutcomeSummary Build()
    {
        var branches = _lastByBranch.Count;
        return new OutcomeSummary(_runs, _failed, _flips, _runs - branches, branches, _failingSince, _lastPassed, _trailing);
    }
}

/// <summary>Argument parsing and wording shared by the three test analysis datasets.</summary>
internal static class TestAnalysisArgs
{
    public static readonly string[] FilterNames = ["suite_id", "test_runner", "name_contains", "class_contains"];

    public static int MinInt(InsightsArgs args, string name, int defaultValue, int minimum = 0)
    {
        var value = args.GetInt(name, defaultValue);
        if (value < minimum) throw new InsightsArgumentException(name, minimum == 0 ? "must not be negative" : $"must be at least {minimum}");
        return value;
    }

    public static double Rate(InsightsArgs args, string name, double defaultValue)
    {
        var value = args.GetDouble(name, defaultValue);
        if (value < 0 || value > 1) throw new InsightsArgumentException(name, "must be between 0 and 1");
        return value;
    }

    /// <summary>The suite, runner, name and class filters as a <see cref="TestsFilter"/> plus the same four as evidence arguments.</summary>
    public static async Task<TestsFilter> FilterAsync(InsightsArgs args, ITestSuiteCatalog catalog, CancellationToken cancellationToken)
    {
        var suiteId = args.GetString("suite_id");
        return new TestsFilter
        {
            Suite = suiteId == null ? null : await catalog.ResolveAsync(suiteId, cancellationToken),
            TestRunner = args.GetString("test_runner"),
            NameContains = args.GetString("name_contains"),
            ClassContains = args.GetString("class_contains")
        };
    }

    /// <summary>
    /// The full aggregate rows (percentiles, status sequence) of one page of candidates only. <paramref name="filter"/> must be
    /// the filter the candidates were chosen with; its group filters are dropped and the page's name keys added, so each row
    /// aggregates exactly as it would in a full <see cref="ITestAggregateQueries.QueryTestsAsync"/> over the filter.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, TestAggregateRow>> EnrichAsync(
        ITestAggregateQueries queries, InsightsScope scope, TestsFilter filter, IReadOnlyCollection<TestCandidate> page, CancellationToken cancellationToken)
    {
        if (page.Count == 0) return new Dictionary<string, TestAggregateRow>();
        var pageFilter = filter with { MinRuns = null, MinAvgMs = null, Status = TestStatusFilter.None, NameKeys = page.Select(c => c.NameKey).Distinct().ToList() };
        var (rows, _) = await queries.QueryTestsAsync(scope, pageFilter, new SortSpec("runs", true), 0, int.MaxValue, cancellationToken);
        var wanted = page.Select(c => c.TestKey).ToHashSet(StringComparer.Ordinal);
        return rows.Where(r => wanted.Contains(r.TestKey)).ToDictionary(r => r.TestKey, StringComparer.Ordinal);
    }

    public static string Applied(InsightsScope scope, InsightsArgs args, params (string Name, object Value)[] thresholds)
    {
        var filters = args.ToDisplayString([.. InsightsScope.ParameterNames, .. thresholds.Select(t => t.Name), "sort_by", "group_by"]);
        var parts = new List<string> { scope.Describe() };
        if (filters.Length > 0) parts.Add(filters);
        parts.AddRange(thresholds.Select(t => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{t.Name}={t.Value}")));
        return string.Join(" ", parts);
    }

    public static string Pct(double? ratio) =>
        ratio.HasValue ? Math.Round(ratio.Value * 100, 0, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "n/a";

    public static string Inv(object value) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{value}");
}
