using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Models.Ingest.Json;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class VitestPayload
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
    [JsonConverter(typeof(LenientPlatformConverter))]
    public PlatformID Platform { get; set; }

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

    [JsonPropertyName("files")]
    public List<VitestFileResult>? Files { get; set; }

    [JsonPropertyName("testcases")]
    public List<VitestTestCase>? TestCases { get; set; }
}

public class VitestFileResult
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class VitestTestCase
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    /// <summary>agoda-test-metrics sends the test file as <c>filename</c>; older reporters send <c>file</c>.</summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("errorMessage")]
    [JsonConverter(typeof(StringOrStringArrayConverter))]
    public string? ErrorMessage { get; set; }
}
