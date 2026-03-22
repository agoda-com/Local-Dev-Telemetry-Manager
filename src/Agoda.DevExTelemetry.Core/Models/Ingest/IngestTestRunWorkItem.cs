using System.Collections.Generic;
using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Models.Ingest;

public class IngestTestRunWorkItem
{
    public required TestRun TestRun { get; init; }
    public required List<TestCase> TestCases { get; init; }
    public string? RawPayloadEndpoint { get; init; }
    public string? RawPayloadContentType { get; init; }
    public string? RawPayloadJson { get; init; }
}
