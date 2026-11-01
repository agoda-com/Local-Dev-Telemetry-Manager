using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary><c>list_test_suites</c>: one row per test suite (repository + project + runner) with runs in the window.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class TestSuitesDataset : IInsightsDataset
{
    private static readonly TableSchema<TestSuiteRow> Schema = new(
        ("suite_id", r => r.SuiteId),
        ("repository", r => r.Repository),
        ("project", r => r.Project),
        ("test_runner", r => r.TestRunner),
        ("runs", r => r.Runs),
        ("ci_runs", r => r.CiRuns),
        ("local_runs", r => r.LocalRuns),
        ("last_run_at", r => r.LastRunAt),
        ("tests_in_last_run", r => r.TestsInLastRun),
        ("avg_run_ms", r => r.AvgRunMs),
        ("p95_run_ms", r => r.P95RunMs),
        ("failed_runs", r => r.FailedRuns),
        ("distinct_tests", r => r.DistinctTests),
        ("branches", r => r.Branches));

    private readonly ITestAggregateQueries _queries;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public TestSuitesDataset(ITestAggregateQueries queries, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.LIST_TEST_SUITES;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var runner = args.GetString("test_runner");
        var sort = SortSpec.Parse(args.GetString("sort"), ITestAggregateQueries.SuiteSortColumns, ITestAggregateQueries.DefaultSuiteSort);
        var offset = Cursor.DecodeOffset(page.Cursor);

        var (rows, total) = await _queries.QuerySuitesAsync(scope, runner, sort, offset, page.Limit, cancellationToken);
        var applied = scope.Describe() + (runner != null ? $" test_runner={runner}" : "") + $" sort=\"{sort}\"";
        return Schema.ToTable(rows, total, Cursor.NextOffset(offset, rows.Count, total)) with
        {
            Window = scope.Window,
            Applied = applied
        };
    }
}
