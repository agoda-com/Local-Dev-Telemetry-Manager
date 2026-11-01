using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>
/// Raw test history: executions of one test, failures, runs and the cases of one run. Rows are listed
/// newest first and paged with keyset cursors on (ReceivedAt, unique id), so concurrent inserts never skip
/// or repeat rows. No row caps: callers pass the limit. The analysis tools reuse the per-test methods at the end.
/// </summary>
public interface ITestHistoryQueries
{
    /// <summary>
    /// Executions matching <paramref name="filter"/> within <paramref name="scope"/>, newest first (ReceivedAt
    /// desc, CaseId desc). <paramref name="after"/> is the (ReceivedAt, CaseId) of the last row of the previous
    /// page. <see cref="KeysetPage{T}.TotalRows"/> counts the whole filtered set, ignoring <paramref name="after"/>.
    /// </summary>
    Task<KeysetPage<TestExecutionRow>> GetExecutionsAsync(
        InsightsScope scope, TestExecutionFilter filter, (DateTime Timestamp, string Id)? after, int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Distinct tests of <paramref name="suite"/> whose name key contains <paramref name="nameContains"/>
    /// (case-insensitive) with runs in scope, most recently run first. <c>Total</c> counts all matches.
    /// </summary>
    Task<(IReadOnlyList<TestCandidate> Candidates, int Total)> FindTestsAsync(
        InsightsScope scope, TestSuiteIdentity suite, string nameContains, int limit, CancellationToken cancellationToken);

    /// <summary>Runs in scope, newest first (ReceivedAt desc, Id desc), with keyset paging like <see cref="GetExecutionsAsync"/>.</summary>
    Task<KeysetPage<TestRunRow>> GetRunsAsync(
        InsightsScope scope, TestRunFilter filter, (DateTime Timestamp, string Id)? after, int limit,
        CancellationToken cancellationToken);

    /// <summary>One run by id, or null. The window does not apply; the run id is exact.</summary>
    Task<TestRunRow?> GetRunAsync(string testRunId, CancellationToken cancellationToken);

    /// <summary>
    /// The cases of one run with offset paging. <paramref name="sort"/> <see cref="TestRunCaseSort.Default"/> is
    /// the dashboard order (failed first, then duration descending); otherwise <paramref name="descending"/> applies.
    /// </summary>
    Task<(IReadOnlyList<TestRunCaseRow> Rows, int Total)> GetRunCasesAsync(
        string testRunId, TestSuiteIdentity suite, string? status, string? nameContains,
        TestRunCaseSort sort, bool descending, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// For each test, the newest <paramref name="perTestLimit"/> executions in scope, newest first.
    /// One query per test; pass the tests of one page, not a whole suite.
    /// </summary>
    Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestExecutionRow>>> GetRecentExecutionsAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, int perTestLimit,
        CancellationToken cancellationToken);

    /// <summary>
    /// For each test, its top <paramref name="topN"/> distinct error first-lines among failed executions in scope,
    /// most frequent first. Counting is done in SQL over the failed rows; only the distinct messages come back
    /// and are merged by first line in memory. Tests without failures are absent.
    /// </summary>
    Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestErrorSummary>>> GetErrorSummariesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, int topN,
        CancellationToken cancellationToken);

    /// <summary>
    /// For each test that fails in scope after its last pass, the current failing streak. Tests that are not
    /// failing (never failed, or passed after their last failure) are absent.
    /// </summary>
    Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), TestFailureStreak>> GetFailureStreaksAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests,
        CancellationToken cancellationToken);
}

[RegisterPerRequest]
public sealed class TestHistoryQueries : ITestHistoryQueries
{
    private const string FAILED = "Failed";
    private const string PASSED = "Passed";
    private const string NO_MESSAGE = "(no error message)";

    private readonly TelemetryDbContext _db;

