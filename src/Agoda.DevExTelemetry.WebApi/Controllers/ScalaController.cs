using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class ScalaController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IBackgroundTaskQueue<IngestTestRunWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IStatusNormalizer _statusNormalizer;

    public ScalaController(
        IBackgroundTaskQueue<IngestTestRunWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IStatusNormalizer statusNormalizer)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("scala/scalatest")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] ScalaTestPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, payload.RunId);

        var testRunId = payload.Id ?? Guid.NewGuid().ToString();
        var testCases = new List<TestCase>();

        if (payload.ScalaTestCases != null)
        {
            foreach (var tc in payload.ScalaTestCases)
            {
                var status = _statusNormalizer.Normalize(tc.Result);

                testCases.Add(new TestCase
                {
                    TestRunId = testRunId,
                    Name = tc.Name ?? string.Empty,
                    FullName = tc.ClassName != null && tc.Name != null
                        ? $"{tc.ClassName}.{tc.Name}"
                        : tc.Name ?? string.Empty,
                    ClassName = tc.ClassName,
                    Status = status,
                    DurationMs = tc.Duration,
                    ErrorMessage = Truncate(tc.ErrorMessage, MaxErrorMessageLength)
                });
            }
        }

        var testRun = new TestRun
        {
            Id = testRunId,
            RunId = payload.RunId ?? string.Empty,
            UserName = payload.UserName ?? string.Empty,
            CpuCount = payload.CpuCount,
            Hostname = payload.Hostname ?? string.Empty,
            Platform = platformStr,
            Os = payload.Os ?? string.Empty,
            Branch = payload.Branch ?? string.Empty,
            ProjectName = payload.ProjectName ?? string.Empty,
            Repository = payload.RepositoryUrl ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            TestRunner = "ScalaTest",
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = payload.TotalTests ?? testCases.Count,
            PassedTests = payload.SucceededTests ?? 0,
            FailedTests = payload.FailedTests ?? 0,
            SkippedTests = payload.IgnoredTests ?? 0,
            TotalDurationMs = payload.TotalDuration,
            SourceEndpoint = "/scala/scalatest"
        };

        await _queue.QueueBackgroundWorkItemAsync(new IngestTestRunWorkItem
        {
            TestRun = testRun,
            TestCases = testCases,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/scala/scalatest",
            RawPayloadContentType = "application/json"
        });

        return Ok();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
