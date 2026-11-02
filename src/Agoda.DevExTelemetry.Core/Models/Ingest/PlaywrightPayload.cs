using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Models.Ingest.Json;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

/// <summary>
/// <c>POST /playwright</c>: an agoda-test-metrics Playwright run. <see cref="TestCases"/> holds one
/// entry per attempt (retry 0, 1, ...), so a test that passes on retry appears once failed and once passed.
/// </summary>
public class PlaywrightPayload
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

    [JsonPropertyName("playwrightVersion")]
    public string? PlaywrightVersion { get; set; }

    [JsonPropertyName("runId")]
    public string? RunId { get; set; }

    [JsonPropertyName("workers")]
    public int? Workers { get; set; }

    [JsonPropertyName("retries")]
    public int? Retries { get; set; }

    [JsonPropertyName("files")]
    public List<PlaywrightFileResult>? Files { get; set; }

    [JsonPropertyName("testcases")]
    public List<PlaywrightTestCase>? TestCases { get; set; }
}

public class PlaywrightFileResult
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}

public class PlaywrightTestCase
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }

    /// <summary><c>passed|failed|timedOut|skipped|interrupted</c>; timedOut and interrupted are stored as Failed.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("retry")]
    public int Retry { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("startTime")]
    [JsonConverter(typeof(EpochOrIsoConverter))]
    public long? StartTime { get; set; }

    [JsonPropertyName("errorMessage")]
    [JsonConverter(typeof(StringOrStringArrayConverter))]
    public string? ErrorMessage { get; set; }
}
