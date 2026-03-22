namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record ApiBuildSummaryViewModel(
    double AvgCompileTimeMs,
    double AvgStartupTimeMs,
    double AvgFirstResponseTimeMs,
    IReadOnlyList<DailyDataPoint> CompileTrend,
    IReadOnlyList<DailyDataPoint> StartupTrend,
    IReadOnlyList<DailyDataPoint> FirstResponseTrend,
    IReadOnlyList<DailyLifecyclePoint> DailyLifecycle);
