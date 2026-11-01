using System.Globalization;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>Shared argument parsing and paging for the test history datasets.</summary>
internal static class TestHistoryArgs
{
    public static readonly string[] ExecutionStatuses = ["passed", "failed", "skipped", "pending", "unknown"];

    /// <summary>The <c>status</c> argument as the stored normalised status (Passed, Failed, ...), or null.</summary>
    public static string? ExecutionStatus(InsightsArgs args)
    {
        var status = args.GetChoice("status", ExecutionStatuses);
        return status == null ? null : char.ToUpperInvariant(status[0]) + status[1..].ToLowerInvariant();
    }

    public static string KeysetId(int caseId) => caseId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Trims the extra row fetched to learn whether another page exists, and builds the next keyset cursor.
    /// </summary>
    public static (IReadOnlyList<T> Rows, string? NextCursor) Page<T>(
        IReadOnlyList<T> fetched, int limit, Func<T, string> cursorOf)
    {
        if (fetched.Count <= limit) return (fetched, null);
        var rows = fetched.Take(limit).ToList();
        return (rows, cursorOf(rows[^1]));
    }

    public static string Applied(InsightsScope scope, params (string Name, string? Value)[] filters) =>
        string.Join(" ", new[] { scope.Describe() }.Concat(
            filters.Where(f => f.Value != null).Select(f => f.Value!.Contains(' ') ? $"{f.Name}=\"{f.Value}\"" : $"{f.Name}={f.Value}")));
}

/// <summary>
/// <c>get_test</c>: the execution history of one test, newest first. Pass <c>test_key</c>, or <c>suite_id</c> and
/// <c>name_contains</c>; when the name matches several tests the candidates are returned instead.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetTestDataset : IInsightsDataset
{
    private static readonly TableSchema<TestExecutionRow> HistorySchema = new(
        ("received_at", r => r.ReceivedAt),
        ("test_run_id", r => r.TestRunId),
        ("status", r => r.Status),
        ("duration_ms", r => Stats.Ms(r.DurationMs)),
        ("branch", r => r.Branch),
        ("environment", r => r.Environment),
        ("platform", r => r.Platform),
        ("os", r => r.Os),
        ("cpu_count", r => r.CpuCount),
        ("run_total_ms", r => Stats.Ms(r.RunTotalMs)),
        ("error", r => r.ErrorFirstLine));

    private static readonly TableSchema<TestCandidate> CandidateSchema = new(
        ("test_key", r => r.TestKey),
        ("class_name", r => r.ClassName),
        ("name", r => r.Name),
        ("runs", r => r.Runs),
        ("last_run_at", r => r.LastRunAt));

    private readonly ITestHistoryQueries _queries;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public GetTestDataset(ITestHistoryQueries queries, ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.GET_TEST;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var status = TestHistoryArgs.ExecutionStatus(args);
        var testKey = args.GetString("test_key");
        var nameContains = args.GetString("name_contains");
        var suiteIdArg = args.GetString("suite_id");

        TestSuiteIdentity suite;
        string nameKey;
        if (testKey != null)
        {
            (suite, nameKey) = await _catalog.ResolveTestKeyAsync(testKey, cancellationToken);
        }
        else if (suiteIdArg != null && nameContains != null)
        {
            suite = await _catalog.ResolveAsync(suiteIdArg, cancellationToken);
            var (candidates, total) = await _queries.FindTestsAsync(scope, suite, nameContains, page.Limit, cancellationToken);
            if (total > 1)
            {
                return CandidateSchema.ToTable(candidates, total, null) with
                {
                    Window = scope.Window,
                    Applied = TestHistoryArgs.Applied(scope, ("suite_id", suiteIdArg), ("name_contains", nameContains)),
                    Notes = [$"{total} tests match name_contains; call get_test again with one test_key"]
                };
            }

            if (total == 0)
            {
                return Table.Empty(HistorySchema.Columns) with
                {
                    Window = scope.Window,
                    Applied = TestHistoryArgs.Applied(scope, ("suite_id", suiteIdArg), ("name_contains", nameContains)),
                    Notes = ["no test matches name_contains in this window"]
                };
            }

            nameKey = candidates[0].NameKey;
        }
        else
        {
            throw new InsightsArgumentException("test_key", "give test_key, or suite_id together with name_contains");
        }

        var after = Cursor.DecodeKeyset(page.Cursor);
        var result = await _queries.GetExecutionsAsync(
            scope, new TestExecutionFilter(suite, nameKey, Status: status), after, page.Limit + 1, cancellationToken);
        var (rows, next) = TestHistoryArgs.Page(result.Rows, page.Limit,
            r => Cursor.EncodeKeyset(r.ReceivedAt, TestHistoryArgs.KeysetId(r.CaseId)));

        return HistorySchema.ToTable(rows, result.TotalRows, next) with
        {
            Window = scope.Window,
            Applied = TestHistoryArgs.Applied(scope, ("test_key", TestKey.Format(suite.SuiteId, nameKey)), ("status", status?.ToLowerInvariant()))
        };
    }
}

