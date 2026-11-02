using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Models.Ingest.Json;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

/// <summary>
/// <c>POST /command</c>: a devfeedback-js <c>CommandBuildData</c> event (install, dev server, package
/// script or task-runner target), stored as a BuildMetric with <c>MetricType = "command"</c>.
/// </summary>
public class CommandPayload
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

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

    [JsonPropertyName("timeTaken")]
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? TimeTaken { get; set; }

    [JsonPropertyName("branch")]
    public string? Branch { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("repositoryName")]
    public string? RepositoryName { get; set; }

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

    [JsonPropertyName("customIdentifier")]
    public string? CustomIdentifier { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("success")]
    public bool? Success { get; set; }

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

    [JsonPropertyName("signal")]
    public string? Signal { get; set; }

    [JsonPropertyName("errorCount")]
    public int? ErrorCount { get; set; }

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("runner")]
    public string? Runner { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("rootCommand")]
    public string? RootCommand { get; set; }

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("spooledAt")]
    [JsonConverter(typeof(EpochOrIsoConverter))]
    public long? SpooledAt { get; set; }
}
