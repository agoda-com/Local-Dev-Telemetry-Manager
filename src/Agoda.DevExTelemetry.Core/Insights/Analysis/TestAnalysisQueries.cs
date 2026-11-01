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
    /// For many tests at once, their Passed and Failed executions in scope ordered by (ReceivedAt, CaseId), oldest first.
    /// Skipped, Pending and Unknown are left out in SQL. A narrow projection (branch, time, case id, failed flag): the
    /// flip rate needs the order of results per branch, which no GROUP BY gives. Tests without such executions are absent.
    /// </summary>
    Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestOutcome>>> GetOutcomesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, CancellationToken cancellationToken);
}

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

    public async Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestOutcome>>> GetOutcomesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, CancellationToken cancellationToken)
    {
        var wanted = tests.ToHashSet();
        var result = new Dictionary<(TestSuiteIdentity, string), List<TestOutcome>>();
        var facts = scope.Apply(_db.TestCases).Facts().Where(f => f.Status == "Passed" || f.Status == "Failed");
        foreach (var chunk in wanted.Select(t => t.NameKey).Distinct().Chunk(KEY_CHUNK))
        {
            var keys = chunk.ToList();
            var query = facts.Where(f => keys.Contains(f.NameKey))
                .OrderBy(f => f.ReceivedAt).ThenBy(f => f.CaseId)
                .Select(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey, f.Branch, f.ReceivedAt, f.CaseId, f.Status });
            await foreach (var r in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                // A key chunk can match the same name key in a suite the caller did not ask about.
                var key = (new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner), r.NameKey);
                if (!wanted.Contains(key)) continue;
                if (!result.TryGetValue(key, out var list)) result[key] = list = [];
                list.Add(new TestOutcome(DateTime.SpecifyKind(r.ReceivedAt, DateTimeKind.Utc), r.CaseId, r.Branch, r.Status == "Failed"));
            }
        }

        return result.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<TestOutcome>)kv.Value);
    }
}

/// <summary>What the Passed/Failed executions of one test add up to.</summary>
/// <param name="Runs">Passed + Failed executions.</param>
/// <param name="Flips">Status changes between consecutive executions on the same branch, summed over branches.</param>
/// <param name="Pairs">Consecutive same-branch pairs: Runs minus the number of branches. The flip rate's denominator.</param>
/// <param name="FailingSince">Time of the first failure of the trailing run of failures, or null when the latest execution passed.</param>
/// <param name="LastPassedAt">Time of the latest pass, or null.</param>
public sealed record OutcomeSummary(int Runs, int Failed, int Flips, int Pairs, int Branches, DateTime? FailingSince, DateTime? LastPassedAt)
{
    /// <summary>flips / pairs, where pairs = runs - branches (each branch's first run has no predecessor); null without pairs.</summary>
    public double? FlipRate => Stats.Ratio(Flips, Pairs);

    public static OutcomeSummary Of(IReadOnlyList<TestOutcome> ordered)
    {
        var lastByBranch = new Dictionary<string, bool>(StringComparer.Ordinal);
        var flips = 0;
        var failed = 0;
        DateTime? failingSince = null;
        DateTime? lastPassed = null;
        foreach (var o in ordered)
        {
            if (lastByBranch.TryGetValue(o.Branch, out var previous) && previous != o.Failed) flips++;
            lastByBranch[o.Branch] = o.Failed;
            if (o.Failed)
            {
                failed++;
                failingSince ??= o.At;
            }
            else
            {
                failingSince = null;
                lastPassed = o.At;
            }
        }

        var branches = lastByBranch.Count;
        return new OutcomeSummary(ordered.Count, failed, flips, ordered.Count - branches, branches, failingSince, lastPassed);
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
