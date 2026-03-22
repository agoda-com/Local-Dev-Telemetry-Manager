using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class FilterService : IFilterService
{
    private readonly ITelemetryRepository _repository;

    public FilterService(ITelemetryRepository repository)
    {
        _repository = repository;
    }

    public Task<FilterOptionsViewModel> GetAvailableFiltersAsync() =>
        _repository.GetAvailableFiltersAsync();
}
