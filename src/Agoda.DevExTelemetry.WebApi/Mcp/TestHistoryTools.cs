using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

/// <summary>Raw test history: one test's executions, failures, runs and one run's cases.</summary>
[McpServerToolType]
public sealed class TestHistoryTools
{
    private readonly IInsightsToolRunner _runner;

    public TestHistoryTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.GET_TEST, Title = "Get test history", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        The execution history of one test, one row per execution, newest first.
        Identify the test with test_key (from get_tests or get_test_failures), or with suite_id plus name_contains
        (case-insensitive substring of the test name). If name_contains matches several tests, no history is returned;
        you get the candidates instead (columns test_key, class_name, name, runs, last_run_at) and a note: call again with one test_key.
        Columns: received_at, test_run_id, status, duration_ms, branch, environment, platform, os, cpu_count, run_total_ms, error.
        environment is the stored Local/CI value; run_total_ms is the whole run's duration; error is the first line of the error message.
        Filters: test_key | (suite_id + name_contains), status, plus repository, project, branch, environment, days/from/to.
        """)]
    public Task<CallToolResult> GetTest(
        [Description("Test key from get_tests, for example ab12cd34:Namespace.Class.Test. Preferred.")] string? test_key = null,
        [Description("Suite id from list_test_suites; use with name_contains when you have no test_key.")] string? suite_id = null,
        [Description("Case-insensitive substring of the test name, within suite_id.")] string? name_contains = null,
        [Description("Only executions with this status: passed | failed | skipped | pending | unknown.")] string? status = null,
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_TEST,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("test_key", test_key).With("suite_id", suite_id).With("name_contains", name_contains).With("status", status),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_TEST_FAILURES, Title = "Get test failures", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Failed test executions with their error, newest first, across a suite or the whole scope.
        Columns: received_at, test_key, class_name, name, branch, environment, test_run_id, error (first line of the message).
        Filters: suite_id, test_key, name_contains, class_contains, error_contains (all case-insensitive substrings), plus repository, project, branch, environment, days/from/to.
        """)]
    public Task<CallToolResult> GetTestFailures(
        [Description("Only this suite (from list_test_suites).")] string? suite_id = null,
        [Description("Only this test (from get_tests).")] string? test_key = null,
        [Description("Case-insensitive substring of the test name.")] string? name_contains = null,
        [Description("Case-insensitive substring of the class name.")] string? class_contains = null,
        [Description("Case-insensitive substring of the error message.")] string? error_contains = null,
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_TEST_FAILURES,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id).With("test_key", test_key).With("name_contains", name_contains)
                .With("class_contains", class_contains).With("error_contains", error_contains),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.LIST_TEST_RUNS, Title = "List test runs", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Test runs of a suite (or of every suite in scope), newest first, with their counts.
        Columns: test_run_id, suite_id, received_at, branch, environment, platform, os, cpu_count, total, passed, failed, skipped, total_ms.
        Filters: suite_id, test_runner, status (failed = runs with failures, passed = runs without), plus repository, project, branch, environment, days/from/to.
        Use get_test_run with a test_run_id to see a run's cases.
        """)]
    public Task<CallToolResult> ListTestRuns(
        [Description("Only this suite (from list_test_suites).")] string? suite_id = null,
        [Description("Only this test runner, for example NUnit (list_scopes kind=test_runner).")] string? test_runner = null,
        [Description("failed = runs with at least one failed test, passed = runs with none.")] string? status = null,
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT)] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.LIST_TEST_RUNS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id).With("test_runner", test_runner).With("status", status),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.GET_TEST_RUN, Title = "Get test run", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        One test run: its cases as rows, and the run header (suite, time, branch, environment, platform, counts, total_ms) in the summary line.
        The time window does not apply; test_run_id is exact. An unknown id is an error.
        Columns: test_key, class_name, name, status, duration_ms, error.
        Default order: failed first, then duration descending. sort: duration_ms | status | name (add 'asc' for ascending).
        Filters: status (passed | failed | skipped | pending | unknown), name_contains.
        """)]
    public Task<CallToolResult> GetTestRun(
        [Description("Test run id from list_test_runs or get_test.")] string test_run_id,
        [Description("Only cases with this status: passed | failed | skipped | pending | unknown.")] string? status = null,
        [Description("Case-insensitive substring of the test name.")] string? name_contains = null,
        [Description(ToolArgs.SORT + " Columns: duration_ms, status, name. Default: failed first, then duration_ms desc.")] string? sort = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.GET_TEST_RUN,
            new InsightsArgs().With("test_run_id", test_run_id).With("status", status)
                .With("name_contains", name_contains).With("sort", sort),
            limit, cursor, format, cancellationToken);
}
