using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class IngestCommandEventWorkItem
{
    public required CommandEvent CommandEvent { get; init; }
    public required IReadOnlyList<CommandEventNpmTimer> NpmTimers { get; init; }
    public string? RawPayloadJson { get; init; }
    public string? RawPayloadEndpoint { get; init; }
    public string? RawPayloadContentType { get; init; }
}
