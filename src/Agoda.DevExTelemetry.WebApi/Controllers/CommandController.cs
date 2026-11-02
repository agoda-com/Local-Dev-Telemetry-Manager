using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

/// <summary>
/// <c>POST /command</c>: a devfeedback-js <c>CommandBuildData</c> event (install, dev server, package script or
/// task-runner target), stored as a BuildMetric with <c>MetricType = "command"</c>.
/// </summary>
[ApiController]
public class CommandController : ControllerBase
{
    private const string MetricType = "command";

    private readonly IBackgroundTaskQueue<IngestBuildMetricWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IBuildCategoryClassifier _classifier;

    public CommandController(
        IBackgroundTaskQueue<IngestBuildMetricWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IBuildCategoryClassifier classifier)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _classifier = classifier;
    }

    [HttpPost("command")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] CommandPayload payload)
    {
        var platformStr = payload.Platform.ToString();
        var environment = _environmentDetector.Detect(payload.Hostname, false, platformStr, null);
        var (buildCategory, reloadType) = _classifier.Classify(MetricType, null);

        if (!double.TryParse(payload.TimeTaken, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var timeTakenMs) && !string.IsNullOrWhiteSpace(payload.TimeTaken))
            return BadRequest(new { error = "Invalid timeTaken value" });
        if (!double.IsFinite(timeTakenMs) || timeTakenMs < 0)
            return BadRequest(new { error = "Invalid timeTaken value" });

        var extraData = new
        {
            payload.Phase,
            payload.Command,
            payload.Outcome,
            payload.Success,
            payload.Signal,
            payload.ExitCode,
            payload.SessionId,
            payload.Runner,
            payload.Project,
            payload.Target,
            payload.RootCommand,
            payload.RunId,
            payload.PackageManager,
            payload.PackageManagerVersion,
            payload.ColdInstall,
            payload.LockfileChanged,
            payload.MeasurementSource,
            payload.NpmTimers,
            payload.Prebundled,
            payload.DomContentLoadedMs,
            payload.FirstContentfulPaintMs,
            payload.ErrorCount,
            payload.Timestamp,
            payload.BuiltAt,
            payload.SpooledAt,
            payload.TotalMemory,
            payload.CpuModels,
            payload.CpuSpeed,
            payload.NodeVersion,
            payload.CustomIdentifier
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
            MetricType = MetricType,
            BuildCategory = buildCategory,
            ReloadType = reloadType,
            CommitSha = payload.CommitSha,
            IsDebuggerAttached = false,
            ExecutionEnvironment = environment,
            SourceEndpoint = "/command",
            ExtraData = JsonSerializer.Serialize(extraData)
        };

        await _queue.QueueBackgroundWorkItemAsync(new IngestBuildMetricWorkItem
        {
            BuildMetric = metric,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/command",
            RawPayloadContentType = "application/json"
        });

        return Ok();
    }
}
