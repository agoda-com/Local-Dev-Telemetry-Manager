namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record ClientsideBuildSummaryViewModel(
    double HotReloadPercent,
    double AvgHotReloadTimeMs,
    double AvgFullReloadTimeMs,
    int TotalReloadsToday,
    IReadOnlyList<DailyReloadCount> ReloadCountTrend,
    IReadOnlyList<DailyReloadTime> ReloadTimeTrend);
