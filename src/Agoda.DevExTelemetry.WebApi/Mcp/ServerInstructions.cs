namespace Agoda.DevExTelemetry.WebApi.Mcp;

/// <summary>The MCP <c>instructions</c> sent at initialize: the data model and its caveats.</summary>
public static class ServerInstructions
{
    public const string TEXT = """
        DX Telemetry: build, startup, hot-reload and test-run timings from developer machines and CI. Read-only.

        Two layers of tools:
        - Analysis tools (get_overview, find_slow_tests, find_flaky_tests, find_failing_tests, find_slow_builds,
          explain_build_time, find_regressions) apply default thresholds and return ranked rows, each with a
          'reason' and an 'evidence' column naming the data tool call that backs it up. Every threshold is a parameter.
        - Data tools (list_*, get_*) filter, group, sort and page without judging. Use them to check an analysis
          result or to answer questions the analysis tools don't cover.
        Start with get_overview or list_scopes.

        Concepts:
        - Test suite: repository + project + test runner. suite_id is a short stable hash of the three.
        - Test key: '{suite_id}:{name}', where name is FullName, or ClassName + '.' + Name when FullName is empty.
          Runners fill these differently: Vitest puts the file in ClassName, NUnit and JUnit put the class there.
        - Run: one test-run upload (a suite executed once). Test runs don't record a commit.
        - Build series: repository + project + metric type (+ reload type for client-side builds), for example
          the .Net compile, .AspNetStartup, vitehmr or GradleTalaiot. series_id is a short stable hash.

        Output: CSV by default (format=json for JSON). Content[0] is a one-line summary, content[1] the rows;
        the structured content has total_rows, returned, truncated, next_cursor, export_url and window.
        Durations are whole milliseconds, timestamps ISO-8601 UTC. At most 500 rows per call: follow next_cursor,
        or download export_url (GET, CSV, same arguments, no row cap) with curl and analyse it with duckdb or pandas.
        Error messages are cut to their first line, 300 characters.

        Caveats:
        - environment (local | ci) is inferred from the debugger flag, hostname patterns and run ids, so it can be wrong.
        - Data is kept for the retention period (90 days by default); 'days' is capped at it. Default window: 14 days.
        - Locally, fail-then-pass is usually TDD, not flakiness. Use environment=ci to judge flakiness.
        - No per-person data: user names and hostnames are never returned.
        - The server never sees source code. Open the test or build config in the workspace to explain a finding.
        """;
}
