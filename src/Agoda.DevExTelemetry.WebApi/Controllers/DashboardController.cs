using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[Route("api")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("test-runs/summary")]
    public async Task<ActionResult<TestRunSummaryViewModel>> GetTestRunSummary(
        [FromQuery] FilterParams filters)
    {
        var result = await _dashboardService.GetTestRunSummaryAsync(filters);
        return Ok(result);
    }

    [HttpGet("test-runs/{id}")]
    public async Task<ActionResult<TestRunDetailViewModel>> GetTestRunDetail(string id)
    {
        var result = await _dashboardService.GetTestRunDetailAsync(id);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpGet("test-runs")]
    public async Task<ActionResult<PaginatedResult<TestRunListItemViewModel>>> GetTestRuns(
        [FromQuery] FilterParams filters)
    {
        var result = await _dashboardService.GetTestRunsAsync(filters);
        return Ok(result);
    }

    [HttpGet("build-metrics")]
    public async Task<ActionResult<PaginatedResult<BuildMetricListItemViewModel>>> GetBuildMetrics(
        [FromQuery] FilterParams filters)
    {
        var result = await _dashboardService.GetBuildMetricsAsync(filters);
        return Ok(result);
    }

    [HttpGet("build-metrics/api-summary")]
    public async Task<ActionResult<ApiBuildSummaryViewModel>> GetApiBuildSummary(
        [FromQuery] FilterParams filters)
    {
        var result = await _dashboardService.GetApiBuildSummaryAsync(filters);
        return Ok(result);
    }

    [HttpGet("build-metrics/clientside-summary")]
    public async Task<ActionResult<ClientsideBuildSummaryViewModel>> GetClientsideSummary(
        [FromQuery] FilterParams filters)
    {
        var result = await _dashboardService.GetClientsideBuildSummaryAsync(filters);
        return Ok(result);
    }
}
