namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class TestRun
{
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int CpuCount { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string TestRunner { get; set; } = string.Empty;
    public bool IsDebuggerAttached { get; set; }
    public string ExecutionEnvironment { get; set; } = string.Empty;
    public int? TotalTests { get; set; }
    public int? PassedTests { get; set; }
    public int? FailedTests { get; set; }
    public int? SkippedTests { get; set; }
    public double? TotalDurationMs { get; set; }
    public string SourceEndpoint { get; set; } = string.Empty;
    public string? ExtraData { get; set; }
    public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
}
