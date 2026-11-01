namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>
/// One queryable dataset, shared by an MCP tool and its <c>/api/export/{name}.csv</c> endpoint, so both
/// return the same rows for the same arguments. Implementations parse their arguments from
/// <see cref="InsightsArgs"/> (throwing <see cref="InsightsArgumentException"/> on bad input), run Core
/// queries, and return one page.
/// <para>
/// Register each implementation with <c>[RegisterPerRequest(For = typeof(IInsightsDataset))]</c>.
/// Keep them thin: the queries live in the track's query service so the analysis layer reuses them.
/// </para>
/// </summary>
public interface IInsightsDataset
{
    /// <summary>The tool name and export name, from <see cref="DatasetNames"/>.</summary>
    string Name { get; }

    /// <summary>
    /// One page of rows. <c>limit</c>, <c>cursor</c> and <c>format</c> are handled by the caller and
    /// arrive in <paramref name="page"/>, not in <paramref name="args"/>.
    /// </summary>
    Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>Tool and export names. Data tools first, then analysis tools.</summary>
public static class DatasetNames
{
    // Data tools: catalog
    public const string LIST_SCOPES = "list_scopes";
    public const string LIST_TEST_SUITES = "list_test_suites";
    public const string LIST_BUILD_SERIES = "list_build_series";

    // Data tools: tests
    public const string GET_TESTS = "get_tests";
    public const string GET_TEST = "get_test";
    public const string GET_TEST_FAILURES = "get_test_failures";
    public const string LIST_TEST_RUNS = "list_test_runs";
    public const string GET_TEST_RUN = "get_test_run";

    // Data tools: builds
    public const string GET_BUILDS = "get_builds";
    public const string GET_BUILD_STATS = "get_build_stats";
    public const string GET_BUILD = "get_build";

    // Analysis tools
    public const string GET_OVERVIEW = "get_overview";
    public const string FIND_SLOW_TESTS = "find_slow_tests";
    public const string FIND_FLAKY_TESTS = "find_flaky_tests";
    public const string FIND_FAILING_TESTS = "find_failing_tests";
    public const string FIND_SLOW_BUILDS = "find_slow_builds";
    public const string EXPLAIN_BUILD_TIME = "explain_build_time";
    public const string FIND_REGRESSIONS = "find_regressions";

    /// <summary>The arguments the runner owns; datasets never see them in <see cref="InsightsArgs"/>.</summary>
    public static readonly string[] PagingArgs = ["limit", "cursor", "format"];
}

/// <summary>Finds a dataset by name.</summary>
public interface IInsightsDatasetResolver
{
    IInsightsDataset? Find(string name);
    IReadOnlyList<string> Names { get; }
}
