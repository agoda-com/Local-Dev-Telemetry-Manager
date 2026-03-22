using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class IngestService : IIngestService
{
    private readonly ITelemetryRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IngestService> _logger;

    public IngestService(ITelemetryRepository repository, IConfiguration configuration, ILogger<IngestService> logger)
    {
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task IngestBuildMetricAsync(BuildMetric metric)
    {
        metric.ReceivedAt = DateTime.UtcNow;

        if (await _repository.BuildMetricExistsAsync(metric.Id))
        {
            _logger.LogInformation("Ignoring duplicate BuildMetric with Id={Id}", metric.Id);
            return;
        }

        await _repository.AddBuildMetricAsync(metric);
    }

    public async Task IngestTestRunAsync(TestRun run, IEnumerable<TestCase> testCases)
    {
        run.ReceivedAt = DateTime.UtcNow;

        if (await _repository.TestRunExistsAsync(run.Id))
        {
            _logger.LogInformation("Ignoring duplicate TestRun with Id={Id}", run.Id);
            return;
        }

        await _repository.AddTestRunAsync(run, testCases);
    }

    public async Task StoreRawPayloadAsync(string endpoint, string contentType, string json)
    {
        var enabled = bool.TryParse(_configuration["DataRetention:EnableRawPayloadStorage"], out var e) && e;
        if (!enabled)
            return;

        var rawPayload = new RawPayload
        {
            ReceivedAt = DateTime.UtcNow,
            Endpoint = endpoint,
            ContentType = contentType,
            PayloadJson = json
        };

        await _repository.AddRawPayloadAsync(rawPayload);
    }
}
