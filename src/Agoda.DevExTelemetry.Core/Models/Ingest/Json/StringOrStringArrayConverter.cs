using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agoda.DevExTelemetry.Core.Models.Ingest.Json;

/// <summary>
/// Reads a string, or an array of strings joined with <c>\n</c> (agoda-test-metrics sends Vitest
/// <c>errorMessage</c> as an array).
/// </summary>
public class StringOrStringArrayConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartArray:
                var parts = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                        throw new JsonException($"array items must be strings, not {reader.TokenType}");
                    parts.Add(reader.GetString()!);
                }
                return string.Join("\n", parts);
            default:
                throw new JsonException($"expected a string or an array of strings, not {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
