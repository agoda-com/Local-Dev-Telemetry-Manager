namespace Agoda.DevExTelemetry.Core.Services;

public interface IStatusNormalizer
{
    string Normalize(string? rawStatus);
}
