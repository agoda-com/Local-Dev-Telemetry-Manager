using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class DashboardService : IDashboardService
{
    private readonly ITelemetryRepository _repository;

    public DashboardService(ITelemetryRepository repository)
    {
        _repository = repository;
    }

    public Task<TestRunSummaryViewModel> GetTestRunSummaryAsync(FilterParams filters) =>
        _repository.GetTestRunSummaryAsync(filters);

    public Task<PaginatedResult<TestRunListItemViewModel>> GetTestRunsAsync(FilterParams filters) =>
        _repository.GetTestRunsAsync(filters);

    public Task<TestRunDetailViewModel?> GetTestRunDetailAsync(string id) =>
        _repository.GetTestRunDetailAsync(id);

    public Task<ApiBuildSummaryViewModel> GetApiBuildSummaryAsync(FilterParams filters) =>
        _repository.GetApiBuildSummaryAsync(filters);

    public Task<ClientsideBuildSummaryViewModel> GetClientsideBuildSummaryAsync(FilterParams filters) =>
        _repository.GetClientsideBuildSummaryAsync(filters);

    public Task<PaginatedResult<BuildMetricListItemViewModel>> GetBuildMetricsAsync(FilterParams filters) =>
        _repository.GetBuildMetricsAsync(filters);
}