    public TestHistoryQueries(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<KeysetPage<TestExecutionRow>> GetExecutionsAsync(
        InsightsScope scope, TestExecutionFilter filter, (DateTime Timestamp, string Id)? after, int limit,
        CancellationToken cancellationToken)
    {
        var query = Filter(scope.Apply(_db.TestCases).Facts(), filter);
        var total = await query.CountAsync(cancellationToken);

        if (after is { } cursor)
        {
            if (!int.TryParse(cursor.Id, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var afterId))
                throw new InsightsArgumentException("cursor", "is not a cursor from this tool; pass next_cursor from the previous result unchanged");
            var ts = cursor.Timestamp;
            query = query.Where(f => f.ReceivedAt < ts || (f.ReceivedAt == ts && f.CaseId < afterId));
        }

        var facts = await query
            .OrderByDescending(f => f.ReceivedAt).ThenByDescending(f => f.CaseId)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return new KeysetPage<TestExecutionRow>(facts.Select(ToRow).ToList(), total);
    }

    public async Task<(IReadOnlyList<TestCandidate> Candidates, int Total)> FindTestsAsync(
        InsightsScope scope, TestSuiteIdentity suite, string nameContains, int limit, CancellationToken cancellationToken)
    {
        var needle = nameContains.ToLower();
        var groups = scope.Apply(_db.TestCases).Facts()
            .InSuite(suite)
            .Where(f => f.NameKey.ToLower().Contains(needle))
            .GroupBy(f => f.NameKey)
            .Select(g => new
            {
                NameKey = g.Key,
                ClassName = g.Max(f => f.ClassName),
                Name = g.Max(f => f.Name),
                Runs = g.Count(),
                LastRunAt = g.Max(f => f.ReceivedAt)
            });
        var total = await groups.CountAsync(cancellationToken);
        var page = await groups
            .OrderByDescending(g => g.LastRunAt).ThenBy(g => g.NameKey)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return (page.Select(g => new TestCandidate(suite, g.NameKey, g.ClassName, g.Name!, g.Runs, g.LastRunAt)).ToList(), total);
    }

    public async Task<KeysetPage<TestRunRow>> GetRunsAsync(
        InsightsScope scope, TestRunFilter filter, (DateTime Timestamp, string Id)? after, int limit,
        CancellationToken cancellationToken)
    {
        var query = scope.Apply(_db.TestRuns);
        if (filter.Suite is { } suite)
            query = query.Where(r => r.RepositoryName == suite.Repository && r.ProjectName == suite.Project && r.TestRunner == suite.TestRunner);
        if (filter.TestRunner != null)
        {
            var runner = filter.TestRunner;
            query = query.Where(r => r.TestRunner == runner);
        }
        if (filter.Status == "failed") query = query.Where(r => r.FailedTests > 0);
        else if (filter.Status == "passed") query = query.Where(r => r.FailedTests == null || r.FailedTests == 0);

        var total = await query.CountAsync(cancellationToken);

        if (after is { } cursor)
        {
            var ts = cursor.Timestamp;
            var id = cursor.Id;
            query = query.Where(r => r.ReceivedAt < ts || (r.ReceivedAt == ts && string.Compare(r.Id, id) < 0));
        }

        var runs = await query
            .OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.Id)
            .Take(limit)
            .Select(RunProjection)
            .ToListAsync(cancellationToken);
        return new KeysetPage<TestRunRow>(runs.Select(ToRunRow).ToList(), total);
    }

    public async Task<TestRunRow?> GetRunAsync(string testRunId, CancellationToken cancellationToken)
    {
        // Narrow projection on purpose: never loads UserName or Hostname.
        var run = await _db.TestRuns
            .Where(r => r.Id == testRunId)
            .Select(RunProjection)
            .FirstOrDefaultAsync(cancellationToken);
        return run == null ? null : ToRunRow(run);
    }

