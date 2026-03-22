using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

public class HealthCheckController : ControllerBase
{
    [Route("api/health")]
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "index")]
    public IActionResult Index()
    {
        return Ok();
    }

    [Route("/version")]
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [SwaggerOperation(OperationId = "GetVersion")]
    public IActionResult GetVersion() => Ok(typeof(HealthCheckController).Assembly.GetName().Version);
}
