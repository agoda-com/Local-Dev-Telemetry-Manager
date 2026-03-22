using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class IngestService : IIngestService
{
    private readonly TelemetryDbContext _db;
    private readonly IConfiguration _configuration;

    public IngestService(TelemetryDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task IngestBuildMetricAsync(BuildMetric metric)
    {
        metric.ReceivedAt = DateTime.UtcNow;
        _db.BuildMetrics.Add(metric);
        await _db.SaveChangesAsync();
    }

    public async Task IngestTestRunAsync(TestRun run, IEnumerable<TestCase> testCases)
    {
        run.ReceivedAt = DateTime.UtcNow;
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
