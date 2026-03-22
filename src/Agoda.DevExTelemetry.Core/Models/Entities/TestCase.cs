namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class TestCase
{
    public int Id { get; set; }
    public string TestRunId { get; set; } = string.Empty;
    public string? OriginalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? DurationMs { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public TestRun TestRun { get; set; } = null!;
}
