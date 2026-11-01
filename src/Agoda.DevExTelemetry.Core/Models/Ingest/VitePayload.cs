using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Models.Ingest.Json;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class VitePayload
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
    [JsonConverter(typeof(LenientPlatformConverter))]
    public PlatformID Platform { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

    [JsonPropertyName("isDebuggerAttached")]
    public bool IsDebuggerAttached { get; set; }

    [JsonPropertyName("timestamp")]
    [JsonConverter(typeof(EpochOrIsoConverter))]
    public long? Timestamp { get; set; }

    [JsonPropertyName("builtAt")]
    [JsonConverter(typeof(EpochOrIsoConverter))]
    public long? BuiltAt { get; set; }

    [JsonPropertyName("totalMemory")]
    public long TotalMemory { get; set; }

    [JsonPropertyName("cpuModels")]
    public List<string>? CpuModels { get; set; }

    [JsonPropertyName("cpuSpeed")]
    [JsonConverter(typeof(NumberOrNumberArrayConverter))]
    public List<double>? CpuSpeed { get; set; }

    [JsonPropertyName("nodeVersion")]
    public string? NodeVersion { get; set; }

    [JsonPropertyName("v8Version")]
    public string? V8Version { get; set; }

    [JsonPropertyName("commitSha")]
    public string? CommitSha { get; set; }

    [JsonPropertyName("timeTaken")]
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? TimeTaken { get; set; }

    [JsonPropertyName("viteVersion")]
    public string? ViteVersion { get; set; }

    [JsonPropertyName("bundleStats")]
    public object? BundleStats { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("moduleCount")]
    public int? ModuleCount { get; set; }

    [JsonPropertyName("devFeedback")]
    public List<WebpackDevFeedbackItem>? DevFeedback { get; set; }
}
