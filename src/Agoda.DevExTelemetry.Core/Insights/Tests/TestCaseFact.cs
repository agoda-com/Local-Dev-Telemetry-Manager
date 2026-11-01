using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>
/// One test execution with its run's context, flattened so EF can filter, group and sort on it in SQL
/// on both SQLite and PostgreSQL. Build queries on <see cref="TestCaseFactQueries.Facts"/>.
/// Never add UserName or Hostname here (decision D6).
/// </summary>
public sealed class TestCaseFact
{
    public int CaseId { get; init; }
    public string TestRunId { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
    public string Repository { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string TestRunner { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string Os { get; init; } = string.Empty;
    public int CpuCount { get; init; }
    public string? ClassName { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary><c>FullName</c>, else <c>ClassName.Name</c>, else <c>Name</c>; the same as <see cref="TestKey.NameKey"/>.</summary>
    public string NameKey { get; init; } = string.Empty;

    /// <summary>Normalised: Passed, Failed, Skipped, Pending or Unknown.</summary>
    public string Status { get; init; } = string.Empty;
    public double? DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public double? RunTotalMs { get; init; }
}

public static class TestCaseFactQueries
{
    /// <summary>
    /// Projects test cases to <see cref="TestCaseFact"/> rows in SQL. Compose filters and GroupBy on the
    /// result; EF translates them. Apply the scope first: <c>scope.Apply(db.TestCases).Facts()</c>.
    /// </summary>
    public static IQueryable<TestCaseFact> Facts(this IQueryable<TestCase> cases) =>
        cases.Select(c => new TestCaseFact
        {
            CaseId = c.Id,
            TestRunId = c.TestRunId,
            ReceivedAt = c.TestRun.ReceivedAt,
            Repository = c.TestRun.RepositoryName,
            Project = c.TestRun.ProjectName,
            TestRunner = c.TestRun.TestRunner,
            Branch = c.TestRun.Branch,
            Environment = c.TestRun.ExecutionEnvironment,
            Platform = c.TestRun.Platform,
            Os = c.TestRun.Os,
            CpuCount = c.TestRun.CpuCount,
            ClassName = c.ClassName,
            Name = c.Name,
            NameKey = c.FullName != null && c.FullName != ""
                ? c.FullName
                : c.ClassName != null && c.ClassName != ""
                    ? c.ClassName + "." + c.Name
                    : c.Name,
            Status = c.Status,
            DurationMs = c.DurationMs,
            ErrorMessage = c.ErrorMessage,
            RunTotalMs = c.TestRun.TotalDurationMs
        });

    /// <summary>Only the facts of one suite.</summary>
    public static IQueryable<TestCaseFact> InSuite(this IQueryable<TestCaseFact> facts, TestSuiteIdentity suite) =>
        facts.Where(f => f.Repository == suite.Repository && f.Project == suite.Project && f.TestRunner == suite.TestRunner);

    /// <summary>Only the facts of one test.</summary>
    public static IQueryable<TestCaseFact> OfTest(this IQueryable<TestCaseFact> facts, TestSuiteIdentity suite, string nameKey) =>
        facts.InSuite(suite).Where(f => f.NameKey == nameKey);
}
