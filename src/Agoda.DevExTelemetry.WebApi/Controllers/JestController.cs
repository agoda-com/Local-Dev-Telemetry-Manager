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
public class JestController : ControllerBase
{
    private const int MaxErrorMessageLength = 4096;

    private readonly IIngestService _ingestService;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IStatusNormalizer _statusNormalizer;

    public JestController(
        IIngestService ingestService,
        IEnvironmentDetector environmentDetector,
        IStatusNormalizer statusNormalizer)
    {
        _ingestService = ingestService;
        _environmentDetector = environmentDetector;
        _statusNormalizer = statusNormalizer;
    }

    [HttpPost("jest")]
    public async Task<IActionResult> Ingest([FromBody] JestPayload payload)
    {
        var platformStr = ((PlatformID)payload.Platform).ToString();
        var environment = _environmentDetector.Detect(
            payload.Hostname, payload.IsDebuggerAttached, platformStr, payload.RunId);

        var testRunId = payload.Id ?? Guid.NewGuid().ToString();
        var testCases = new List<TestCase>();
        int passed = 0, failed = 0, skipped = 0;

        if (payload.TestCaseSummary?.TestResults != null)
        {
            foreach (var suite in payload.TestCaseSummary.TestResults)
            {
                if (suite.TestResults == null) continue;

                foreach (var tr in suite.TestResults)
                {
                    var status = _statusNormalizer.Normalize(tr.Status);
                    switch (status)
                    {
                        case "Passed": passed++; break;
                        case "Failed": failed++; break;
                        case "Skipped": skipped++; break;
                    }

                    var fullName = tr.AncestorTitles != null && tr.AncestorTitles.Count > 0
                        ? string.Join(" > ", tr.AncestorTitles) + " > " + tr.Title
                        : tr.FullName ?? tr.Title;

                    var errorMessage = tr.FailureMessages != null && tr.FailureMessages.Count > 0
                        ? string.Join("\n", tr.FailureMessages)
                        : null;

                    testCases.Add(new TestCase
                    {
                        TestRunId = testRunId,
                        Name = tr.Title ?? string.Empty,
                        FullName = fullName,
                        ClassName = suite.TestFilePath,
                        Status = status,
                        DurationMs = tr.Duration,
                        ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength)
                    });
                }
            }
        }

        var summary = payload.TestCaseSummary;
        var extraData = summary != null
            ? new
            {
                summary.NumFailedTestSuites,
                summary.NumPassedTestSuites,
                summary.NumTotalTestSuites,
                summary.StartTime,
                summary.Success
            }
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
            TestRunner = payload.TestRunner ?? "Jest",
            IsDebuggerAttached = payload.IsDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = summary?.NumTotalTests ?? testCases.Count,
            PassedTests = summary?.NumPassedTests ?? passed,
            FailedTests = summary?.NumFailedTests ?? failed,
            SkippedTests = summary?.NumPendingTests ?? skipped,
            TotalDurationMs = testCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/jest",
            ExtraData = extraData != null ? JsonSerializer.Serialize(extraData) : null
        };

        await _ingestService.IngestTestRunAsync(testRun, testCases);
        await _ingestService.StoreRawPayloadAsync("/jest", "application/json",
            JsonSerializer.Serialize(payload));

        return Ok();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
