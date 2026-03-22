using Agoda.DevExTelemetry.Core.Models.Dashboard;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IDashboardService
{
    Task<TestRunSummaryViewModel> GetTestRunSummaryAsync(FilterParams filters);
    Task<PaginatedResult<TestRunListItemViewModel>> GetTestRunsAsync(FilterParams filters);
    Task<TestRunDetailViewModel?> GetTestRunDetailAsync(string id);
    Task<ApiBuildSummaryViewModel> GetApiBuildSummaryAsync(FilterParams filters);
    Task<ClientsideBuildSummaryViewModel> GetClientsideBuildSummaryAsync(FilterParams filters);
    Task<PaginatedResult<BuildMetricListItemViewModel>> GetBuildMetricsAsync(FilterParams filters);
}
