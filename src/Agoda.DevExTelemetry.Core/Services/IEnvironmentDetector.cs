namespace Agoda.DevExTelemetry.Core.Services;

public interface IEnvironmentDetector
{
    string Detect(string? hostname, bool isDebuggerAttached, string? platform, string? runId);
}
