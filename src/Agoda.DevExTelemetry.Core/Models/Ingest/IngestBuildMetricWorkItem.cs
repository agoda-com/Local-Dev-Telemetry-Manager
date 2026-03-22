using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class IngestBuildMetricWorkItem
{
    public required BuildMetric BuildMetric { get; init; }
    public string? RawPayloadJson { get; init; }
    public string? RawPayloadEndpoint { get; init; }
    public string? RawPayloadContentType { get; init; }
}
