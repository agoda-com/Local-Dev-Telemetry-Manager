using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Services;

public class JUnitXmlParseResult
{
    public List<TestCase> TestCases { get; init; } = new();
    public bool HasParseErrors { get; init; }
    public string? ErrorMessage { get; init; }
}
