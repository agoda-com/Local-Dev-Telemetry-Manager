using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class IngestService : IIngestService
{
    private readonly TelemetryDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IngestService> _logger;

    public IngestService(TelemetryDbContext db, IConfiguration configuration, ILogger<IngestService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task IngestBuildMetricAsync(BuildMetric metric)
    {
        metric.ReceivedAt = DateTime.UtcNow;

        if (await _db.BuildMetrics.AnyAsync(bm => bm.Id == metric.Id))
        {
            _logger.LogInformation("Ignoring duplicate BuildMetric with Id={Id}", metric.Id);
            return;
        }

        _db.BuildMetrics.Add(metric);
        await _db.SaveChangesAsync();
    }

    public async Task IngestTestRunAsync(TestRun run, IEnumerable<TestCase> testCases)
    {
        run.ReceivedAt = DateTime.UtcNow;

        if (await _db.TestRuns.AnyAsync(tr => tr.Id == run.Id))
        {
            _logger.LogInformation("Ignoring duplicate TestRun with Id={Id}", run.Id);
            return;
        }

        _db.TestRuns.Add(run);
        await _db.TestCases.AddRangeAsync(testCases);
        await _db.SaveChangesAsync();
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

        _db.RawPayloads.Add(rawPayload);
        await _db.SaveChangesAsync();
    }
}
