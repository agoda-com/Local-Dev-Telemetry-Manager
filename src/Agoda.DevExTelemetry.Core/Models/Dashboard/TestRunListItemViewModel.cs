namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record TestRunListItemViewModel(
    string Id,
    DateTime ReceivedAt,
    string ProjectName,
    string TestRunner,
    int? TotalTests,
    int? PassedTests,
    int? FailedTests,
    double? TotalDurationMs,
    string ExecutionEnvironment);
