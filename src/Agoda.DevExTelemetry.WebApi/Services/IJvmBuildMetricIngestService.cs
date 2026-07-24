using System.Threading.Tasks;
using Agoda.DevExTelemetry.Core.Models.Ingest;

namespace Agoda.DevExTelemetry.WebApi.Services;

public interface IJvmBuildMetricIngestService
{
    Task<string?> QueueGradleAsync(GradleBuildMetricPayload payload);
    Task<string?> QueueKtorAsync(KtorStartupMetricPayload payload);
}
