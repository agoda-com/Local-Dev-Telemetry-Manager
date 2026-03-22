namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record TestRunSummaryViewModel(
    int TotalRuns,
    double AvgDurationMs,
    double PassRate,
    IReadOnlyList<DailyDataPoint> DurationTrend,
    IReadOnlyList<DailyPassFail> PassFailTrend);
