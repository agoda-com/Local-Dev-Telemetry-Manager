using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class CommandController : ControllerBase
{
    private static readonly string[] AllowedPhases =
    {
        "install",
        "codegen",
        "typecheck",
        "lint",
        "test",
        "build",
        "devserver",
        "clientready"
    };

    private readonly IBackgroundTaskQueue<IngestCommandEventWorkItem> _queue;

    public CommandController(IBackgroundTaskQueue<IngestCommandEventWorkItem> queue)
    {
        _queue = queue;
    }

    [HttpPost("command")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] CommandPayload payload)
    {
        if (!string.Equals(payload.Type, "command", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid type value" });

        if (string.IsNullOrWhiteSpace(payload.Phase) ||
            Array.IndexOf(AllowedPhases, payload.Phase.ToLowerInvariant()) < 0)
        {
            return BadRequest(new { error = "Invalid phase value" });
        }

        if (string.IsNullOrWhiteSpace(payload.Command))
            return BadRequest(new { error = "command is required" });

        if (!TryGetDouble(payload.TimeTaken, out var timeTakenMs) || !double.IsFinite(timeTakenMs) || timeTakenMs < 0)
            return BadRequest(new { error = "Invalid timeTaken value" });

        var platform = ResolvePlatform(payload.Platform);

        object? builtAt = payload.BuiltAt.ValueKind == JsonValueKind.Undefined ? null : payload.BuiltAt;
        object? cpuModels = payload.CpuModels.ValueKind == JsonValueKind.Undefined ? null : payload.CpuModels;
        object? cpuSpeed = payload.CpuSpeed.ValueKind == JsonValueKind.Undefined ? null : payload.CpuSpeed;

        var extraData = new
        {
            payload.Timestamp,
            BuiltAt = builtAt,
            payload.TotalMemory,
            CpuModels = cpuModels,
            CpuSpeed = cpuSpeed,
            payload.NodeVersion,
            payload.V8Version,
            payload.CustomIdentifier,
            payload.AdditionalData
        };

        var commandEventId = payload.Id ?? Guid.NewGuid().ToString();
        var commandEvent = new CommandEvent
        {
            Id = commandEventId,
            SessionId = payload.SessionId,
            UserName = payload.UserName ?? string.Empty,
            CpuCount = payload.CpuCount,
            Hostname = payload.Hostname ?? string.Empty,
            Platform = platform,
            Os = payload.Os ?? string.Empty,
            Branch = payload.Branch ?? string.Empty,
            ProjectName = payload.ProjectName ?? string.Empty,
            Repository = payload.Repository ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            Type = "command",
            Phase = payload.Phase!.ToLowerInvariant(),
            Command = payload.Command,
            ExitCode = payload.ExitCode,
            Success = payload.Success,
            Signal = payload.Signal,
            ErrorCount = payload.ErrorCount,
            TimeTakenMs = timeTakenMs,
            PackageManager = payload.PackageManager,
            PackageManagerVersion = payload.PackageManagerVersion,
            ColdInstall = payload.ColdInstall,
            LockfileChanged = payload.LockfileChanged,
            MeasurementSource = payload.MeasurementSource,
            Prebundled = payload.Prebundled,
            DomContentLoadedMs = payload.DomContentLoadedMs,
            FirstContentfulPaintMs = payload.FirstContentfulPaintMs,
            SpooledAt = payload.SpooledAt,
            CommitSha = payload.CommitSha,
            SourceEndpoint = "/command",
            ExtraData = JsonSerializer.Serialize(extraData)
        };

        var npmTimers = new List<CommandEventNpmTimer>();
        if (payload.NpmTimers != null)
        {
            foreach (var timer in payload.NpmTimers)
            {
                npmTimers.Add(new CommandEventNpmTimer
                {
                    CommandEventId = commandEventId,
                    TimerName = timer.Key,
                    DurationMs = timer.Value
                });
            }
        }

        await _queue.QueueBackgroundWorkItemAsync(new IngestCommandEventWorkItem
        {
            CommandEvent = commandEvent,
            NpmTimers = npmTimers,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/command",
            RawPayloadContentType = "application/json"
        });

        return Ok();
    }

    private static bool TryGetDouble(JsonElement value, out double result)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetDouble(out result);

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out result);
        }

        result = default;
        return false;
    }

    private static string ResolvePlatform(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var platformId))
            return ((PlatformID)platformId).ToString();

        return string.Empty;
    }
}