    public async Task<(IReadOnlyList<TestRunCaseRow> Rows, int Total)> GetRunCasesAsync(
        string testRunId, TestSuiteIdentity suite, string? status, string? nameContains,
        TestRunCaseSort sort, bool descending, int offset, int limit, CancellationToken cancellationToken)
    {
        var query = _db.TestCases.Where(c => c.TestRunId == testRunId).Facts();
        if (status != null) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrEmpty(nameContains))
        {
            var needle = nameContains.ToLower();
            query = query.Where(f => f.NameKey.ToLower().Contains(needle));
        }

        var total = await query.CountAsync(cancellationToken);

        IOrderedQueryable<TestCaseFact> ordered = sort switch
        {
            TestRunCaseSort.DurationMs => descending
                ? query.OrderByDescending(f => f.DurationMs ?? 0)
                : query.OrderBy(f => f.DurationMs ?? 0),
            TestRunCaseSort.Status => descending
                ? query.OrderByDescending(f => f.Status)
                : query.OrderBy(f => f.Status),
            TestRunCaseSort.Name => descending
                ? query.OrderByDescending(f => f.NameKey)
                : query.OrderBy(f => f.NameKey),
            _ => query.OrderBy(f => f.Status == FAILED ? 0 : 1).ThenByDescending(f => f.DurationMs ?? 0)
        };

