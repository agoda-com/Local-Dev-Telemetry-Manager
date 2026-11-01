using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace Agoda.DevExTelemetry.McpTests.Infrastructure;

/// <summary>
/// Runs the <c>evidence</c> column of an analysis row, for example
/// <c>get_test test_key=ab12cd34:Ns.Cls.T environment=ci from=2026-03-04T12:00:00Z</c>, as a real tool call.
/// Argument values are converted to the types the tool's input schema declares.
/// </summary>
public static class EvidenceCalls
{
    public static async Task<ToolResult> CallEvidenceAsync(this McpClient client, string evidence)
    {
        var (tool, values) = Parse(evidence);
        var tools = await client.ListToolsAsync();
        var target = tools.FirstOrDefault(t => t.Name == tool)
            ?? throw new InvalidOperationException($"Evidence names unknown tool '{tool}': {evidence}");

        var properties = target.JsonSchema.TryGetProperty("properties", out var p) ? p : default;
        var args = new Dictionary<string, object?>();
        foreach (var (name, text) in values)
        {
            if (properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty(name, out var schema))
                throw new InvalidOperationException($"Evidence passes '{name}', which {tool} does not take: {evidence}");
            args[name] = Convert(text, SchemaType(schema));
        }

        return ToolResult.Parse(await client.CallToolAsync(tool, args));
    }

    /// <summary>Splits <c>tool a=1 b="x y"</c> into the tool name and its arguments.</summary>
    public static (string Tool, IReadOnlyList<(string Name, string Value)> Args) Parse(string evidence)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var c in evidence.Trim())
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        if (tokens.Count == 0) throw new InvalidOperationException("Empty evidence");

        var args = tokens.Skip(1).Select(t =>
        {
            var eq = t.IndexOf('=');
            if (eq <= 0) throw new InvalidOperationException($"Evidence argument '{t}' is not name=value");
            return (t[..eq], t[(eq + 1)..]);
        }).ToList();
        return (tokens[0], args);
    }

    private static string SchemaType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type)) return "string";
        if (type.ValueKind == JsonValueKind.String) return type.GetString()!;
        // Nullable parameters are declared as ["integer", "null"].
        return type.EnumerateArray().Select(t => t.GetString()!).First(t => t != "null");
    }

    private static object Convert(string text, string type) => type switch
    {
        "integer" => int.Parse(text, CultureInfo.InvariantCulture),
        "number" => double.Parse(text, CultureInfo.InvariantCulture),
        "boolean" => bool.Parse(text),
        _ => text
    };
}
