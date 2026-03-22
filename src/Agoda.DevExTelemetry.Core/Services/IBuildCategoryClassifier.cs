namespace Agoda.DevExTelemetry.Core.Services;

public interface IBuildCategoryClassifier
{
    (string BuildCategory, string? ReloadType) Classify(string metricType, string? devFeedbackType);
}