/// <summary><c>get_test_failures</c>: failed executions with their error, newest first.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetTestFailuresDataset : IInsightsDataset
{
    private static readonly TableSchema<TestExecutionRow> Schema = new(
        ("received_at", r => r.ReceivedAt),
        ("test_key", r => r.TestKey),
        ("class_name", r => r.ClassName),
        ("name", r => r.Name),
        ("branch", r => r.Branch),
        ("environment", r => r.Environment),
        ("test_run_id", r => r.TestRunId),
        ("error", r => r.ErrorFirstLine));

    private readonly ITestHistoryQueries _queries;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public GetTestFailuresDataset(ITestHistoryQueries queries, ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.GET_TEST_FAILURES;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var suiteIdArg = args.GetString("suite_id");
        var testKey = args.GetString("test_key");
        var nameContains = args.GetString("name_contains");
        var classContains = args.GetString("class_contains");
        var errorContains = args.GetString("error_contains");

        TestSuiteIdentity? suite = null;
        string? nameKey = null;
        if (testKey != null)
            (suite, nameKey) = await _catalog.ResolveTestKeyAsync(testKey, cancellationToken);
        else if (suiteIdArg != null)
            suite = await _catalog.ResolveAsync(suiteIdArg, cancellationToken);

        var filter = new TestExecutionFilter(suite, nameKey, nameContains, classContains, errorContains, "Failed");
        var after = Cursor.DecodeKeyset(page.Cursor);
        var result = await _queries.GetExecutionsAsync(scope, filter, after, page.Limit + 1, cancellationToken);
        var (rows, next) = TestHistoryArgs.Page(result.Rows, page.Limit,
            r => Cursor.EncodeKeyset(r.ReceivedAt, TestHistoryArgs.KeysetId(r.CaseId)));

        return Schema.ToTable(rows, result.TotalRows, next) with
        {
            Window = scope.Window,
            Applied = TestHistoryArgs.Applied(scope, ("suite_id", suiteIdArg), ("test_key", testKey),
                ("name_contains", nameContains), ("class_contains", classContains), ("error_contains", errorContains))
        };
    }
}

