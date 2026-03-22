using Agoda.DevExTelemetry.Core.Models.Dashboard;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IFilterService
{
    Task<FilterOptionsViewModel> GetAvailableFiltersAsync();
}
