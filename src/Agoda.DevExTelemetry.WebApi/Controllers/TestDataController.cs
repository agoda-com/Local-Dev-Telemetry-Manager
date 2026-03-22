using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Agoda.DevExTelemetry.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

[ApiController]
public class TestDataController : ControllerBase
{
    private readonly IBackgroundTaskQueue<IngestTestRunWorkItem> _queue;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IJUnitXmlParser _junitXmlParser;

    public TestDataController(
        IBackgroundTaskQueue<IngestTestRunWorkItem> queue,
        IEnvironmentDetector environmentDetector,
        IJUnitXmlParser junitXmlParser)
    {
        _queue = queue;
        _environmentDetector = environmentDetector;
        _junitXmlParser = junitXmlParser;
    }

    [HttpPost("testdata/junit")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> IngestJUnit(
        [FromForm] string? id,
        [FromForm] string? runId,
        [FromForm] string? userName,
        [FromForm] string? hostname,
        [FromForm] string? platform,
        [FromForm] string? os,
        [FromForm] string? branch,
        [FromForm] string? projectName,
        [FromForm] string? repository,
        [FromForm] string? repositoryName,
        [FromForm] bool isDebuggerAttached,
        IFormFileCollection files)
    {
        var environment = _environmentDetector.Detect(
            hostname, isDebuggerAttached, platform, runId);

        var testRunId = id ?? Guid.NewGuid().ToString();
        var allTestCases = new List<TestCase>();

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            var result = _junitXmlParser.Parse(stream, testRunId);
            if (result.HasParseErrors)
                return BadRequest(new { error = result.ErrorMessage ?? "Invalid JUnit XML", fileName = file.FileName });
            allTestCases.AddRange(result.TestCases);
        }

        int passed = allTestCases.Count(tc => tc.Status == "Passed");
        int failed = allTestCases.Count(tc => tc.Status == "Failed");
        int skipped = allTestCases.Count(tc => tc.Status == "Skipped");

        var testRun = new TestRun
        {
            Id = testRunId,
            RunId = runId ?? string.Empty,
            UserName = userName ?? string.Empty,
            CpuCount = 0,
            Hostname = hostname ?? string.Empty,
            Platform = platform ?? string.Empty,
            Os = os ?? string.Empty,
            Branch = branch ?? string.Empty,
            ProjectName = projectName ?? string.Empty,
            Repository = repository ?? string.Empty,
            RepositoryName = repositoryName ?? string.Empty,
            TestRunner = "JUnit-XML",
            IsDebuggerAttached = isDebuggerAttached,
            ExecutionEnvironment = environment,
            TotalTests = allTestCases.Count,
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TotalDurationMs = allTestCases.Where(tc => tc.DurationMs.HasValue).Sum(tc => tc.DurationMs!.Value),
            SourceEndpoint = "/testdata/junit"
        };

        var testSuiteCounts = allTestCases
            .GroupBy(tc => tc.ClassName ?? "Unknown")
            .Select(g => new { name = g.Key, numberOfTests = g.Count() })
            .ToList();

        await _queue.QueueBackgroundWorkItemAsync(new IngestTestRunWorkItem
        {
            TestRun = testRun,
            TestCases = allTestCases
        });

        return Ok(new
        {
            localTestRun = new
            {
                testRun.Id,
                testRun.TotalTests,
                testRun.PassedTests,
                testRun.FailedTests,
                testRun.SkippedTests,
                testRun.TotalDurationMs,
                testRun.ExecutionEnvironment
            },
            testSuiteCounts
        });
    }
}
