using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class GradleBuildMetricPayload : JvmBuildMetricPayload
{
    [JsonPropertyName("ide")]
    public string? Ide { get; set; }

    [JsonPropertyName("buildKind")]
    public string? BuildKind { get; set; }

    [JsonPropertyName("requestedTasks")]
    public List<string>? RequestedTasks { get; set; }

    [JsonPropertyName("taskCount")]
    public int TaskCount { get; set; }

    [JsonPropertyName("executedTaskCount")]
    public int ExecutedTaskCount { get; set; }

    [JsonPropertyName("upToDateTaskCount")]
    public int UpToDateTaskCount { get; set; }

    [JsonPropertyName("fromCacheTaskCount")]
    public int FromCacheTaskCount { get; set; }

    [JsonPropertyName("failedTaskCount")]
    public int FailedTaskCount { get; set; }

    [JsonPropertyName("compileTaskCount")]
    public int CompileTaskCount { get; set; }

    [JsonPropertyName("compileTimeMs")]
    public long CompileTimeMs { get; set; }

    [JsonPropertyName("taskTimeMs")]
    public long TaskTimeMs { get; set; }

    [JsonPropertyName("projects")]
    public List<GradleProjectMetricPayload>? Projects { get; set; }
}
