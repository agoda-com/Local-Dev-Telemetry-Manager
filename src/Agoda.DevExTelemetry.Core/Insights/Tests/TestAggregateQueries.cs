using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>
/// Aggregate test queries behind <c>list_test_suites</c> and <c>get_tests</c>, and reused by the analysis tools
/// (slow, flaky and failing tests). Counts, sums, maxima, filters, sorting and paging run in SQL; only the
/// percentiles and status sequences of the returned page come from a second, narrow streaming query.
/// None of it caps rows: callers pass the limit they want.
/// </summary>
public interface ITestAggregateQueries
{
    /// <summary>The columns <see cref="QuerySuitesAsync"/> can sort by.</summary>
    static readonly IReadOnlyList<string> SuiteSortColumns =
        ["runs", "ci_runs", "local_runs", "tests_in_last_run", "avg_run_ms", "p95_run_ms", "failed_runs", "distinct_tests", "branches"];

    /// <summary>The columns <see cref="QueryTestsAsync"/> can sort by.</summary>
    static readonly IReadOnlyList<string> TestSortColumns =
        ["runs", "passed", "failed", "skipped", "fail_rate", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms"];

    static readonly SortSpec DefaultSuiteSort = new("runs", true);
    static readonly SortSpec DefaultTestSort = new("total_ms", true);

    /// <summary>
    /// One row per suite with runs in the scope's window. Suites are few, so all are aggregated and sorted
    /// here; <paramref name="offset"/> and <paramref name="limit"/> page the sorted list. Ties break by suite id.
    /// Null values sort as -1.
    /// </summary>
    Task<(IReadOnlyList<TestSuiteRow> Rows, int Total)> QuerySuitesAsync(
        InsightsScope scope, string? testRunner, SortSpec sort, int offset, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// One row per test (or per class, see <see cref="TestsFilter.GroupBy"/>) over the window, sorted by
    /// <paramref name="sort"/> (one of <see cref="TestSortColumns"/>) and paged. Rows include percentiles and status sequences.
    /// <para>
    /// <c>class_name</c> and <c>name</c> are the greatest (ordinal, by the database) values seen across the
    /// group's executions, so they are stable between calls. Null averages, maxima, totals and rates sort as -1.
    /// Ties break by suite repository, project and runner, then key (not by the hashed suite id, which SQL cannot order by).
    /// </para>
    /// <para>
    /// Sorting by <c>p50_ms</c> or <c>p95_ms</c> loads a narrow (group key, duration) projection of the groups that
    /// passed the filters, because a percentile cannot be ordered in SQL on both providers; every other sort is
    /// ORDER BY with Skip/Take in SQL.
    /// </para>
    /// </summary>
    Task<(IReadOnlyList<TestAggregateRow> Rows, int Total)> QueryTestsAsync(
        InsightsScope scope, TestsFilter filter, SortSpec sort, int offset, int limit, CancellationToken cancellationToken);
}

[RegisterPerRequest]
public sealed class TestAggregateQueries : ITestAggregateQueries
{
    private const int SEQUENCE_LENGTH = 30;
    private const int KEY_CHUNK = 500;

    private readonly TelemetryDbContext _db;
    private readonly Dictionary<TestSuiteIdentity, string> _suiteIds = new();

    public TestAggregateQueries(TelemetryDbContext db)
    {
        _db = db;
    }

    // ---- suites ----------------------------------------------------------------------------

    public async Task<(IReadOnlyList<TestSuiteRow> Rows, int Total)> QuerySuitesAsync(
        InsightsScope scope, string? testRunner, SortSpec sort, int offset, int limit, CancellationToken cancellationToken)
    {
        var runs = scope.Apply(_db.TestRuns);
        var cases = scope.Apply(_db.TestCases).Facts();
        if (testRunner != null)
        {
            runs = runs.Where(r => r.TestRunner == testRunner);
            cases = cases.Where(f => f.TestRunner == testRunner);
        }

        var counts = await runs
            .GroupBy(r => new { r.RepositoryName, r.ProjectName, r.TestRunner })
            .Select(g => new
            {
                g.Key.RepositoryName,
                g.Key.ProjectName,
                g.Key.TestRunner,
                Runs = g.Count(),
                CiRuns = g.Count(r => r.ExecutionEnvironment == "CI"),
                LocalRuns = g.Count(r => r.ExecutionEnvironment == "Local"),
                LastRunAt = g.Max(r => r.ReceivedAt),
                AvgRunMs = g.Average(r => r.TotalDurationMs),
                FailedRuns = g.Count(r => r.FailedTests > 0),
                Branches = g.Select(r => r.Branch).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var distinctTests = (await cases
                .GroupBy(f => new { f.Repository, f.Project, f.TestRunner })
                .Select(g => new { g.Key.Repository, g.Key.Project, g.Key.TestRunner, Tests = g.Select(f => f.NameKey).Distinct().Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Repository, x.Project, x.TestRunner), x => x.Tests);

        // Run durations and the latest run's size per suite: a narrow projection, one small row per run,
        // because the 95th percentile cannot be computed in SQL on both providers.
        var durations = new Dictionary<(string, string, string), List<double>>();
        var latest = new Dictionary<(string, string, string), (DateTime At, string Id, int? Tests)>();
        await foreach (var r in runs
                           .Select(r => new { r.RepositoryName, r.ProjectName, r.TestRunner, r.ReceivedAt, r.Id, r.TotalTests, r.TotalDurationMs })
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var key = (r.RepositoryName, r.ProjectName, r.TestRunner);
            if (r.TotalDurationMs.HasValue)
            {
                if (!durations.TryGetValue(key, out var list)) durations[key] = list = [];
                list.Add(r.TotalDurationMs.Value);
            }

            if (!latest.TryGetValue(key, out var cur) ||
                r.ReceivedAt > cur.At || (r.ReceivedAt == cur.At && string.CompareOrdinal(r.Id, cur.Id) > 0))
                latest[key] = (r.ReceivedAt, r.Id, r.TotalTests);
        }

        var rows = counts.Select(c =>
        {
            var key = (c.RepositoryName, c.ProjectName, c.TestRunner);
            return new TestSuiteRow
            {
                SuiteId = SuiteId(new TestSuiteIdentity(c.RepositoryName, c.ProjectName, c.TestRunner)),
                Repository = c.RepositoryName,
                Project = c.ProjectName,
                TestRunner = c.TestRunner,
                Runs = c.Runs,
                CiRuns = c.CiRuns,
                LocalRuns = c.LocalRuns,
                LastRunAt = Utc(c.LastRunAt),
                TestsInLastRun = latest.TryGetValue(key, out var l) ? l.Tests : null,
                AvgRunMs = Stats.Ms(c.AvgRunMs),
                P95RunMs = durations.TryGetValue(key, out var d) ? Stats.Ms(Stats.Percentile(d, 0.95)) : null,
                FailedRuns = c.FailedRuns,
                DistinctTests = distinctTests.GetValueOrDefault(key),
                Branches = c.Branches
            };
        }).ToList();

        double Value(TestSuiteRow r) => sort.Column switch
        {
            "runs" => r.Runs,
            "ci_runs" => r.CiRuns,
            "local_runs" => r.LocalRuns,
            "tests_in_last_run" => r.TestsInLastRun ?? -1,
            "avg_run_ms" => r.AvgRunMs ?? -1,
            "p95_run_ms" => r.P95RunMs ?? -1,
            "failed_runs" => r.FailedRuns,
            "distinct_tests" => r.DistinctTests,
            "branches" => r.Branches,
            _ => throw new InsightsArgumentException("sort", $"'{sort.Column}' is not sortable")
        };

        var ordered = (sort.Descending ? rows.OrderByDescending(Value) : rows.OrderBy(Value))
            .ThenBy(r => r.SuiteId, StringComparer.Ordinal)
            .Skip(offset).Take(limit).ToList();
        return (ordered, rows.Count);
    }

    // ---- tests -----------------------------------------------------------------------------

    public async Task<(IReadOnlyList<TestAggregateRow> Rows, int Total)> QueryTestsAsync(
        InsightsScope scope, TestsFilter filter, SortSpec sort, int offset, int limit, CancellationToken cancellationToken)
    {
        var facts = BuildFacts(scope, filter);
        var groups = ApplyGroupFilters(Aggregate(facts, filter.GroupBy), filter);

        List<TestAgg> page;
        int total;
        if (sort.Column is "p50_ms" or "p95_ms")
        {
            var all = await groups.ToListAsync(cancellationToken);
            total = all.Count;
            var percentiles = await LoadPercentilesAsync(facts, filter.GroupBy, all, sort.Column == "p50_ms" ? 0.5 : 0.95, cancellationToken);
            double Value(TestAgg a) => percentiles.TryGetValue(KeyOf(a), out var v) && v.HasValue ? v.Value : -1;
            page = (sort.Descending ? all.OrderByDescending(Value) : all.OrderBy(Value))
                .ThenBy(a => a.Repository, StringComparer.Ordinal)
                .ThenBy(a => a.Project, StringComparer.Ordinal)
                .ThenBy(a => a.TestRunner, StringComparer.Ordinal)
                .ThenBy(a => a.Key, StringComparer.Ordinal)
                .Skip(offset).Take(limit).ToList();
        }
        else
        {
            total = await groups.CountAsync(cancellationToken);
            page = await OrderGroups(groups, sort).Skip(offset).Take(limit).ToListAsync(cancellationToken);
        }

        var rows = await EnrichAsync(facts, filter.GroupBy, page, cancellationToken);
        return (rows, total);
    }

    private IQueryable<TestCaseFact> BuildFacts(InsightsScope scope, TestsFilter filter) =>
        ApplyExecutionFilters(scope.Apply(_db.TestCases).Facts(), filter);

    /// <summary>The suite, runner, name, class and key filters, which apply to executions before grouping. Shared with the analysis queries.</summary>
    internal static IQueryable<TestCaseFact> ApplyExecutionFilters(IQueryable<TestCaseFact> facts, TestsFilter filter)
    {
        if (filter.Suite != null) facts = facts.InSuite(filter.Suite);
        if (filter.TestRunner != null)
        {
            var runner = filter.TestRunner;
            facts = facts.Where(f => f.TestRunner == runner);
        }

        if (!string.IsNullOrEmpty(filter.NameContains))
        {
            var text = filter.NameContains.ToLowerInvariant();
            facts = facts.Where(f => f.Name.ToLower().Contains(text) || f.NameKey.ToLower().Contains(text));
        }

        if (!string.IsNullOrEmpty(filter.ClassContains))
        {
            var text = filter.ClassContains.ToLowerInvariant();
            facts = facts.Where(f => f.ClassName != null && f.ClassName.ToLower().Contains(text));
        }

        if (filter.NameKeys != null)
        {
            var keys = filter.NameKeys.ToList();
            facts = facts.Where(f => keys.Contains(f.NameKey));
        }

        return facts;
    }

    private static IQueryable<TestAgg> Aggregate(IQueryable<TestCaseFact> facts, TestGrouping grouping)
    {
        var grouped = grouping == TestGrouping.Test
            ? facts.GroupBy(f => new GroupKey { Repository = f.Repository, Project = f.Project, TestRunner = f.TestRunner, Key = f.NameKey })
            : facts.GroupBy(f => new GroupKey
            {
                Repository = f.Repository,
                Project = f.Project,
                TestRunner = f.TestRunner,
                Key = f.ClassName == null || f.ClassName == "" ? TestsFilter.NO_CLASS : f.ClassName
            });

        return grouped.Select(g => new TestAgg
        {
            Repository = g.Key.Repository,
            Project = g.Key.Project,
            TestRunner = g.Key.TestRunner,
            Key = g.Key.Key,
            ClassName = g.Max(f => f.ClassName),
            Name = g.Max(f => f.Name)!,
            Runs = g.Count(),
            Passed = g.Count(f => f.Status == "Passed"),
            Failed = g.Count(f => f.Status == "Failed"),
            Skipped = g.Count(f => f.Status == "Skipped"),
            AvgMs = g.Average(f => f.DurationMs),
            MaxMs = g.Max(f => f.DurationMs),
            TotalMs = g.Sum(f => f.DurationMs),
            FirstSeenAt = g.Min(f => f.ReceivedAt),
            LastRunAt = g.Max(f => f.ReceivedAt),
            LastFailedAt = g.Where(f => f.Status == "Failed").Max(f => (DateTime?)f.ReceivedAt)
        });
    }

    private static IQueryable<TestAgg> ApplyGroupFilters(IQueryable<TestAgg> groups, TestsFilter filter)
    {
        if (filter.MinRuns is { } minRuns) groups = groups.Where(a => a.Runs >= minRuns);
        if (filter.MinAvgMs is { } minAvg) groups = groups.Where(a => a.AvgMs >= minAvg);
        return filter.Status switch
        {
            TestStatusFilter.HasFailures => groups.Where(a => a.Failed > 0),
            TestStatusFilter.HasPassesAndFailures => groups.Where(a => a.Passed > 0 && a.Failed > 0),
            TestStatusFilter.AlwaysFailed => groups.Where(a => a.Failed > 0 && a.Passed == 0),
            // Cases of one run share ReceivedAt, so "the latest failure is the latest execution" means a case failed in the latest run.
            TestStatusFilter.LastFailed => groups.Where(a => a.LastFailedAt != null && a.LastFailedAt == a.LastRunAt),
            _ => groups
        };
    }

    private static IOrderedQueryable<TestAgg> OrderGroups(IQueryable<TestAgg> groups, SortSpec sort)
    {
        System.Linq.Expressions.Expression<Func<TestAgg, double>> key = sort.Column switch
        {
            "runs" => a => a.Runs,
            "passed" => a => a.Passed,
            "failed" => a => a.Failed,
            "skipped" => a => a.Skipped,
            "fail_rate" => a => a.Passed + a.Failed == 0 ? -1.0 : (double)a.Failed / (a.Passed + a.Failed),
            "avg_ms" => a => a.AvgMs ?? -1.0,
            "max_ms" => a => a.MaxMs ?? -1.0,
            "total_ms" => a => a.TotalMs ?? -1.0,
            _ => throw new InsightsArgumentException("sort", $"'{sort.Column}' is not sortable; use one of: {string.Join(", ", ITestAggregateQueries.TestSortColumns)}")
        };

        var ordered = sort.Descending ? groups.OrderByDescending(key) : groups.OrderBy(key);
        return ordered.ThenBy(a => a.Repository).ThenBy(a => a.Project).ThenBy(a => a.TestRunner).ThenBy(a => a.Key);
    }

    private static IQueryable<TestCaseFact> WhereKeyIn(IQueryable<TestCaseFact> facts, TestGrouping grouping, List<string> keys) =>
        grouping == TestGrouping.Test
            ? facts.Where(f => keys.Contains(f.NameKey))
            : facts.Where(f => keys.Contains(f.ClassName == null || f.ClassName == "" ? TestsFilter.NO_CLASS : f.ClassName));

    private static IQueryable<StreamRow> Stream(IQueryable<TestCaseFact> facts, TestGrouping grouping)
    {
        // Test mode orders by (ReceivedAt, CaseId). Class mode adds the run id so a run's cases are adjacent.
        var ordered = grouping == TestGrouping.Test
            ? facts.OrderBy(f => f.ReceivedAt).ThenBy(f => f.CaseId)
            : facts.OrderBy(f => f.ReceivedAt).ThenBy(f => f.TestRunId).ThenBy(f => f.CaseId);

        return grouping == TestGrouping.Test
            ? ordered.Select(f => new StreamRow
            {
                Repository = f.Repository, Project = f.Project, TestRunner = f.TestRunner, Key = f.NameKey,
                TestRunId = f.TestRunId, Status = f.Status, DurationMs = f.DurationMs
            })
            : ordered.Select(f => new StreamRow
            {
                Repository = f.Repository, Project = f.Project, TestRunner = f.TestRunner,
                Key = f.ClassName == null || f.ClassName == "" ? TestsFilter.NO_CLASS : f.ClassName,
                TestRunId = f.TestRunId, Status = f.Status, DurationMs = f.DurationMs
            });
    }

    /// <summary>
    /// Only for sorting by p50_ms / p95_ms: streams (group key, duration) for the groups that passed the
    /// filters, because percentiles cannot be ordered in SQL on both providers.
    /// </summary>
    private async Task<Dictionary<(string, string, string, string), double?>> LoadPercentilesAsync(
        IQueryable<TestCaseFact> facts, TestGrouping grouping, List<TestAgg> candidates, double p, CancellationToken ct)
    {
        var wanted = candidates.Select(KeyOf).ToHashSet();
        var durations = new Dictionary<(string, string, string, string), List<double>>();
        await foreach (var r in Stream(facts.Where(f => f.DurationMs != null), grouping).AsAsyncEnumerable().WithCancellation(ct))
        {
            var key = (r.Repository, r.Project, r.TestRunner, r.Key);
            if (!wanted.Contains(key)) continue;
            if (!durations.TryGetValue(key, out var list)) durations[key] = list = [];
            list.Add(r.DurationMs!.Value);
        }

        return durations.ToDictionary(kv => kv.Key, kv => Stats.Percentile(kv.Value, p));
    }

    /// <summary>
    /// The second, narrow query for a page: streams (key, run, status, duration) of just the page's groups in
    /// time order and folds them into percentiles and the last-30 status sequence.
    /// </summary>
    private async Task<IReadOnlyList<TestAggregateRow>> EnrichAsync(
        IQueryable<TestCaseFact> facts, TestGrouping grouping, List<TestAgg> page, CancellationToken ct)
    {
        var folds = new Dictionary<(string, string, string, string), Fold>();
        foreach (var chunk in page.Select(a => a.Key).Distinct().Chunk(KEY_CHUNK))
        {
            var keys = chunk.ToList();
            await foreach (var r in Stream(WhereKeyIn(facts, grouping, keys), grouping).AsAsyncEnumerable().WithCancellation(ct))
            {
                var key = (r.Repository, r.Project, r.TestRunner, r.Key);
                if (!folds.TryGetValue(key, out var fold)) folds[key] = fold = new Fold(grouping);
                fold.Add(r);
            }
        }

        return page.Select(a =>
        {
            var fold = folds.GetValueOrDefault(KeyOf(a)) ?? new Fold(grouping);
            fold.Finish();
            var suite = new TestSuiteIdentity(a.Repository, a.Project, a.TestRunner);
            return new TestAggregateRow
            {
                TestKey = TestKey.Format(SuiteId(suite), a.Key),
                SuiteId = SuiteId(suite),
                Repository = a.Repository,
                Project = a.Project,
                TestRunner = a.TestRunner,
                ClassName = grouping == TestGrouping.Class ? a.Key : a.ClassName,
                Name = grouping == TestGrouping.Class ? string.Empty : a.Name,
                Runs = a.Runs,
                Passed = a.Passed,
                Failed = a.Failed,
                Skipped = a.Skipped,
                FailRate = Stats.Ratio(a.Failed, a.Passed + a.Failed),
                AvgMs = Stats.Ms(a.AvgMs),
                P50Ms = Stats.Ms(Stats.Percentile(fold.Durations, 0.5)),
                P95Ms = Stats.Ms(Stats.Percentile(fold.Durations, 0.95)),
                MaxMs = Stats.Ms(a.MaxMs),
                TotalMs = Stats.Ms(a.TotalMs),
                FirstSeenAt = Utc(a.FirstSeenAt),
                LastRunAt = Utc(a.LastRunAt),
                LastStatus = fold.LastStatus,
                LastFailedAt = a.LastFailedAt.HasValue ? Utc(a.LastFailedAt.Value) : null,
                StatusSequence = new string(fold.Sequence.ToArray())
            };
        }).ToList();
    }

    private static (string, string, string, string) KeyOf(TestAgg a) => (a.Repository, a.Project, a.TestRunner, a.Key);

    private string SuiteId(TestSuiteIdentity suite)
    {
        if (!_suiteIds.TryGetValue(suite, out var id)) _suiteIds[suite] = id = suite.SuiteId;
        return id;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static char Letter(string status) => status switch
    {
        "Passed" => 'P',
        "Failed" => 'F',
        "Skipped" => 'S',
        _ => '?'
    };

    // ---- projections -----------------------------------------------------------------------

    private sealed class GroupKey
    {
        public string Repository { get; init; } = string.Empty;
        public string Project { get; init; } = string.Empty;
        public string TestRunner { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
    }

    private sealed class TestAgg
    {
        public string Repository { get; init; } = string.Empty;
        public string Project { get; init; } = string.Empty;
        public string TestRunner { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public string? ClassName { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Runs { get; init; }
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Skipped { get; init; }
        public double? AvgMs { get; init; }
        public double? MaxMs { get; init; }
        public double? TotalMs { get; init; }
        public DateTime FirstSeenAt { get; init; }
        public DateTime LastRunAt { get; init; }
        public DateTime? LastFailedAt { get; init; }
    }

    private sealed class StreamRow
    {
        public string Repository { get; init; } = string.Empty;
        public string Project { get; init; } = string.Empty;
        public string TestRunner { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public string TestRunId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public double? DurationMs { get; init; }
    }

    /// <summary>Folds one group's time-ordered executions into durations, the last-30 sequence and the last status.</summary>
    private sealed class Fold
    {
        private readonly TestGrouping _grouping;
        private string? _runId;
        private bool _anyFailed, _anyPassed, _anySkipped, _anyOther;

        public Fold(TestGrouping grouping) => _grouping = grouping;

        public List<double> Durations { get; } = [];
        public List<char> Sequence { get; } = [];
        public string LastStatus { get; private set; } = string.Empty;

        public void Add(StreamRow row)
        {
            if (row.DurationMs.HasValue) Durations.Add(row.DurationMs.Value);

            if (_grouping == TestGrouping.Test)
            {
                Push(Letter(row.Status));
                LastStatus = row.Status;
                return;
            }

            if (_runId != null && _runId != row.TestRunId) Flush();
            _runId = row.TestRunId;
            switch (row.Status)
            {
                case "Failed": _anyFailed = true; break;
                case "Passed": _anyPassed = true; break;
                case "Skipped": _anySkipped = true; break;
                default: _anyOther = true; break;
            }
        }

        /// <summary>Closes the last run of a class group.</summary>
        public void Finish()
        {
            if (_grouping == TestGrouping.Class && _runId != null) Flush();
        }

        private void Flush()
        {
            var letter = _anyFailed ? 'F' : _anyPassed ? 'P' : _anySkipped && !_anyOther ? 'S' : '?';
            Push(letter);
            LastStatus = letter switch { 'F' => "Failed", 'P' => "Passed", 'S' => "Skipped", _ => "Unknown" };
            _runId = null;
            _anyFailed = _anyPassed = _anySkipped = _anyOther = false;
        }

        private void Push(char letter)
        {
            Sequence.Add(letter);
            if (Sequence.Count > SEQUENCE_LENGTH) Sequence.RemoveAt(0);
        }
    }
}
