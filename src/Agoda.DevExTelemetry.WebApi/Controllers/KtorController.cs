using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.WebApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class KtorController : ControllerBase
{
    private readonly IJvmBuildMetricIngestService _ingestService;

    public KtorController(IJvmBuildMetricIngestService ingestService)
    {
        _ingestService = ingestService;
    }

    [HttpPost("ktor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest([FromBody] KtorStartupMetricPayload payload)
    {
        var error = await _ingestService.QueueKtorAsync(payload);
        return error is null ? Ok() : BadRequest(new { error });
    }
}
