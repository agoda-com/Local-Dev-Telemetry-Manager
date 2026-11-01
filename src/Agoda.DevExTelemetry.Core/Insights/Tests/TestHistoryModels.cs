namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>Which executions to read. Every property is optional; null means "do not filter on it".</summary>
/// <param name="Suite">Only this suite.</param>
/// <param name="NameKey">Exactly this test (needs <paramref name="Suite"/>).</param>
/// <param name="NameContains">Case-insensitive substring of the name key.</param>
/// <param name="ClassContains">Case-insensitive substring of the class name.</param>
/// <param name="ErrorContains">Case-insensitive substring of the error message.</param>
/// <param name="Status">Normalised status: Passed, Failed, Skipped, Pending or Unknown.</param>
public sealed record TestExecutionFilter(
    TestSuiteIdentity? Suite = null,
    string? NameKey = null,
    string? NameContains = null,
    string? ClassContains = null,
    string? ErrorContains = null,
    string? Status = null);

/// <summary>One execution of one test, with its run's context. Never carries user or host names.</summary>
public sealed record TestExecutionRow(
    int CaseId,
    DateTime ReceivedAt,
    string TestRunId,
    TestSuiteIdentity Suite,
    string NameKey,
    string? ClassName,
    string Name,
    string Status,
    double? DurationMs,
    string Branch,
    string Environment,
    string Platform,
    string Os,
    int CpuCount,
    double? RunTotalMs,
    string? ErrorFirstLine)
{
    public string TestKey => Insights.TestKey.Format(Suite.SuiteId, NameKey);
}

/// <summary>A keyset page: the rows plus the COUNT of the whole filtered set.</summary>
public sealed record KeysetPage<T>(IReadOnlyList<T> Rows, int TotalRows);

/// <summary>A test found by name, with how often it ran in the window.</summary>
public sealed record TestCandidate(
    TestSuiteIdentity Suite,
    string NameKey,
    string? ClassName,
    string Name,
    int Runs,
    DateTime LastRunAt)
{
    public string TestKey => Insights.TestKey.Format(Suite.SuiteId, NameKey);
}

/// <summary>Which runs to list.</summary>
/// <param name="Suite">Only this suite.</param>
/// <param name="TestRunner">Only this test runner (any repository and project).</param>
/// <param name="Status"><c>failed</c> = FailedTests &gt; 0, <c>passed</c> = FailedTests is 0 or unknown.</param>
public sealed record TestRunFilter(TestSuiteIdentity? Suite = null, string? TestRunner = null, string? Status = null);

/// <summary>One test run header. No user or host names.</summary>
public sealed record TestRunRow(
    string Id,
    DateTime ReceivedAt,
    TestSuiteIdentity Suite,
    string Branch,
    string Environment,
    string Platform,
    string Os,
    int CpuCount,
    int? TotalTests,
    int? PassedTests,
    int? FailedTests,
    int? SkippedTests,
    double? TotalDurationMs);

/// <summary>Sortable columns of the cases of one run.</summary>
public enum TestRunCaseSort
{
    /// <summary>The dashboard order: failed first, then duration descending.</summary>
    Default,
    DurationMs,
    Status,
    Name
}

/// <summary>One case of one run.</summary>
public sealed record TestRunCaseRow(
    int CaseId,
    TestSuiteIdentity Suite,
    string NameKey,
    string? ClassName,
    string Name,
    string Status,
    double? DurationMs,
    string? ErrorFirstLine)
{
    public string TestKey => Insights.TestKey.Format(Suite.SuiteId, NameKey);
}

/// <summary>A distinct error first-line of one test, with how often it happened.</summary>
public sealed record TestErrorSummary(
    TestSuiteIdentity Suite,
    string NameKey,
    string Error,
    int Count,
    DateTime LastSeenAt);

/// <summary>
/// The current failing streak of one test: the failures since its last pass.
/// Skipped, pending and unknown executions do not break a streak.
/// </summary>
/// <param name="FirstFailureAt">ReceivedAt of the first failure after the last pass (or the first ever failure).</param>
/// <param name="LastPassAt">ReceivedAt of the last pass before the streak, or null when it never passed in scope.</param>
/// <param name="LatestFailureAt">ReceivedAt of the newest failure.</param>
/// <param name="LatestError">First line of the newest failure's error.</param>
/// <param name="FailedExecutions">Failures in the streak.</param>
/// <param name="Branches">Distinct branches with a failure in the streak, sorted.</param>
public sealed record TestFailureStreak(
    TestSuiteIdentity Suite,
    string NameKey,
    DateTime FirstFailureAt,
    DateTime? LastPassAt,
    DateTime LatestFailureAt,
    string? LatestError,
    int FailedExecutions,
    IReadOnlyList<string> Branches);
