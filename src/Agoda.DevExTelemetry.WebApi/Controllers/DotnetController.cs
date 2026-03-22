using System;
using System.Collections.Generic;
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
public class DotnetController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IIngestService _ingestService;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IBuildCategoryClassifier _classifier;
    private readonly IStatusNormalizer _statusNormalizer;

    public DotnetController(
        IIngestService ingestService,
        IEnvironmentDetector environmentDetector,
        IBuildCategoryClassifier classifier,
        IStatusNormalizer statusNormalizer)
    {
        _ingestService = ingestService;
        _environmentDetector = environmentDetector;
        _classifier = classifier;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("dotnet")]
    public async Task<IActionResult> IngestBuild([FromBody] DotnetMsBuildPayload payload)
    {
        var metricType = payload.Type ?? ".Net";
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, null);
        var (buildCategory, reloadType) = _classifier.Classify(metricType, null);

        if (!TryParseFinitePositive(payload.TimeTaken, out var timeTakenMs))
            return BadRequest(new { error = "Invalid timeTaken value" });

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
            MetricType = metricType,
            BuildCategory = buildCategory,
            ReloadType = reloadType,
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            SourceEndpoint = "/dotnet"
        };

        await _ingestService.IngestBuildMetricAsync(metric);
        await _ingestService.StoreRawPayloadAsync("/dotnet", "application/json",
            JsonSerializer.Serialize(payload));

        return Ok();
    }

    [HttpPost("dotnet/nunit")]
    public async Task<IActionResult> IngestNUnit([FromBody] NUnitPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, payload.RunId);

        var testRunId = payload.Id ?? Guid.NewGuid().ToString();
        var testCases = new List<TestCase>();
        int passed = 0, failed = 0, skipped = 0;

        if (payload.NUnitTestCases != null)
        {
            foreach (var tc in payload.NUnitTestCases)
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
                    DurationMs = tc.Duration.HasValue ? tc.Duration.Value * 1000 : null,
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
            TestRunner = "NUnit",
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = testCases.Count,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TotalDurationMs = testCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/dotnet/nunit"
        };

        await _ingestService.IngestTestRunAsync(testRun, testCases);
        await _ingestService.StoreRawPayloadAsync("/dotnet/nunit", "application/json",
            JsonSerializer.Serialize(payload));

        return Ok();
    }

    private static bool TryParseFinitePositive(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out result))
            return false;

        if (!double.IsFinite(result) || result < 0)
            return false;

        return true;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
