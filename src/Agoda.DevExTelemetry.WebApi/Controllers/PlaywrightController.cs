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

/// <summary>
/// <c>POST /playwright</c>: an agoda-test-metrics Playwright run, stored as one TestRun with one TestCase per
/// attempt, so a test that passes on retry is stored once Failed and once Passed.
/// </summary>
[ApiController]
public class PlaywrightController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IBackgroundTaskQueue<IngestTestRunWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IStatusNormalizer _statusNormalizer;

    public PlaywrightController(
        IBackgroundTaskQueue<IngestTestRunWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IStatusNormalizer statusNormalizer)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("playwright")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Ingest([FromBody] PlaywrightPayload payload)
    {
        var platformStr = payload.Platform.ToString();
        var environment = _environmentDetector.Detect(payload.Hostname, false, platformStr, payload.RunId);

        var testRunId = payload.Id ?? Guid.NewGuid().ToString();
        var testCases = new List<TestCase>();
        int passed = 0, failed = 0, skipped = 0;

        if (payload.TestCases != null)
        {
            foreach (var tc in payload.TestCases)
            {
                var status = _statusNormalizer.Normalize(tc.Status);
                switch (status)
                {
                    case "Passed": passed++; break;
                    case "Failed": failed++; break;
                    case "Skipped": skipped++; break;
                }

                testCases.Add(new TestCase
                {
                    TestRunId = testRunId,
                    Name = tc.Name ?? string.Empty,
                    FullName = tc.FullName,
                    ClassName = tc.File,
                    Status = status,
                    DurationMs = tc.Duration,
                    StartTime = tc.StartTime.HasValue
                        ? DateTimeOffset.FromUnixTimeMilliseconds(tc.StartTime.Value).ToString("O")
                        : null,
                    ErrorMessage = Truncate(tc.ErrorMessage, MaxErrorMessageLength)
                });
            }
        }

        var extraData = new
        {
            payload.PlaywrightVersion,
            payload.Workers,
            payload.Retries,
            payload.Files
        };

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
            TestRunner = "Playwright",
            IsDebuggerAttached = false,
            ExecutionEnvironment = environment,
            TotalTests = testCases.Count,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TotalDurationMs = testCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/playwright",
            ExtraData = JsonSerializer.Serialize(extraData)
        };

        await _queue.QueueBackgroundWorkItemAsync(new IngestTestRunWorkItem
        {
            TestRun = testRun,
            TestCases = testCases,
            RawPayloadJson = JsonSerializer.Serialize(payload),
            RawPayloadEndpoint = "/playwright",
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