        var facts = await ordered.ThenBy(f => f.CaseId).Skip(offset).Take(limit).ToListAsync(cancellationToken);
        var rows = facts
            .Select(f => new TestRunCaseRow(f.CaseId, suite, f.NameKey, f.ClassName, f.Name, f.Status, f.DurationMs,
                ErrorText.FirstLine(f.ErrorMessage)))
            .ToList();
        return (rows, total);
    }

    public async Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestExecutionRow>>> GetRecentExecutionsAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, int perTestLimit,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(TestSuiteIdentity, string), IReadOnlyList<TestExecutionRow>>();
        foreach (var (suite, nameKey) in tests.Distinct())
        {
            var facts = await scope.Apply(_db.TestCases).Facts().OfTest(suite, nameKey)
                .OrderByDescending(f => f.ReceivedAt).ThenByDescending(f => f.CaseId)
                .Take(perTestLimit)
                .ToListAsync(cancellationToken);
            result[(suite, nameKey)] = facts.Select(ToRow).ToList();
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), IReadOnlyList<TestErrorSummary>>> GetErrorSummariesAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests, int topN,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(TestSuiteIdentity, string), IReadOnlyList<TestErrorSummary>>();
        foreach (var (suite, nameKey) in tests.Distinct())
        {
            // Counts per distinct full message in SQL; the first-line merge happens over those few groups.
            var groups = await scope.Apply(_db.TestCases).Facts().OfTest(suite, nameKey)
                .Where(f => f.Status == FAILED)
                .GroupBy(f => f.ErrorMessage)
                .Select(g => new { Message = g.Key, Count = g.Count(), Last = g.Max(f => f.ReceivedAt) })
                .ToListAsync(cancellationToken);
            if (groups.Count == 0) continue;

            var merged = groups
                .GroupBy(g => ErrorText.FirstLine(g.Message) ?? NO_MESSAGE, StringComparer.Ordinal)
                .Select(g => new TestErrorSummary(suite, nameKey, g.Key, g.Sum(x => x.Count), g.Max(x => x.Last)))
                .OrderByDescending(e => e.Count).ThenByDescending(e => e.LastSeenAt).ThenBy(e => e.Error, StringComparer.Ordinal)
                .Take(topN)
                .ToList();
            result[(suite, nameKey)] = merged;
        }
        return result;
    }

    public async Task<IReadOnlyDictionary<(TestSuiteIdentity Suite, string NameKey), TestFailureStreak>> GetFailureStreaksAsync(
        InsightsScope scope, IReadOnlyCollection<(TestSuiteIdentity Suite, string NameKey)> tests,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(TestSuiteIdentity, string), TestFailureStreak>();
        foreach (var (suite, nameKey) in tests.Distinct())
        {
            var facts = scope.Apply(_db.TestCases).Facts().OfTest(suite, nameKey);
            var lastPass = await facts.Where(f => f.Status == PASSED)
                .MaxAsync(f => (DateTime?)f.ReceivedAt, cancellationToken);

            var streak = lastPass == null
                ? facts.Where(f => f.Status == FAILED)
                : facts.Where(f => f.Status == FAILED && f.ReceivedAt > lastPass.Value);

            var count = await streak.CountAsync(cancellationToken);
            if (count == 0) continue;

            var first = await streak.MinAsync(f => f.ReceivedAt, cancellationToken);
            var latest = await streak
                .OrderByDescending(f => f.ReceivedAt).ThenByDescending(f => f.CaseId)
                .Select(f => new { f.ReceivedAt, f.ErrorMessage })
                .FirstAsync(cancellationToken);
            var branches = await streak.Select(f => f.Branch).Distinct().ToListAsync(cancellationToken);

            result[(suite, nameKey)] = new TestFailureStreak(
                suite, nameKey, first, lastPass, latest.ReceivedAt, ErrorText.FirstLine(latest.ErrorMessage), count,
                branches.OrderBy(b => b, StringComparer.Ordinal).ToList());
        }
        return result;
    }

    private static IQueryable<TestCaseFact> Filter(IQueryable<TestCaseFact> facts, TestExecutionFilter filter)
    {
        if (filter.Suite is { } suite)
        {
            facts = filter.NameKey != null ? facts.OfTest(suite, filter.NameKey) : facts.InSuite(suite);
        }
        if (filter.Status != null)
        {
            var status = filter.Status;
            facts = facts.Where(f => f.Status == status);
        }
        if (!string.IsNullOrEmpty(filter.NameContains))
        {
            var needle = filter.NameContains.ToLower();
            facts = facts.Where(f => f.NameKey.ToLower().Contains(needle));
        }
        if (!string.IsNullOrEmpty(filter.ClassContains))
        {
            var needle = filter.ClassContains.ToLower();
            facts = facts.Where(f => f.ClassName != null && f.ClassName.ToLower().Contains(needle));
        }
        if (!string.IsNullOrEmpty(filter.ErrorContains))
        {
            var needle = filter.ErrorContains.ToLower();
            facts = facts.Where(f => f.ErrorMessage != null && f.ErrorMessage.ToLower().Contains(needle));
        }
        return facts;
    }

    private static TestExecutionRow ToRow(TestCaseFact f) =>
        new(f.CaseId, f.ReceivedAt, f.TestRunId, new TestSuiteIdentity(f.Repository, f.Project, f.TestRunner),
            f.NameKey, f.ClassName, f.Name, f.Status, f.DurationMs, f.Branch, f.Environment, f.Platform, f.Os,
            f.CpuCount, f.RunTotalMs, ErrorText.FirstLine(f.ErrorMessage));

    private sealed record RunHeader(
        string Id, DateTime ReceivedAt, string Repository, string Project, string TestRunner, string Branch,
        string Environment, string Platform, string Os, int CpuCount, int? TotalTests, int? PassedTests,
        int? FailedTests, int? SkippedTests, double? TotalDurationMs);

    private static readonly System.Linq.Expressions.Expression<Func<Models.Entities.TestRun, RunHeader>> RunProjection =
        r => new RunHeader(r.Id, r.ReceivedAt, r.RepositoryName, r.ProjectName, r.TestRunner, r.Branch,
            r.ExecutionEnvironment, r.Platform, r.Os, r.CpuCount, r.TotalTests, r.PassedTests, r.FailedTests,
            r.SkippedTests, r.TotalDurationMs);

    private static TestRunRow ToRunRow(RunHeader r) =>
        new(r.Id, r.ReceivedAt, new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner), r.Branch, r.Environment,
            r.Platform, r.Os, r.CpuCount, r.TotalTests, r.PassedTests, r.FailedTests, r.SkippedTests, r.TotalDurationMs);
}
