namespace Agoda.DevExTelemetry.Core.Models.Entities;

public class RawPayload
{
    public int Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}
