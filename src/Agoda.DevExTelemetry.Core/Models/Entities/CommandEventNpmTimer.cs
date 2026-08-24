namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class CommandEventNpmTimer
{
    public int Id { get; set; }
    public string CommandEventId { get; set; } = string.Empty;
    public string TimerName { get; set; } = string.Empty;
    public double DurationMs { get; set; }

    public CommandEvent CommandEvent { get; set; } = null!;
}
