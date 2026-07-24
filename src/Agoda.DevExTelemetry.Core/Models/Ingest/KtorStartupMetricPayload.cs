using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class KtorStartupMetricPayload : JvmBuildMetricPayload
{
    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
