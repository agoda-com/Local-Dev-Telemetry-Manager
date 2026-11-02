using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest.Json;

/// <summary>
/// Reads a value sent as a JSON number (devfeedback-js, agoda-test-metrics) or a string
/// (Agoda.Builds.Metrics) into a string. A number keeps its JSON text, so <c>1234.5</c> reads as "1234.5".
/// </summary>
public class NumberOrStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.HasValueSequence
                    ? Encoding.UTF8.GetString(reader.ValueSequence)
                    : Encoding.UTF8.GetString(reader.ValueSpan);
            default:
                throw new JsonException($"expected a number or a string, not {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
