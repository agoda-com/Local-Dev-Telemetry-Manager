namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class CommandEvent
{
    public string Id { get; set; } = string.Empty;
    public string? SessionId { get; set; }
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
    public string Type { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool Success { get; set; }
    public string? Signal { get; set; }
    public int? ErrorCount { get; set; }
    public double TimeTakenMs { get; set; }
    public string? PackageManager { get; set; }
    public string? PackageManagerVersion { get; set; }
    public bool? ColdInstall { get; set; }
    public bool? LockfileChanged { get; set; }
    public string? MeasurementSource { get; set; }
    public bool? Prebundled { get; set; }
    public double? DomContentLoadedMs { get; set; }
    public double? FirstContentfulPaintMs { get; set; }
    public long? SpooledAt { get; set; }
    public string? CommitSha { get; set; }
    public string SourceEndpoint { get; set; } = string.Empty;
    public string? ExtraData { get; set; }

    public ICollection<CommandEventNpmTimer> NpmTimers { get; set; } = new List<CommandEventNpmTimer>();
}
