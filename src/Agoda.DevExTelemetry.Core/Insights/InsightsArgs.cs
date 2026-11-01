using System.Globalization;

namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>
/// The arguments of one insights call, keyed by their snake_case names (<c>suite_id</c>, <c>min_runs</c>).
/// MCP tools build this from their typed parameters and the export endpoint builds it from the query
/// string, so a tool call and its export parse the same values the same way.
/// </summary>
public sealed class InsightsArgs
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Sets an argument. Null and blank values are dropped, so "not given" and "empty" mean the same.</summary>
    public InsightsArgs With(string name, object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            bool b => b ? "true" : "false",
            DateTime d => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
            _values.Remove(name);
        else
            _values[name] = text.Trim();

        return this;
    }

    public InsightsArgs Without(string name)
    {
        _values.Remove(name);
        return this;
    }

    public InsightsArgs Clone()
    {
        var copy = new InsightsArgs();
        foreach (var (k, v) in _values) copy._values[k] = v;
        return copy;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? GetString(string name) => _values.TryGetValue(name, out var v) ? v : null;

    public int? GetInt(string name)
    {
        var v = GetString(name);
        if (v == null) return null;
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : throw new InsightsArgumentException(name, $"'{v}' is not a whole number");
    }

    public int GetInt(string name, int defaultValue) => GetInt(name) ?? defaultValue;

    public double? GetDouble(string name)
    {
        var v = GetString(name);
        if (v == null) return null;
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new InsightsArgumentException(name, $"'{v}' is not a number");
    }

    public double GetDouble(string name, double defaultValue) => GetDouble(name) ?? defaultValue;

    public bool? GetBool(string name)
    {
        var v = GetString(name);
        if (v == null) return null;
        return bool.TryParse(v, out var b)
            ? b
            : throw new InsightsArgumentException(name, $"'{v}' is not true or false");
    }

    public DateTime? GetDateTime(string name)
    {
        var v = GetString(name);
        if (v == null) return null;
        return DateTime.TryParse(v, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : throw new InsightsArgumentException(name, $"'{v}' is not an ISO-8601 date or time");
    }

    /// <summary>A comma-separated list, trimmed, empty entries dropped.</summary>
    public IReadOnlyList<string> GetList(string name)
    {
        var v = GetString(name);
        return v == null
            ? []
            : v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>One of <paramref name="allowed"/> (case-insensitive), returned in its canonical spelling.</summary>
    public string? GetChoice(string name, params string[] allowed)
    {
        var v = GetString(name);
        if (v == null) return null;
        var match = allowed.FirstOrDefault(a => string.Equals(a, v, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new InsightsArgumentException(name, $"'{v}' is not one of: {string.Join(", ", allowed)}");
    }

    /// <summary>Renders the arguments as a query string (sorted by name), without a leading '?'.</summary>
    public string ToQueryString(params string[] exclude) =>
        string.Join("&", _values
            .Where(kv => !exclude.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    /// <summary>Renders the arguments as <c>a=1 b=x</c> for summaries and evidence calls.</summary>
    public string ToDisplayString(params string[] exclude) =>
        string.Join(" ", _values
            .Where(kv => !exclude.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Value.Contains(' ') ? $"{kv.Key}=\"{kv.Value}\"" : $"{kv.Key}={kv.Value}"));

    public static InsightsArgs FromPairs(IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        var args = new InsightsArgs();
        foreach (var (k, v) in pairs) args.With(k, v);
        return args;
    }
}
