namespace Agoda.DevExTelemetry.Core.Models.Dashboard;

public record FilterOptionsViewModel(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Repositories,
    IReadOnlyList<string> Branches,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> TestRunners,
    IReadOnlyList<string> MetricTypes);
