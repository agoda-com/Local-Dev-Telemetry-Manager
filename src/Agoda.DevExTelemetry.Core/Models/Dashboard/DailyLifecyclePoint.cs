namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record DailyLifecyclePoint(
    string Date,
    double AvgCompileMs,
    double AvgStartupMs,
    double AvgFirstResponseMs);
