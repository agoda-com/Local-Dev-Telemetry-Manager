using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class JUnitController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IBackgroundTaskQueue<IngestTestRunWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IStatusNormalizer _statusNormalizer;

    public JUnitController(
        IBackgroundTaskQueue<IngestTestRunWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IStatusNormalizer statusNormalizer)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("junit")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] JUnitPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, payload.RunId);

        var testRunId = payload.Id ?? Guid.NewGuid().ToString();
        var testCases = new List<TestCase>();
        int passed = 0, failed = 0, skipped = 0;

        if (payload.JUnitTestCases != null)
        {
            foreach (var tc in payload.JUnitTestCases)
            {
                var status = _statusNormalizer.Normalize(tc.Result);
                switch (status)
                {
                    case "Passed": passed++; break;
                    case "Failed": failed++; break;
                    case "Skipped": skipped++; break;
                }

                testCases.Add(new TestCase
                {
                    TestRunId = testRunId,
                    OriginalId = tc.Id,
                    Name = tc.Name ?? string.Empty,
                    FullName = tc.FullName,
                    ClassName = tc.ClassName,
                    MethodName = tc.MethodName,
                    Status = status,
                    DurationMs = tc.Duration,
                    StartTime = tc.StartTime,
                    EndTime = tc.EndTime,
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
            Repository = payload.Repository ?? string.Empty,
            RepositoryName = payload.RepositoryName ?? string.Empty,
            TestRunner = "JUnit",
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = testCases.Count,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TotalDurationMs = testCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/junit"
        };

        await _queue.QueueBackgroundWorkItemAsync(new IngestTestRunWorkItem
        {
            TestRun = testRun,
            TestCases = testCases,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/junit",
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
