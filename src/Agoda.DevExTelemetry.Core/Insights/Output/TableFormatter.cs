using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Agoda.DevExTelemetry.Core.Insights.Output;

/// <summary>
/// Renders a <see cref="Table"/> as CSV (RFC 4180) or JSON. Timestamps are ISO-8601 UTC, numbers use the
/// invariant culture, booleans are <c>true</c>/<c>false</c> and null is an empty CSV field.
/// </summary>
public static class TableFormatter
{
    public const string CSV = "csv";
    public const string JSON = "json";

    public static string ToCsv(Table table)
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb, CultureInfo.InvariantCulture);
        WriteCsvHeader(writer, table.Columns);
        foreach (var row in table.Rows) WriteCsvRow(writer, row);
        return sb.ToString();
    }

    public static void WriteCsvHeader(TextWriter writer, IReadOnlyList<string> columns)
    {
        writer.Write(string.Join(",", columns.Select(Quote)));
        writer.Write("\r\n");
    }

    public static void WriteCsvRow(TextWriter writer, object?[] row)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (i > 0) writer.Write(',');
            writer.Write(Quote(FormatValue(row[i])));
        }
        writer.Write("\r\n");
    }

    /// <summary>A JSON array of objects keyed by column name.</summary>
    public static string ToJson(Table table)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray();
            foreach (var row in table.Rows)
            {
                json.WriteStartObject();
                for (var i = 0; i < table.Columns.Count; i++)
                {
                    json.WritePropertyName(table.Columns[i]);
                    WriteJsonValue(json, row[i]);
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        DateTime d => FormatTimestamp(d),
        double d => FormatDouble(d),
        float f => FormatDouble(f),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    public static string FormatTimestamp(DateTime d)
    {
        var utc = d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : d;
        return utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double d) =>
        double.IsFinite(d) ? Math.Round(d, 4).ToString("0.####", CultureInfo.InvariantCulture) : string.Empty;

    private static string Quote(string field) =>
        field.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;

    private static void WriteJsonValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null: json.WriteNullValue(); break;
            case bool b: json.WriteBooleanValue(b); break;
            case int i: json.WriteNumberValue(i); break;
            case long l: json.WriteNumberValue(l); break;
            case double d when double.IsFinite(d): json.WriteNumberValue(Math.Round(d, 4)); break;
            case double: json.WriteNullValue(); break;
            case DateTime d: json.WriteStringValue(FormatTimestamp(d)); break;
            default: json.WriteStringValue(FormatValue(value)); break;
        }
    }
}
