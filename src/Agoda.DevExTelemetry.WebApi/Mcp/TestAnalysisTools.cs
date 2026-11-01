using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class TestAnalysisTools
{
    private readonly IInsightsToolRunner _runner;

    public TestAnalysisTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.FIND_SLOW_TESTS, Title = "Find slow tests", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: where test time goes. Tests (or classes) ranked by sort_by = total (default; total_ms, what costs developers the most) | p95 | avg.
        Excludes tests with fewer than min_runs executions (default 3). group_by=class rolls up to class or file, where shared fixture and setup cost sits.
        Columns: those of get_tests (test_key, suite_id, repository, project, test_runner, class_name, name, runs, passed, failed, skipped, fail_rate, avg_ms, p50_ms, p95_ms, max_ms, total_ms,
        first_seen_at, last_run_at, last_status, last_failed_at, status_sequence), then share_of_suite_time (the row's total_ms as a PERCENT, 0-100, of the total_ms of all tests in the same suite in the window), reason, evidence.
        evidence is a get_test call (get_tests group_by=class for class rows) that shows the rows behind it.
        Filters: repository, project, branch, environment (default all), days/from/to, suite_id, test_runner, name_contains, class_contains.
        """)]
    public Task<CallToolResult> FindSlowTests(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT + " Default all.")] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this suite, from list_test_suites.")] string? suite_id = null,
        [Description("Test runner, for example NUnit or Vitest.")] string? test_runner = null,
        [Description("Only tests whose name or full name contains this text (case-insensitive).")] string? name_contains = null,
        [Description("Only tests whose class contains this text (case-insensitive).")] string? class_contains = null,
        [Description("total (default) | p95 | avg.")] string? sort_by = null,
        [Description("test (default) or class.")] string? group_by = null,
        [Description("Minimum executions in the window. Default 3.")] int? min_runs = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.FIND_SLOW_TESTS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id)
                .With("test_runner", test_runner)
                .With("name_contains", name_contains)
                .With("class_contains", class_contains)
                .With("sort_by", sort_by)
                .With("group_by", group_by)
                .With("min_runs", min_runs),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.FIND_FLAKY_TESTS, Title = "Find flaky tests", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: tests that both passed and failed in the window, ranked by flip rate. Defaults to environment=ci, because locally fail-then-pass is usually TDD (with local or all, the summary says so).
        Only Passed and Failed executions count; skipped, pending and unknown are ignored. A flip is a status change between consecutive executions on the same branch, ordered by time;
        flip_rate = flips / (runs - number of branches), which is flips / (runs - 1) on one branch. A test that keeps flickering outranks one that broke for a while and was then fixed.
        Excludes tests with fewer than min_runs Passed+Failed runs (default 5) and with flip_rate below min_flip_rate (default 0, so any flip counts). Always-failing tests are not listed; see find_failing_tests.
        Columns: test_key, suite_id, repository, project, test_runner, class_name, name, runs (passed + failed), failed, fail_rate, flips, flip_rate, last_failed_at, status_sequence (last 30 results),
        top_errors (up to 3 distinct error first-lines with counts, like "connection refused (4); timeout (2)"), reason, evidence (a get_test call).
        Ranked by flip_rate, then fail_rate. Filters: repository, project, branch, environment (default ci), days/from/to, suite_id, test_runner, name_contains, class_contains.
        """)]
    public Task<CallToolResult> FindFlakyTests(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT + " Default ci.")] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this suite, from list_test_suites.")] string? suite_id = null,
        [Description("Test runner, for example NUnit or Vitest.")] string? test_runner = null,
        [Description("Only tests whose name or full name contains this text (case-insensitive).")] string? name_contains = null,
        [Description("Only tests whose class contains this text (case-insensitive).")] string? class_contains = null,
        [Description("Minimum Passed+Failed executions. Default 5.")] int? min_runs = null,
        [Description("Minimum flip rate, 0 to 1. Default 0 (any flip).")] double? min_flip_rate = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.FIND_FLAKY_TESTS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id)
                .With("test_runner", test_runner)
                .With("name_contains", name_contains)
                .With("class_contains", class_contains)
                .With("min_runs", min_runs)
                .With("min_flip_rate", min_flip_rate),
            limit, cursor, format, cancellationToken);

    [McpServerTool(Name = DatasetNames.FIND_FAILING_TESTS, Title = "Find failing tests", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Analysis: tests that are broken rather than flaky. Listed when fail_rate >= min_fail_rate (default 0.8) with at least min_runs Passed+Failed runs (default 3),
        or when the most recent recent_runs (default 3) Passed/Failed executions all failed (read from the last 30 results). Skipped, pending and unknown are ignored.
        Tests failing right now (latest result is a failure) come first, ordered by failing_since, oldest first; the rest follow by fail_rate.
        Columns: test_key, suite_id, repository, project, test_runner, class_name, name, runs (passed + failed), failed, fail_rate,
        failing_since (first failure of the current streak, empty when the latest result passed), last_passed_at, latest_error, branches (semicolon-separated, of the current streak),
        status_sequence (last 30 results), reason, evidence (a get_test_failures call).
        Filters: repository, project, branch, environment (default all), days/from/to, suite_id, test_runner, name_contains, class_contains.
        """)]
    public Task<CallToolResult> FindFailingTests(
        [Description(ToolArgs.REPOSITORY)] string? repository = null,
        [Description(ToolArgs.PROJECT)] string? project = null,
        [Description(ToolArgs.BRANCH)] string? branch = null,
        [Description(ToolArgs.ENVIRONMENT + " Default all.")] string? environment = null,
        [Description(ToolArgs.DAYS)] int? days = null,
        [Description(ToolArgs.FROM)] string? from = null,
        [Description(ToolArgs.TO)] string? to = null,
        [Description("Only this suite, from list_test_suites.")] string? suite_id = null,
        [Description("Test runner, for example NUnit or Vitest.")] string? test_runner = null,
        [Description("Only tests whose name or full name contains this text (case-insensitive).")] string? name_contains = null,
        [Description("Only tests whose class contains this text (case-insensitive).")] string? class_contains = null,
        [Description("Minimum fail rate, 0 to 1. Default 0.8.")] double? min_fail_rate = null,
        [Description("Minimum Passed+Failed executions for the fail-rate rule. Default 3.")] int? min_runs = null,
        [Description("List tests whose latest this-many results all failed. Default 3.")] int? recent_runs = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.FIND_FAILING_TESTS,
            ToolArgs.Scope(repository, project, branch, environment, days, from, to)
                .With("suite_id", suite_id)
                .With("test_runner", test_runner)
                .With("name_contains", name_contains)
                .With("class_contains", class_contains)
                .With("min_fail_rate", min_fail_rate)
                .With("min_runs", min_runs)
                .With("recent_runs", recent_runs),
            limit, cursor, format, cancellationToken);
}
