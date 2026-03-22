namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class BuildMetric
{
    public string Id { get; set; } = string.Empty;
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
    public double TimeTakenMs { get; set; }
    public string MetricType { get; set; } = string.Empty;
    public string BuildCategory { get; set; } = string.Empty;
    public string? ReloadType { get; set; }
    public string? ToolVersion { get; set; }
    public string? CommitSha { get; set; }
    public bool IsDebuggerAttached { get; set; }
    public string ExecutionEnvironment { get; set; } = string.Empty;
    public string SourceEndpoint { get; set; } = string.Empty;
    public string? ExtraData { get; set; }
}
