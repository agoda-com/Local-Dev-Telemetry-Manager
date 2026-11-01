using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class TestCatalogTools
{
    private readonly IInsightsToolRunner _runner;

    public TestCatalogTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.LIST_TEST_SUITES, Title = "List test suites", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Test suites (repository + project + test runner) that have runs in the window, one row per suite. Start here to find a suite_id for get_tests.
        Columns: suite_id, repository, project, test_runner, runs, ci_runs, local_runs, last_run_at, tests_in_last_run, avg_run_ms, p95_run_ms, failed_runs, distinct_tests, branches.
        failed_runs counts runs with at least one failed test; distinct_tests counts distinct tests seen in the window; tests_in_last_run is the size of the latest run.
        Filters: repository, project, branch, environment, days/from/to, test_runner.
        Sort: runs, ci_runs, local_runs, tests_in_last_run, avg_run_ms, p95_run_ms, failed_runs, distinct_tests, branches. Default runs desc.
        """)]
    public Task<CallToolResult> ListTestSuites(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Test runner, as listed by list_scopes kind=test_runner, for example NUnit or Vitest.")] string? test_runner = null,
        [Description(ToolArgs.SORT)] string? sort = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.LIST_TEST_SUITES,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("test_runner", test_runner)
                .With("sort", sort),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_TESTS, Title = "Get tests", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        One row per test, aggregated over the window, for one suite or across suites. The raw material for questions about slow, flaky or failing tests.
        Columns: test_key, suite_id, repository, project, test_runner, class_name, name, runs, passed, failed, skipped, fail_rate, avg_ms, p50_ms, p95_ms, max_ms, total_ms, first_seen_at, last_run_at, last_status, last_failed_at, status_sequence.
        runs counts executions of any status; fail_rate = failed / (passed + failed), empty when both are 0. Durations are whole ms over executions that have a duration.
        status_sequence is the last 30 results in the window, oldest first, one letter each: P passed, F failed, S skipped, ? other.
        test_key is suite_id:name where name is the full name, else class.name; pass it to get_test.
        Filters: repository, project, branch, environment, days/from/to, suite_id, test_runner, name_contains and class_contains (case-insensitive), min_runs, min_avg_ms,
        status = has_failures (failed > 0) | has_passes_and_failures (both passed and failed > 0) | last_failed (the latest execution failed) | always_failed (failed > 0, never passed).
        Sort: runs, passed, failed, skipped, fail_rate, avg_ms, p50_ms, p95_ms, max_ms, total_ms. Default total_ms desc. Empty values sort last when descending.
        group_by=class rolls the same columns up to suite + class: test_key is suite_id:class_name (a missing class is "(none)"), name is empty, runs counts executions of any test in the class,
        and status_sequence is per run: F if any case in that run failed, else P if any passed, else S if all were skipped, else ?.
        """)]
    public Task<CallToolResult> GetTests(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this suite, from list_test_suites.")] string? suite_id = null,
        [Description("Test runner, for example NUnit or Vitest.")] string? test_runner = null,
        [Description("Only tests whose name or full name contains this text (case-insensitive).")] string? name_contains = null,
        [Description("Only tests whose class contains this text (case-insensitive).")] string? class_contains = null,
        [Description("Only tests with at least this many executions in the window.")] int? min_runs = null,
        [Description("Only tests whose average duration is at least this many ms.")] double? min_avg_ms = null,
        [Description("has_failures | has_passes_and_failures | last_failed | always_failed.")] string? status = null,
        [Description("test (default) or class.")] string? group_by = null,
        [Description(ToolArgs.SORT)] string? sort = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_TESTS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id)
                .With("test_runner", test_runner)
                .With("name_contains", name_contains)
                .With("class_contains", class_contains)
                .With("min_runs", min_runs)
                .With("min_avg_ms", min_avg_ms)
                .With("status", status)
                .With("group_by", group_by)
                .With("sort", sort),
            limit, cursor, format, cancellationToken);
}
