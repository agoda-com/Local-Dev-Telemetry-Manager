using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest.Json;

/// <summary>
/// Reads a point in time sent as epoch milliseconds (a JSON number) or an ISO-8601 string into
/// epoch milliseconds. An ISO string without an offset is taken as UTC.
/// Use it on <c>long?</c> to accept a JSON null as well.
/// </summary>
public class EpochOrIsoConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var epochMs))
                    return epochMs;
                return (long)Math.Round(reader.GetDouble());
            case JsonTokenType.String:
                var text = reader.GetString();
                if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var parsed))
                    throw new JsonException($"'{text}' is not an ISO-8601 time");
                return parsed.ToUnixTimeMilliseconds();
            default:
                throw new JsonException($"expected epoch ms or an ISO-8601 string, not {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
