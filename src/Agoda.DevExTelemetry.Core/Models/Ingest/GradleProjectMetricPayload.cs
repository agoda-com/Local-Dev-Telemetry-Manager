using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class GradleProjectMetricPayload
{
    [JsonPropertyName("projectPath")]
    public string? ProjectPath { get; set; }

    [JsonPropertyName("compileTaskCount")]
    public int CompileTaskCount { get; set; }

    [JsonPropertyName("compileTimeMs")]
    public long CompileTimeMs { get; set; }
}