/// <summary><c>list_test_runs</c>: runs of a suite (or of every suite in scope), newest first.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class ListTestRunsDataset : IInsightsDataset
{
    private static readonly TableSchema<TestRunRow> Schema = new(
        ("test_run_id", r => r.Id),
        ("suite_id", r => r.Suite.SuiteId),
        ("received_at", r => r.ReceivedAt),
        ("branch", r => r.Branch),
        ("environment", r => r.Environment),
        ("platform", r => r.Platform),
        ("os", r => r.Os),
        ("cpu_count", r => r.CpuCount),
        ("total", r => r.TotalTests),
        ("passed", r => r.PassedTests),
        ("failed", r => r.FailedTests),
        ("skipped", r => r.SkippedTests),
        ("total_ms", r => Stats.Ms(r.TotalDurationMs)));

    private readonly ITestHistoryQueries _queries;
    private readonly ITestSuiteCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public ListTestRunsDataset(ITestHistoryQueries queries, ITestSuiteCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.LIST_TEST_RUNS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var suiteIdArg = args.GetString("suite_id");
        var runner = args.GetString("test_runner");
        var status = args.GetChoice("status", "failed", "passed")?.ToLowerInvariant();

        TestSuiteIdentity? suite = suiteIdArg == null ? null : await _catalog.ResolveAsync(suiteIdArg, cancellationToken);
        var after = Cursor.DecodeKeyset(page.Cursor);
        var result = await _queries.GetRunsAsync(scope, new TestRunFilter(suite, runner, status), after, page.Limit + 1, cancellationToken);
        var (rows, next) = TestHistoryArgs.Page(result.Rows, page.Limit, r => Cursor.EncodeKeyset(r.ReceivedAt, r.Id));

        return Schema.ToTable(rows, result.TotalRows, next) with
        {
            Window = scope.Window,
            Applied = TestHistoryArgs.Applied(scope, ("suite_id", suiteIdArg), ("test_runner", runner), ("status", status))
        };
    }
}

/// <summary>
/// <c>get_test_run</c>: the cases of one run. The run header (no user or host) goes into the summary notes.
/// The window does not apply.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetTestRunDataset : IInsightsDataset
{
    public static readonly string[] SortColumns = ["duration_ms", "status", "name"];

    private static readonly TableSchema<TestRunCaseRow> Schema = new(
        ("test_key", r => r.TestKey),
        ("class_name", r => r.ClassName),
        ("name", r => r.Name),
        ("status", r => r.Status),
        ("duration_ms", r => Stats.Ms(r.DurationMs)),
        ("error", r => r.ErrorFirstLine));

    private readonly ITestHistoryQueries _queries;

    public GetTestRunDataset(ITestHistoryQueries queries)
    {
        _queries = queries;
    }

    public string Name => DatasetNames.GET_TEST_RUN;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var runId = args.GetString("test_run_id")
            ?? throw new InsightsArgumentException("test_run_id", "is required; take it from the test_run_id column of list_test_runs or get_test");
        var status = TestHistoryArgs.ExecutionStatus(args);
        var nameContains = args.GetString("name_contains");
        var sortText = args.GetString("sort");
        var sort = SortSpec.Parse(sortText, SortColumns, new SortSpec("default", true));

        var run = await _queries.GetRunAsync(runId, cancellationToken)
            ?? throw new InsightsArgumentException("test_run_id", $"no test run '{runId}'; list_test_runs shows the run ids");

        var sortKind = sortText == null
            ? TestRunCaseSort.Default
            : sort.Column switch
            {
                "duration_ms" => TestRunCaseSort.DurationMs,
                "status" => TestRunCaseSort.Status,
                _ => TestRunCaseSort.Name
            };

        var offset = Cursor.DecodeOffset(page.Cursor);
        var (rows, total) = await _queries.GetRunCasesAsync(
            runId, run.Suite, status, nameContains, sortKind, sort.Descending, offset, page.Limit, cancellationToken);

        return Schema.ToTable(rows, total, Cursor.NextOffset(offset, rows.Count, total)) with
        {
            Applied = string.Join(" ", new[] { $"test_run_id={runId}", status == null ? null : $"status={status.ToLowerInvariant()}",
                nameContains == null ? null : $"name_contains=\"{nameContains}\"", sortText == null ? null : $"sort=\"{sort}\"" }
                .Where(x => x != null)),
            Notes = [Header(run)]
        };
    }

    private static string Header(TestRunRow r) =>
        string.Create(CultureInfo.InvariantCulture,
            $"run {r.Id} suite {r.Suite.SuiteId} ({r.Suite.Repository} / {r.Suite.Project} / {r.Suite.TestRunner}) " +
            $"{r.ReceivedAt.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ} branch={r.Branch} environment={r.Environment} " +
            $"platform={r.Platform} os={r.Os} cpu_count={r.CpuCount} total={r.TotalTests} passed={r.PassedTests} " +
            $"failed={r.FailedTests} skipped={r.SkippedTests} total_ms={Stats.Ms(r.TotalDurationMs)}");
}
