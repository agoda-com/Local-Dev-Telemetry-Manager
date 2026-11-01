using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary><c>get_tests</c>: one row per test (or class) aggregated over the window.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class TestsDataset : IInsightsDataset
{
    private static readonly TableSchema<TestAggregateRow> Schema = new(
        ("test_key", r => r.TestKey),
        ("suite_id", r => r.SuiteId),
        ("repository", r => r.Repository),
        ("project", r => r.Project),
        ("test_runner", r => r.TestRunner),
        ("class_name", r => r.ClassName),
        ("name", r => r.Name),
        ("runs", r => r.Runs),
        ("passed", r => r.Passed),
        ("failed", r => r.Failed),
        ("skipped", r => r.Skipped),
        ("fail_rate", r => r.FailRate),
        ("avg_ms", r => r.AvgMs),
        ("p50_ms", r => r.P50Ms),
        ("p95_ms", r => r.P95Ms),
        ("max_ms", r => r.MaxMs),
        ("total_ms", r => r.TotalMs),
        ("first_seen_at", r => r.FirstSeenAt),
        ("last_run_at", r => r.LastRunAt),
        ("last_status", r => r.LastStatus),
        ("last_failed_at", r => r.LastFailedAt),
        ("status_sequence", r => r.StatusSequence));

    private readonly ITestAggregateQueries _queries;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public TestsDataset(ITestAggregateQueries queries, ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.GET_TESTS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var sort = SortSpec.Parse(args.GetString("sort"), ITestAggregateQueries.TestSortColumns, ITestAggregateQueries.DefaultTestSort);
        var groupBy = args.GetChoice("group_by", "test", "class") == "class" ? TestGrouping.Class : TestGrouping.Test;
        var status = args.GetChoice("status", "has_failures", "has_passes_and_failures", "last_failed", "always_failed") switch
        {
            "has_failures" => TestStatusFilter.HasFailures,
            "has_passes_and_failures" => TestStatusFilter.HasPassesAndFailures,
            "last_failed" => TestStatusFilter.LastFailed,
            "always_failed" => TestStatusFilter.AlwaysFailed,
            _ => TestStatusFilter.None
        };
        var minRuns = args.GetInt("min_runs");
        if (minRuns < 0) throw new InsightsArgumentException("min_runs", "must not be negative");
        var minAvg = args.GetDouble("min_avg_ms");
        if (minAvg < 0) throw new InsightsArgumentException("min_avg_ms", "must not be negative");

        var suiteId = args.GetString("suite_id");
        var filter = new TestsFilter
        {
            GroupBy = groupBy,
            Suite = suiteId == null ? null : await _catalog.ResolveAsync(suiteId, cancellationToken),
            TestRunner = args.GetString("test_runner"),
            NameContains = args.GetString("name_contains"),
            ClassContains = args.GetString("class_contains"),
            MinRuns = minRuns,
            MinAvgMs = minAvg,
            Status = status
        };

        var offset = Cursor.DecodeOffset(page.Cursor);
        var (rows, total) = await _queries.QueryTestsAsync(scope, filter, sort, offset, page.Limit, cancellationToken);
        var extra = args.ToDisplayString([.. InsightsScope.ParameterNames, "sort"]);
        return Schema.ToTable(rows, total, Cursor.NextOffset(offset, rows.Count, total)) with
        {
            Window = scope.Window,
            Applied = scope.Describe() + (extra.Length > 0 ? " " + extra : "") + $" sort=\"{sort}\""
        };
    }
}
