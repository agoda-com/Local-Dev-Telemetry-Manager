using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class GradleController : ControllerBase
{
    private readonly IBackgroundTaskQueue<IngestBuildMetricWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IBuildCategoryClassifier _classifier;

    public GradleController(
        IBackgroundTaskQueue<IngestBuildMetricWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IBuildCategoryClassifier classifier)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _classifier = classifier;
    }

    [HttpPost("gradletalaiot")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] GradleTalaiotPayload payload)
    {
        var platformStr = payload.Platform ?? string.Empty;
        var isDebuggerAttached = bool.TryParse(payload.IsDebuggerAttached, out var dbg) && dbg;
        var environment = _environmentDetector.Detect(
            payload.Hostname, isDebuggerAttached, platformStr, payload.BuildId);

        var (buildCategory, reloadType) = _classifier.Classify("GradleTalaiot", null);

        if (!double.TryParse(payload.DurationMs, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var timeTakenMs) && !string.IsNullOrWhiteSpace(payload.DurationMs))
            return BadRequest(new { error = "Invalid durationMs value" });
        if (!double.IsFinite(timeTakenMs) || timeTakenMs < 0)
            return BadRequest(new { error = "Invalid durationMs value" });
        int.TryParse(payload.CpuCount, out var cpuCount);

        var extraData = new
        {
            payload.ConfigurationDurationMs,
            payload.RequestedTasks,
            payload.GradleVersion,
            payload.RootProject,
            payload.Success,
            payload.BuildId,
            payload.CacheRatio
        };

        var metric = new BuildMetric
        {
            Id = payload.Id ?? Guid.NewGuid().ToString(),
            UserName = payload.UserName ?? string.Empty,
            CpuCount = cpuCount,
            Hostname = payload.Hostname ?? string.Empty,
            Platform = platformStr,
            Os = payload.Os ?? string.Empty,
            Branch = payload.Branch ?? string.Empty,
            ProjectName = payload.ProjectName ?? string.Empty,
            Repository = payload.Repository ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            TimeTakenMs = timeTakenMs,
            MetricType = "GradleTalaiot",
            BuildCategory = buildCategory,
            ReloadType = reloadType,
            ToolVersion = payload.GradleVersion,
            IsDebuggerAttached = isDebuggerAttached,
            ExecutionEnvironment = environment,
            SourceEndpoint = "/gradletalaiot",
            ExtraData = JsonSerializer.Serialize(extraData)
        };

        await _queue.QueueBackgroundWorkItemAsync(_ => new IngestBuildMetricWorkItem
        {
            BuildMetric = metric,
            RawPayloadEndpoint = "/gradletalaiot",
            RawPayloadContentType = "application/json",
            RawPayloadJson = JsonSerializer.Serialize(payload)
        });

        return Ok();
    }
}
