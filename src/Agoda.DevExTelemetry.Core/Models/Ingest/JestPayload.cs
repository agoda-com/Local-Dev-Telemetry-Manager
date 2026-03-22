using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class JestPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("cpuCount")]
    public int CpuCount { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("platform")]
    public int Platform { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("isDebuggerAttached")]
    public bool IsDebuggerAttached { get; set; }

    [JsonPropertyName("testRunner")]
    public string? TestRunner { get; set; }

    [JsonPropertyName("testRunnerVersion")]
    public string? TestRunnerVersion { get; set; }

    [JsonPropertyName("testCaseSummary")]
    public JestTestCaseSummary? TestCaseSummary { get; set; }
}

public class JestTestCaseSummary
{
    [JsonPropertyName("numFailedTestSuites")]
    public int NumFailedTestSuites { get; set; }

    [JsonPropertyName("numPassedTestSuites")]
    public int NumPassedTestSuites { get; set; }

    [JsonPropertyName("numTotalTestSuites")]
    public int NumTotalTestSuites { get; set; }

    [JsonPropertyName("numFailedTests")]
    public int NumFailedTests { get; set; }

    [JsonPropertyName("numPassedTests")]
    public int NumPassedTests { get; set; }

    [JsonPropertyName("numPendingTests")]
    public int NumPendingTests { get; set; }

    [JsonPropertyName("numTotalTests")]
    public int NumTotalTests { get; set; }

    [JsonPropertyName("startTime")]
    public long StartTime { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("testResults")]
    public List<JestTestSuiteResult>? TestResults { get; set; }
}

public class JestTestSuiteResult
{
    [JsonPropertyName("testFilePath")]
    public string? TestFilePath { get; set; }

    [JsonPropertyName("numFailingTests")]
    public int NumFailingTests { get; set; }

    [JsonPropertyName("numPassingTests")]
    public int NumPassingTests { get; set; }

    [JsonPropertyName("numPendingTests")]
    public int NumPendingTests { get; set; }

    [JsonPropertyName("testResults")]
    public List<JestTestResult>? TestResults { get; set; }
}

public class JestTestResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("ancestorTitles")]
    public List<string>? AncestorTitles { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("failureMessages")]
    public List<string>? FailureMessages { get; set; }
}
