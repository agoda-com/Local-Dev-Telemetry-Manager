using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.McpTests.Infrastructure;

/// <summary>
/// Builders for rows with chosen timestamps, statuses and durations. Every row carries the same
/// <see cref="USER_NAME"/> and <see cref="HOSTNAME"/>, so privacy tests can check they never come back.
/// </summary>
public static class Seed
{
    public const string USER_NAME = "seeded-user-name";
    public const string HOSTNAME = "seeded-host-name";

    /// <summary>
    /// A test run at <paramref name="at"/>. Totals are computed from <paramref name="cases"/>; the run's
    /// duration is the sum of the case durations unless <paramref name="totalMs"/> is given.
    /// </summary>
    public static TestRun Run(
        DateTime at,
        IEnumerable<TestCase> cases,
        string repository = "web-app",
        string project = "Web.Tests",
        string runner = "NUnit",
        string environment = "CI",
        string branch = "main",
        string platform = "Linux",
        string os = "Ubuntu 22.04",
        int cpuCount = 8,
        double? totalMs = null,
        string? id = null)
    {
        var runId = id ?? Guid.NewGuid().ToString("N");
        var caseList = cases.ToList();
        foreach (var c in caseList) c.TestRunId = runId;

        return new TestRun
        {
            Id = runId,
            RunId = runId,
            ReceivedAt = DateTime.SpecifyKind(at, DateTimeKind.Utc),
            UserName = USER_NAME,
            Hostname = HOSTNAME,
            CpuCount = cpuCount,
            Platform = platform,
            Os = os,
            Branch = branch,
            ProjectName = project,
            Repository = $"https://github.com/example/{repository}",
            RepositoryName = repository,
            TestRunner = runner,
            IsDebuggerAttached = false,
            ExecutionEnvironment = environment,
            TotalTests = caseList.Count,
            PassedTests = caseList.Count(c => c.Status == "Passed"),
            FailedTests = caseList.Count(c => c.Status == "Failed"),
            SkippedTests = caseList.Count(c => c.Status == "Skipped"),
            TotalDurationMs = totalMs ?? caseList.Sum(c => c.DurationMs ?? 0),
            SourceEndpoint = "/api/test",
            TestCases = caseList
        };
    }

    /// <summary>
    /// A test case. <paramref name="status"/> is a normalised status: Passed, Failed, Skipped, Pending or Unknown.
    /// <paramref name="fullName"/> defaults to null, so the test key is <c>ClassName.Name</c>.
    /// </summary>
    public static TestCase Case(
        string name,
        string status = "Passed",
        double? durationMs = 100,
        string? className = "Web.Tests.LoginTests",
        string? fullName = null,
        string? error = null) =>
        new()
        {
            Name = name,
            FullName = fullName,
            ClassName = className,
            MethodName = name,
            Status = status,
            DurationMs = durationMs,
            ErrorMessage = error ?? (status == "Failed" ? $"{name} failed" : null)
        };

    public static BuildMetric Build(
        DateTime at,
        double timeMs,
        string metricType = ".Net",
        string repository = "web-app",
        string project = "Web.Api",
        string category = "API",
        string? reloadType = null,
        string environment = "Local",
        string branch = "main",
        string platform = "Linux",
        string os = "Ubuntu 22.04",
        int cpuCount = 8,
        string? toolVersion = "10.0.100",
        bool debuggerAttached = false,
        string? commitSha = null,
        string? extraData = null,
        string? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            ReceivedAt = DateTime.SpecifyKind(at, DateTimeKind.Utc),
            UserName = USER_NAME,
            Hostname = HOSTNAME,
            CpuCount = cpuCount,
            Platform = platform,
            Os = os,
            Branch = branch,
            ProjectName = project,
            Repository = $"https://github.com/example/{repository}",
            RepositoryName = repository,
            TimeTakenMs = timeMs,
            MetricType = metricType,
            BuildCategory = category,
            ReloadType = reloadType,
            ToolVersion = toolVersion,
            CommitSha = commitSha,
            IsDebuggerAttached = debuggerAttached,
            ExecutionEnvironment = environment,
            SourceEndpoint = "/dotnet",
            ExtraData = extraData
        };
}
