using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class BuildCategoryClassifier : IBuildCategoryClassifier
{
    public (string BuildCategory, string? ReloadType) Classify(string metricType, string? devFeedbackType)
    {
        var normalizedType = metricType?.ToLowerInvariant() ?? string.Empty;

        return normalizedType switch
        {
            ".net" or ".aspnetstartup" or ".aspnetresponse" or "gradletalaiot"
                => ("API", null),

            "vitehmr"
                => ("Clientside", "hot"),

            "vite" or "webpack" or "rspack" or "rsbuild"
                => ("Clientside", ResolveReloadType(devFeedbackType)),

            _ => ("API", null)
        };
    }

    private static string ResolveReloadType(string? devFeedbackType)
    {
        if (string.Equals(devFeedbackType, "hmr", StringComparison.OrdinalIgnoreCase))
            return "hot";

        return "full";
    }
}
