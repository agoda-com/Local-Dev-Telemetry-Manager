using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Services;

public interface IIngestService
{
    Task IngestBuildMetricAsync(BuildMetric metric);
    Task IngestCommandEventAsync(CommandEvent commandEvent, IEnumerable<CommandEventNpmTimer> npmTimers);
    Task IngestTestRunAsync(TestRun run, IEnumerable<TestCase> testCases);
    Task StoreRawPayloadAsync(string endpoint, string contentType, string json);
}
