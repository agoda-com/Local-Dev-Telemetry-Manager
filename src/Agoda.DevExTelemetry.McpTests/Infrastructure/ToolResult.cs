using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Agoda.DevExTelemetry.McpTests.Infrastructure;

/// <summary>A parsed insights tool result: summary line, CSV rows and structured content.</summary>
public sealed class ToolResult
{
    private ToolResult(CallToolResult raw, string summary, string body, IReadOnlyList<string> columns,
        IReadOnlyList<CsvRow> rows, JsonElement? meta)
    {
        Raw = raw;
        Summary = summary;
        Body = body;
        Columns = columns;
        Rows = rows;
        Meta = meta;
    }

    public CallToolResult Raw { get; }
    public bool IsError => Raw.IsError == true;
    public string Summary { get; }

    /// <summary>The text of content[1]: CSV, or JSON with format=json.</summary>
    public string Body { get; }

    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<CsvRow> Rows { get; }
    public JsonElement? Meta { get; }

    public int TotalRows => Meta!.Value.GetProperty("total_rows").GetInt32();
    public bool Truncated => Meta!.Value.GetProperty("truncated").GetBoolean();

    public string? NextCursor =>
        Meta!.Value.GetProperty("next_cursor").ValueKind == JsonValueKind.Null
            ? null
            : Meta.Value.GetProperty("next_cursor").GetString();

    public string ExportUrl => Meta!.Value.GetProperty("export_url").GetString()!;

    /// <summary>The error text of a failed call.</summary>
    public string ErrorText => string.Join("\n", Raw.Content.OfType<TextContentBlock>().Select(t => t.Text));

    public static ToolResult Parse(CallToolResult raw)
    {
        var texts = raw.Content.OfType<TextContentBlock>().Select(t => t.Text).ToList();
        JsonElement? meta = raw.StructuredContent.HasValue ? raw.StructuredContent.Value.Clone() : null;
        if (raw.IsError == true || texts.Count < 2)
            return new ToolResult(raw, texts.FirstOrDefault() ?? string.Empty, string.Empty, [], [], meta);

        var body = texts[1];
        if (body.TrimStart().StartsWith('['))
            return new ToolResult(raw, texts[0], body, [], [], meta);

        var (columns, rows) = Csv.Parse(body);
        return new ToolResult(raw, texts[0], body, columns, rows, meta);
    }
}

/// <summary>One CSV row, read by column name.</summary>
public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public CsvRow(IReadOnlyDictionary<string, string> values) => _values = values;

    public IReadOnlyDictionary<string, string> Values => _values;

    public string this[string column] =>
        _values.TryGetValue(column, out var v) ? v : throw new KeyNotFoundException($"No column '{column}'. Columns: {string.Join(",", _values.Keys)}");

    public int Int(string column) => int.Parse(this[column], CultureInfo.InvariantCulture);
    public long Long(string column) => long.Parse(this[column], CultureInfo.InvariantCulture);
    public double Double(string column) => double.Parse(this[column], CultureInfo.InvariantCulture);
    public bool Bool(string column) => bool.Parse(this[column]);
    public string? NullableString(string column) => this[column].Length == 0 ? null : this[column];

    public override string ToString() => string.Join(",", _values.Select(kv => $"{kv.Key}={kv.Value}"));
}

/// <summary>A small RFC 4180 reader for tool output and exports.</summary>
public static class Csv
{
    public static (IReadOnlyList<string> Columns, IReadOnlyList<CsvRow> Rows) Parse(string csv)
    {
        var records = ReadRecords(csv);
        if (records.Count == 0) return ([], []);
        var columns = records[0];
        var rows = records.Skip(1)
            .Select(r => new CsvRow(columns.Select((c, i) => (c, v: i < r.Count ? r[i] : string.Empty))
                .ToDictionary(x => x.c, x => x.v)))
            .ToList();
        return (columns, rows);
    }

    private static List<List<string>> ReadRecords(string csv)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var any = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; any = true; break;
                case ',': record.Add(field.ToString()); field.Clear(); any = true; break;
                case '\r': break;
                case '\n':
                    record.Add(field.ToString()); field.Clear();
                    records.Add(record); record = new List<string>(); any = false;
                    break;
                default: field.Append(c); any = true; break;
            }
        }

        if (any || field.Length > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}

public static class McpClientExtensions
{
    /// <summary>Calls a tool and parses its result. Argument values are passed as given (use C# ints, strings, bools).</summary>
    public static async Task<ToolResult> CallAsync(this McpClient client, string tool, object? args = null)
    {
        var dict = args == null
            ? new Dictionary<string, object?>()
            : args.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(args));
        var raw = await client.CallToolAsync(tool, dict);
        return ToolResult.Parse(raw);
    }

    /// <summary>Follows next_cursor until the last page and returns every row.</summary>
    public static async Task<List<CsvRow>> CallAllPagesAsync(this McpClient client, string tool, object? args = null)
    {
        var dict = args == null
            ? new Dictionary<string, object?>()
            : args.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(args));
        var rows = new List<CsvRow>();
        for (var guard = 0; guard < 10_000; guard++)
        {
            var result = ToolResult.Parse(await client.CallToolAsync(tool, dict));
            if (result.IsError) throw new InvalidOperationException(result.ErrorText);
            rows.AddRange(result.Rows);
            if (result.NextCursor == null) return rows;
            dict["cursor"] = result.NextCursor;
        }
        throw new InvalidOperationException("Cursor never ended");
    }
}

public static class ExportClientExtensions
{
    /// <summary>GETs an export URL (absolute or relative) and parses the CSV.</summary>
    public static async Task<(IReadOnlyList<string> Columns, IReadOnlyList<CsvRow> Rows)> GetCsvAsync(this HttpClient http, string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var abs) ? abs.PathAndQuery : url;
        var response = await http.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{(int)response.StatusCode}: {text}");
        return Csv.Parse(text);
    }
}
