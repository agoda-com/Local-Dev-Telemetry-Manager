using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class EnvironmentDetector : IEnvironmentDetector
{
    private static readonly string[] CiHostnamePatterns = ["runner", "agent", "build", "ci"];

    public string Detect(string? hostname, bool isDebuggerAttached, string? platform, string? runId)
    {
        if (isDebuggerAttached)
            return "Local";

        if (string.Equals(platform, "Docker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platform, "AWS", StringComparison.OrdinalIgnoreCase))
            return "CI";

        if (!string.IsNullOrEmpty(runId) &&
            (runId.Contains("CI_JOB_ID", StringComparison.OrdinalIgnoreCase) ||
             runId.Contains("GITHUB_RUN_ID", StringComparison.OrdinalIgnoreCase)))
            return "CI";

        if (!string.IsNullOrEmpty(hostname))
        {
            var lowerHostname = hostname.ToLowerInvariant();
            foreach (var pattern in CiHostnamePatterns)
            {
                if (lowerHostname.Contains(pattern))
                    return "CI";
            }
        }

        return "Local";
    }
}
