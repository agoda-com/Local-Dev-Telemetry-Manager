namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record BuildMetricListItemViewModel(
    string Id,
    DateTime ReceivedAt,
    string ProjectName,
    string MetricType,
    string BuildCategory,
    string? ReloadType,
    double TimeTakenMs,
    string? ToolVersion,
    string ExecutionEnvironment);
