using System.Globalization;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>One <c>find_slow_tests</c> row: the get_tests row, its share of suite time and the explanation.</summary>
public sealed record SlowTestRow(TestAggregateRow Test, double? ShareOfSuiteTimePct, string Reason, string Evidence);

/// <summary>
/// <c>find_slow_tests</c>: tests (or classes) ranked by total, p95 or average duration. A thin layer over
/// <see cref="ITestAggregateQueries.QueryTestsAsync"/> (sort, paging and aggregation all happen there) that adds each
/// row's share of its suite's total time, computed from per-suite SQL totals.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class SlowTestsDataset : IInsightsDataset
{
    public const int DEFAULT_MIN_RUNS = 3;

    private static readonly TableSchema<SlowTestRow> Schema = new(
        ("test_key", r => r.Test.TestKey),
        ("suite_id", r => r.Test.SuiteId),
        ("repository", r => r.Test.Repository),
        ("project", r => r.Test.Project),
        ("test_runner", r => r.Test.TestRunner),
        ("class_name", r => r.Test.ClassName),
        ("name", r => r.Test.Name),
        ("runs", r => r.Test.Runs),
        ("passed", r => r.Test.Passed),
        ("failed", r => r.Test.Failed),
        ("skipped", r => r.Test.Skipped),
        ("fail_rate", r => r.Test.FailRate),
        ("avg_ms", r => r.Test.AvgMs),
        ("p50_ms", r => r.Test.P50Ms),
        ("p95_ms", r => r.Test.P95Ms),
        ("max_ms", r => r.Test.MaxMs),
        ("total_ms", r => r.Test.TotalMs),
        ("first_seen_at", r => r.Test.FirstSeenAt),
        ("last_run_at", r => r.Test.LastRunAt),
        ("last_status", r => r.Test.LastStatus),
        ("last_failed_at", r => r.Test.LastFailedAt),
        ("status_sequence", r => r.Test.StatusSequence),
        ("share_of_suite_time", r => r.ShareOfSuiteTimePct),
        (Evidence.REASON, r => r.Reason),
        (Evidence.EVIDENCE, r => r.Evidence));

    private readonly ITestAggregateQueries _queries;
    private readonly ITestAnalysisQueries _analysis;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public SlowTestsDataset(
        ITestAggregateQueries queries, ITestAnalysisQueries analysis, ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _analysis = analysis;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.FIND_SLOW_TESTS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var sortBy = args.GetChoice("sort_by", "total", "p95", "avg") ?? "total";
        var groupBy = args.GetChoice("group_by", "test", "class") ?? "test";
        var minRuns = TestAnalysisArgs.MinInt(args, "min_runs", DEFAULT_MIN_RUNS);
        var filter = (await TestAnalysisArgs.FilterAsync(args, _catalog, cancellationToken)) with
        {
            GroupBy = groupBy == "class" ? TestGrouping.Class : TestGrouping.Test,
            MinRuns = minRuns
        };
        var sort = new SortSpec(sortBy switch { "p95" => "p95_ms", "avg" => "avg_ms", _ => "total_ms" }, true);

        var offset = Cursor.DecodeOffset(page.Cursor);
        var (rows, total) = await _queries.QueryTestsAsync(scope, filter, sort, offset, page.Limit, cancellationToken);
        var suiteTotals = await _analysis.GetSuiteTotalsAsync(scope, cancellationToken);

        var result = rows.Select(r =>
        {
            var suite = new TestSuiteIdentity(r.Repository, r.Project, r.TestRunner);
            var suiteTotal = suiteTotals.GetValueOrDefault(suite);
            // Percent (0-100) of the suite's total test time in the window: Stats.Ratio x 100, two decimals.
            var share = r.TotalMs.HasValue && Stats.Ratio(r.TotalMs.Value, suiteTotal) is { } ratio ? Math.Round(ratio * 100, 2) : (double?)null;
            return new SlowTestRow(r, share, Reason(r, sortBy, share, groupBy == "class"), EvidenceFor(scope, r, groupBy == "class"));
        }).ToList();

        return Schema.ToTable(result, total, Cursor.NextOffset(offset, result.Count, total)) with
        {
            Window = scope.Window,
            Applied = TestAnalysisArgs.Applied(scope, args, ("sort_by", sortBy), ("group_by", groupBy), ("min_runs", minRuns))
        };
    }

    private static string Reason(TestAggregateRow r, string sortBy, double? sharePct, bool byClass)
    {
        var inv = CultureInfo.InvariantCulture;
        var lead = sortBy switch
        {
            "p95" => $"p95 {r.P95Ms?.ToString(inv)} ms",
            "avg" => $"avg {r.AvgMs?.ToString(inv)} ms",
            _ => $"total {r.TotalMs?.ToString(inv)} ms"
        };
        var detail = sortBy switch
        {
            "p95" => $"avg {r.AvgMs?.ToString(inv)} ms, total {r.TotalMs?.ToString(inv)} ms",
            "avg" => $"p95 {r.P95Ms?.ToString(inv)} ms, total {r.TotalMs?.ToString(inv)} ms",
            _ => $"avg {r.AvgMs?.ToString(inv)} ms, p95 {r.P95Ms?.ToString(inv)} ms"
        };
        var share = sharePct.HasValue ? $"; {sharePct.Value.ToString("0.##", inv)}% of suite time" : string.Empty;
        return $"{lead} over {r.Runs} {(byClass ? "executions in the class" : "runs")} ({detail}){share}";
    }

    private static string EvidenceFor(InsightsScope scope, TestAggregateRow r, bool byClass)
    {
        if (!byClass)
            return Evidence.Call(DatasetNames.GET_TEST, Evidence.ScopeArgs(scope).With("test_key", r.TestKey));

        var args = Evidence.ScopeArgs(scope).With("suite_id", r.SuiteId).With("group_by", "class");
        if (r.ClassName != TestsFilter.NO_CLASS) args.With("class_contains", r.ClassName);
        return Evidence.Call(DatasetNames.GET_TESTS, args);
    }
}
