using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.WebApi.Services;

[RegisterPerRequest]
public class JvmBuildMetricIngestService : IJvmBuildMetricIngestService
{
    private readonly IBackgroundTaskQueue<IngestBuildMetricWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;

    public JvmBuildMetricIngestService(
        IBackgroundTaskQueue<IngestBuildMetricWorkItem> queue,
        IEnvironmentDetector environmentDetector)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
    }

    public async Task<string?> QueueGradleAsync(GradleBuildMetricPayload payload)
    {
        if (!string.Equals(payload.Type, "Gradle", StringComparison.Ordinal))
            return "Invalid type value";

        if (!long.TryParse(payload.TimeTaken, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var timeTakenMs) || timeTakenMs < 0)
            return "Invalid timeTaken value";

        var extraData = new Dictionary<string, object?>
        {
            ["date"] = payload.Date,
            ["ide"] = payload.Ide,
            ["buildKind"] = payload.BuildKind,
            ["requestedTasks"] = payload.RequestedTasks,
            ["taskCount"] = payload.TaskCount,
            ["executedTaskCount"] = payload.ExecutedTaskCount,
            ["upToDateTaskCount"] = payload.UpToDateTaskCount,
            ["fromCacheTaskCount"] = payload.FromCacheTaskCount,
            ["failedTaskCount"] = payload.FailedTaskCount,
            ["compileTaskCount"] = payload.CompileTaskCount,
            ["compileTimeMs"] = payload.CompileTimeMs,
            ["taskTimeMs"] = payload.TaskTimeMs,
            ["projects"] = payload.Projects
        };

        await QueueAsync(
            payload,
            timeTakenMs,
            "Gradle",
            "/gradle",
            JsonSerializer.Serialize(extraData),
            JsonSerializer.Serialize(payload));

        return null;
    }

    public async Task<string?> QueueKtorAsync(KtorStartupMetricPayload payload)
    {
        if (payload.Type is not ".KtorStartup" and not ".KtorResponse")
            return "Invalid type value";

        if (!double.TryParse(payload.TimeTaken, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var timeTakenMs) || !double.IsFinite(timeTakenMs) || timeTakenMs < 0)
            return "Invalid timeTaken value";

        var extraData = new Dictionary<string, object?>
        {
            ["date"] = payload.Date,
            ["tags"] = payload.Tags
        };

        await QueueAsync(
            payload,
            timeTakenMs,
            payload.Type,
            "/ktor",
            JsonSerializer.Serialize(extraData),
            JsonSerializer.Serialize(payload));

        return null;
    }

    private async Task QueueAsync(
        JvmBuildMetricPayload payload,
        double timeTakenMs,
        string metricType,
        string endpoint,
        string extraData,
        string rawPayload)
    {
        var platform = payload.Platform ?? string.Empty;
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platform, null);

        var metric = new BuildMetric
        {
            Id = payload.Id ?? Guid.NewGuid().ToString(),
            UserName = payload.UserName ?? string.Empty,
            CpuCount = payload.CpuCount,
            Hostname = payload.Hostname ?? string.Empty,
            Platform = platform,
            Os = payload.Os ?? string.Empty,
            Branch = payload.Branch ?? string.Empty,
            ProjectName = payload.ProjectName ?? string.Empty,
            Repository = payload.Repository ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            TimeTakenMs = timeTakenMs,
            MetricType = metricType,
            BuildCategory = "API",
            ReloadType = null,
            ToolVersion = payload.MetricsVersion,
            CommitSha = payload.CommitSha,
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            SourceEndpoint = endpoint,
            ExtraData = extraData
        };

        await _queue.QueueBackgroundWorkItemAsync(new IngestBuildMetricWorkItem
        {
            BuildMetric = metric,
            RawPayloadJson = rawPayload,
            RawPayloadEndpoint = endpoint,
            RawPayloadContentType = "application/json"
        });
    }
}
