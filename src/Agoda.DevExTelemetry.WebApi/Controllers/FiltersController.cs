using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[Route("api/filters")]
[ApiController]
public class FiltersController : ControllerBase
{
    private readonly IFilterService _filterService;

    public FiltersController(IFilterService filterService)
    {
        _filterService = filterService;
    }

    [HttpGet]
    public async Task<ActionResult<FilterOptionsViewModel>> GetFilters()
    {
        var result = await _filterService.GetAvailableFiltersAsync();
        return Ok(result);
    }
}
