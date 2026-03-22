using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class ScalaTestPayload
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

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("isDebuggerAttached")]
    public bool IsDebuggerAttached { get; set; }

    [JsonPropertyName("totalTests")]
    public int? TotalTests { get; set; }

    [JsonPropertyName("succeededTests")]
    public int? SucceededTests { get; set; }

    [JsonPropertyName("failedTests")]
    public int? FailedTests { get; set; }

    [JsonPropertyName("ignoredTests")]
    public int? IgnoredTests { get; set; }

    [JsonPropertyName("totalDuration")]
    public double? TotalDuration { get; set; }

    [JsonPropertyName("scalaTestCases")]
    public List<ScalaTestCaseItem>? ScalaTestCases { get; set; }
}

public class ScalaTestCaseItem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("className")]
    public string? ClassName { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
