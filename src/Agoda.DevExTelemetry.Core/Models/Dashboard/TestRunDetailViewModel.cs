namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record TestRunDetailViewModel(
    string Id,
    string RunId,
    DateTime ReceivedAt,
    string UserName,
    string Hostname,
    string Branch,
    string ProjectName,
    string TestRunner,
    string ExecutionEnvironment,
    int? TotalTests,
    int? PassedTests,
    int? FailedTests,
    int? SkippedTests,
    double? TotalDurationMs,
    IReadOnlyList<TestCaseViewModel> TestCases);
