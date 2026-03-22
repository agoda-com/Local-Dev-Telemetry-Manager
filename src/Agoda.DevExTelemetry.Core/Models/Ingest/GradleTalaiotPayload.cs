using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class GradleTalaiotPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("cpuCount")]
    public string? CpuCount { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

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
    public string? IsDebuggerAttached { get; set; }

    [JsonPropertyName("durationMs")]
    public string? DurationMs { get; set; }

    [JsonPropertyName("configurationDurationMs")]
    public string? ConfigurationDurationMs { get; set; }

    [JsonPropertyName("requestedTasks")]
    public string? RequestedTasks { get; set; }

    [JsonPropertyName("gradleVersion")]
    public string? GradleVersion { get; set; }

    [JsonPropertyName("rootProject")]
    public string? RootProject { get; set; }

    [JsonPropertyName("success")]
    public string? Success { get; set; }

    [JsonPropertyName("buildId")]
    public string? BuildId { get; set; }

    [JsonPropertyName("cacheRatio")]
    public string? CacheRatio { get; set; }
}
