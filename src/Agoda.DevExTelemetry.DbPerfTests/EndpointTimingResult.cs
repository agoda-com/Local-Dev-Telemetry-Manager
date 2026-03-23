namespace Agoda.DevExTelemetry.DbPerfTests;

public record EndpointTimingResult(
    string Engine,
    string Endpoint,
    int Iterations,
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    DateTime CollectedAtUtc);
