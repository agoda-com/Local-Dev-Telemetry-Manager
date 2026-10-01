using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class CommandPayload
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("cpuCount")]
    public int CpuCount { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("platform")]
    public JsonElement Platform { get; set; }

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

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    [JsonPropertyName("builtAt")]
    public JsonElement BuiltAt { get; set; }

    [JsonPropertyName("totalMemory")]
    public long? TotalMemory { get; set; }

    [JsonPropertyName("cpuModels")]
    public JsonElement CpuModels { get; set; }

    [JsonPropertyName("cpuSpeed")]
    public JsonElement CpuSpeed { get; set; }

    [JsonPropertyName("nodeVersion")]
    public string? NodeVersion { get; set; }

    [JsonPropertyName("v8Version")]
    public string? V8Version { get; set; }

    [JsonPropertyName("commitSha")]
    public string? CommitSha { get; set; }

    [JsonPropertyName("customIdentifier")]
    public string? CustomIdentifier { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("signal")]
    public string? Signal { get; set; }

    [JsonPropertyName("errorCount")]
    public int? ErrorCount { get; set; }

    [JsonPropertyName("timeTaken")]
    public JsonElement TimeTaken { get; set; }

    [JsonPropertyName("packageManager")]
    public string? PackageManager { get; set; }

    [JsonPropertyName("packageManagerVersion")]
    public string? PackageManagerVersion { get; set; }

    [JsonPropertyName("coldInstall")]
    public bool? ColdInstall { get; set; }

    [JsonPropertyName("lockfileChanged")]
    public bool? LockfileChanged { get; set; }

    [JsonPropertyName("measurementSource")]
    public string? MeasurementSource { get; set; }

    [JsonPropertyName("npmTimers")]
    public Dictionary<string, double>? NpmTimers { get; set; }

    [JsonPropertyName("prebundled")]
    public bool? Prebundled { get; set; }

    [JsonPropertyName("domContentLoadedMs")]
    public double? DomContentLoadedMs { get; set; }

    [JsonPropertyName("firstContentfulPaintMs")]
    public double? FirstContentfulPaintMs { get; set; }

    [JsonPropertyName("spooledAt")]
    public long? SpooledAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
