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
public class VitestController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IIngestService _ingestService;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IStatusNormalizer _statusNormalizer;

    public VitestController(
        IIngestService ingestService,
        IEnvironmentDetector environmentDetector,
        IStatusNormalizer statusNormalizer)
    {
        _ingestService = ingestService;
        _environmentDetector = environmentDetector;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("vitest")]
    public async Task<IActionResult> Ingest([FromBody] VitestPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, payload.RunId);

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
                    ErrorMessage = Truncate(tc.ErrorMessage, MaxErrorMessageLength)
                });
            }
        }

        var filesExtraData = payload.Files != null
            ? JsonSerializer.Serialize(payload.Files)
            : null;

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
            TestRunner = "Vitest",
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = testCases.Count,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TotalDurationMs = testCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/vitest",
            ExtraData = filesExtraData
        };

        await _ingestService.IngestTestRunAsync(testRun, testCases);
        await _ingestService.StoreRawPayloadAsync("/vitest", "application/json",
            JsonSerializer.Serialize(payload));

        return Ok();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
