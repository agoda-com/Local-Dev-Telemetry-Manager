namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record TestCaseViewModel(
    string Name,
    string? FullName,
    string? ClassName,
    string Status,
    double? DurationMs,
    string? ErrorMessage);
