using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class WebpackPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

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

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("builtAt")]
    public long BuiltAt { get; set; }

    [JsonPropertyName("totalMemory")]
    public long TotalMemory { get; set; }

    [JsonPropertyName("cpuModels")]
    public List<string>? CpuModels { get; set; }

    [JsonPropertyName("cpuSpeed")]
    public double CpuSpeed { get; set; }

    [JsonPropertyName("nodeVersion")]
    public string? NodeVersion { get; set; }

    [JsonPropertyName("v8Version")]
    public string? V8Version { get; set; }

    [JsonPropertyName("commitSha")]
    public string? CommitSha { get; set; }

    [JsonPropertyName("timeTaken")]
    public string? TimeTaken { get; set; }

    [JsonPropertyName("devFeedback")]
    public List<WebpackDevFeedbackItem>? DevFeedback { get; set; }
}

public class WebpackDevFeedbackItem
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("timeTaken")]
    public string? TimeTaken { get; set; }
}
