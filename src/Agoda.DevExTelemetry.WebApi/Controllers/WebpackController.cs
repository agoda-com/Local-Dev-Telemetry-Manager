using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class WebpackController : ControllerBase
{
    private readonly IBackgroundTaskQueue<IngestBuildMetricWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IBuildCategoryClassifier _classifier;

    public WebpackController(
        IBackgroundTaskQueue<IngestBuildMetricWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IBuildCategoryClassifier classifier)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _classifier = classifier;
    }

    [HttpPost("webpack")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] WebpackPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, null);

        string? devFeedbackType = null;
        if (payload.DevFeedback?.Any(df =>
                string.Equals(df.Type, "hmr", StringComparison.OrdinalIgnoreCase)) == true)
        {
            devFeedbackType = "hmr";
        }

        var (buildCategory, reloadType) = _classifier.Classify("webpack", devFeedbackType);

        if (!double.TryParse(payload.TimeTaken, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var timeTakenMs) && !string.IsNullOrWhiteSpace(payload.TimeTaken))
            return BadRequest(new { error = "Invalid timeTaken value" });
        if (!double.IsFinite(timeTakenMs) || timeTakenMs < 0)
            return BadRequest(new { error = "Invalid timeTaken value" });

        var extraData = new
        {
            payload.DevFeedback,
            payload.Timestamp,
            payload.BuiltAt,
            payload.TotalMemory,
            payload.CpuModels,
            payload.CpuSpeed,
            payload.NodeVersion,
            payload.V8Version
        };

        var metric = new BuildMetric
        {
            Id = payload.Id ?? Guid.NewGuid().ToString(),
            UserName = payload.UserName ?? string.Empty,
            CpuCount = payload.CpuCount,
            Hostname = payload.Hostname ?? string.Empty,
            Platform = platformStr,
            Os = payload.Os ?? string.Empty,
            Branch = payload.Branch ?? string.Empty,
            ProjectName = payload.ProjectName ?? string.Empty,
            Repository = payload.Repository ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            TimeTakenMs = timeTakenMs,
            MetricType = "webpack",
            BuildCategory = buildCategory,
            ReloadType = reloadType,
            ToolVersion = payload.NodeVersion,
            CommitSha = payload.CommitSha,
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            SourceEndpoint = "/webpack",
            ExtraData = JsonSerializer.Serialize(extraData)
        };

        await _queue.QueueBackgroundWorkItemAsync(_ => new IngestBuildMetricWorkItem
        {
            BuildMetric = metric,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/webpack",
            RawPayloadContentType = "application/json"
        });

        return Ok();
    }
}
