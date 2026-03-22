using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Data;

public interface ITelemetryRepository
{
    Task<bool> BuildMetricExistsAsync(string id);
    Task AddBuildMetricAsync(BuildMetric metric);
    Task<bool> TestRunExistsAsync(string id);
    Task AddTestRunAsync(TestRun run, IEnumerable<TestCase> testCases);
    Task AddRawPayloadAsync(RawPayload payload);

    Task<PaginatedResult<TestRunListItemViewModel>> GetTestRunsAsync(FilterParams filters);
    Task<TestRunSummaryViewModel> GetTestRunSummaryAsync(FilterParams filters);
    Task<TestRunDetailViewModel?> GetTestRunDetailAsync(string id);
    Task<PaginatedResult<BuildMetricListItemViewModel>> GetBuildMetricsAsync(FilterParams filters);
    Task<ApiBuildSummaryViewModel> GetApiBuildSummaryAsync(FilterParams filters);
    Task<ClientsideBuildSummaryViewModel> GetClientsideBuildSummaryAsync(FilterParams filters);

    Task<FilterOptionsViewModel> GetAvailableFiltersAsync();

    Task DeleteOldDataAsync(DateTime cutoff);
    Task RunMaintenanceAsync();
}
