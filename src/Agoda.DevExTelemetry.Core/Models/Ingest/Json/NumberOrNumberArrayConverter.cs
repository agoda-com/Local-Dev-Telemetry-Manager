using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest.Json;

/// <summary>
/// Reads a number, or an array of numbers (Node sends <c>cpuSpeed</c> as one entry per CPU), into
/// a list. A single number reads as a one-item list.
/// </summary>
public class NumberOrNumberArrayConverter : JsonConverter<List<double>>
{
    public override List<double>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return [reader.GetDouble()];
            case JsonTokenType.StartArray:
                var values = new List<double>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.Number)
                        throw new JsonException($"array items must be numbers, not {reader.TokenType}");
                    values.Add(reader.GetDouble());
                }
                return values;
            default:
                throw new JsonException($"expected a number or an array of numbers, not {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, List<double> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteNumberValue(item);
        writer.WriteEndArray();
    }
}
