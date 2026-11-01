using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

/// <summary>
/// Investigation recipes (MCP prompts). Each starts from an analysis tool, then checks the results with the
/// data tools and the codebase. They are text, so a team can change how it investigates without a new tool.
/// </summary>
[McpServerPromptType]
public sealed class RecipePrompts
{
    private const string REPOSITORY = "Repository name to focus on (list_scopes kind=repository). Optional.";

    [McpServerPrompt(Name = "investigate_slow_tests", Title = "Investigate slow tests")]
    [Description("Find the tests that cost developers the most time, read them, and propose speed-ups.")]
    public static string InvestigateSlowTests(
        [Description(REPOSITORY)] string? repository = null,
        [Description("Limit to one suite (list_test_suites). Optional.")] string? suite_id = null) =>
        Recipe(
            "Investigate slow tests",
            Scope(repository, suite_id),
            """
            1. Call find_slow_tests (ranked by total time contributed). Then call it again with group_by=class: fixture and
               setup cost often sits at class or file level rather than in one test.
            2. For the top 5 rows, open the test source in this workspace. Look for sleeps and fixed waits, real I/O
               (network, disk, databases), heavy setup or teardown, and large parametrisation.
            3. For each, call get_test with its test_key to check whether it is slow everywhere or only on some OSes,
               CPU counts or branches. If it is only slow on some machines, say so: that is a machine problem, not a test problem.
            4. Report each finding with: the test_key, its share of suite time, the evidence (tool calls and the rows
               that matter), what in the code makes it slow, and a concrete fix with the expected saving.
            """);

    [McpServerPrompt(Name = "investigate_flaky_tests", Title = "Investigate flaky tests")]
    [Description("Find tests that pass and fail without a code change, group their failures, and propose fixes.")]
    public static string InvestigateFlakyTests(
        [Description(REPOSITORY)] string? repository = null,
        [Description("Limit to one suite (list_test_suites). Optional.")] string? suite_id = null) =>
        Recipe(
            "Investigate flaky tests",
            Scope(repository, suite_id),
            """
            1. Call find_flaky_tests (CI data by default: locally, fail-then-pass is usually TDD). Also call
               find_failing_tests so you don't mistake a broken test for a flaky one.
            2. For each flaky candidate, call get_test with its test_key and check whether the failures cluster on one
               branch, OS, platform or CPU count. Read its status_sequence: a test that flickers (PFPPFP) is flaky; one
               that broke and stayed broken (PPPPFFFF) is not.
            3. Call get_test_failures for the suite and group the error messages. Many tests failing with the same error
               (connection refused, timeout, port in use) point at a shared root cause, not at each test.
            4. Open each test in this workspace and look for timing assumptions, ordering dependence, shared or static
               state, real network or clock use, and parallelism.
            5. Report each finding with the evidence (tool calls and rows), the likely cause and a fix.
            """);

    [McpServerPrompt(Name = "investigate_build_regressions", Title = "Investigate build regressions")]
    [Description("Find builds, startups and reloads that got slower, explain why, and propose fixes.")]
    public static string InvestigateBuildRegressions(
        [Description(REPOSITORY)] string? repository = null) =>
        Recipe(
            "Investigate build regressions",
            Scope(repository, null),
            """
            1. Call find_regressions with kind=build. For each flagged series, call explain_build_time with its series_id
               to see which dimension (CPU count, OS, tool version, branch, environment, debugger) explains the spread.
            2. Confirm with get_build_stats series_id=<id> group_by=day,tool_version: does the step line up with a tool
               version change, a branch, or a set of machines?
            3. Use get_builds and get_build on a few slow and a few fast builds to compare what was built: Gradle
               RequestedTasks and CacheRatio, Vite or Webpack extra fields.
            4. Check the build configuration in this workspace (project files, package versions, build scripts) for a
               change that matches the date in shift_started_on.
            5. Report each regression with before and after p50, the start date, the explaining dimension, the evidence
               (tool calls and rows) and a fix.
            """);

    [McpServerPrompt(Name = "triage_dev_loop", Title = "Triage the dev loop")]
    [Description("A full health check of one repository's dev loop: tests, builds, startups and reloads.")]
    public static string TriageDevLoop(
        [Description("Repository name (list_scopes kind=repository).")] string repository) =>
        Recipe(
            "Triage the dev loop",
            Scope(repository, null),
            """
            1. Call get_overview for the repository. It returns the top rows of every analysis tool.
            2. Take the worst findings and follow the matching recipe for each: investigate_slow_tests,
               investigate_flaky_tests and investigate_build_regressions (use find_slow_builds and explain_build_time
               for slow but not regressed builds).
            3. Write a ranked report. Rank by developer time lost per week where you can estimate it (runs × time).
               For each finding cite the evidence (the tool calls and the rows that matter), what you found in the code,
               and a suggested fix. Say what you could not confirm.
            """);

    private static string Scope(string? repository, string? suiteId)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(repository)) parts.Add($"repository={repository}");
        if (!string.IsNullOrWhiteSpace(suiteId)) parts.Add($"suite_id={suiteId}");
        return parts.Count == 0
            ? "all repositories (call list_scopes first if you need to narrow down)"
            : string.Join(" ", parts);
    }

    private static string Recipe(string title, string scope, string steps)
    {
        var sb = new StringBuilder();
        sb.Append(title).Append(" using the dx-telemetry MCP tools. Scope: ").Append(scope)
            .Append(". Pass the scope arguments to every tool call.\n\n");
        sb.Append(steps.TrimEnd()).Append('\n');
        sb.Append("\nEvery analysis row has an 'evidence' column: run it to see the rows behind the 'reason'. ");
        sb.Append("Results are CSV and capped at 500 rows: follow next_cursor, or curl export_url to a file and use duckdb or pandas for bigger questions.");
        return sb.ToString();
    }
}
