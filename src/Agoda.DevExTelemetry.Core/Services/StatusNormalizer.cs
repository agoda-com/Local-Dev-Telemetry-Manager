using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class StatusNormalizer : IStatusNormalizer
{
    public string Normalize(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return "Unknown";

        return rawStatus.Trim().ToLowerInvariant() switch
        {
            "passed" or "pass" or "succeeded" => "Passed",
            "failed" or "fail" => "Failed",
            "skipped" or "skip" or "ignored" => "Skipped",
            "pending" or "todo" => "Pending",
            _ => "Unknown"
        };
    }
}
