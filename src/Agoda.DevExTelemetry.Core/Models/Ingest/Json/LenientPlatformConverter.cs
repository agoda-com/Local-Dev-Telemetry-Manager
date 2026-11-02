using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest.Json;

/// <summary>
/// Reads <c>platform</c> as either the .NET <see cref="PlatformID"/> number (Agoda.Builds.Metrics,
/// Agoda.Tests.Metrics) or the Node <c>os.type()</c> string (devfeedback-js, agoda-test-metrics).
/// </summary>
public class LenientPlatformConverter : JsonConverter<PlatformID>
{
    public override PlatformID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (!reader.TryGetInt32(out var number))
                    throw new JsonException("platform is not an integer");
                return (PlatformID)number;
            case JsonTokenType.String:
                return FromOsType(reader.GetString());
            default:
                throw new JsonException($"platform must be a number or a string, not {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, PlatformID value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((int)value);

    private static PlatformID FromOsType(string? osType) => osType switch
    {
        "Linux" => PlatformID.Unix,
        "Darwin" => PlatformID.MacOSX,
        "Windows_NT" => PlatformID.Win32NT,
        _ => PlatformID.Other
    };
}
