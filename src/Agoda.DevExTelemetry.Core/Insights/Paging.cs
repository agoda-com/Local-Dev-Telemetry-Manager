using System.Globalization;
using System.Text;

namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>
/// What page to return. <see cref="Limit"/> is already clamped by the caller (tool: 1..Mcp:MaxRows,
/// export: its own page size), so datasets use it as given.
/// </summary>
public sealed record PageRequest(int Limit, string? Cursor);

/// <summary>
/// Opaque cursors. Aggregated, sorted datasets use offset cursors; raw-row datasets that are
/// ordered newest first use keyset cursors on (timestamp, id).
/// </summary>
public static class Cursor
{
    private const string OFFSET_PREFIX = "o:";
    private const string KEYSET_PREFIX = "k:";

    public static string EncodeOffset(int offset) => Encode(OFFSET_PREFIX + offset.ToString(CultureInfo.InvariantCulture));

    /// <summary>The offset in the cursor, or 0 when there is no cursor.</summary>
    public static int DecodeOffset(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return 0;
        var text = Decode(cursor);
        if (text.StartsWith(OFFSET_PREFIX, StringComparison.Ordinal) &&
            int.TryParse(text[OFFSET_PREFIX.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) &&
            offset >= 0)
            return offset;
        throw Invalid();
    }

    /// <summary>The offset cursor for the page after this one, or null when this page is the last.</summary>
    public static string? NextOffset(int offset, int returned, int totalRows) =>
        offset + returned < totalRows ? EncodeOffset(offset + returned) : null;

    public static string EncodeKeyset(DateTime timestamp, string id) =>
        Encode(KEYSET_PREFIX + timestamp.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + id);

    /// <summary>The (timestamp, id) of the last row of the previous page, or null when there is no cursor.</summary>
    public static (DateTime Timestamp, string Id)? DecodeKeyset(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        var text = Decode(cursor);
        if (!text.StartsWith(KEYSET_PREFIX, StringComparison.Ordinal)) throw Invalid();
        var body = text[KEYSET_PREFIX.Length..];
        var bar = body.IndexOf('|');
        if (bar <= 0 || !long.TryParse(body[..bar], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            throw Invalid();
        return (new DateTime(ticks, DateTimeKind.Utc), body[(bar + 1)..]);
    }

    private static string Encode(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Decode(string cursor)
    {
        try
        {
            var b64 = cursor.Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
        catch (FormatException)
        {
            throw Invalid();
        }
    }

    private static InsightsArgumentException Invalid() =>
        new("cursor", "is not a cursor from this tool; pass next_cursor from the previous result unchanged");
}

/// <summary>A parsed <c>sort</c> argument: <c>total_ms desc</c>, <c>total_ms asc</c>, <c>-total_ms</c> or <c>total_ms</c> (desc).</summary>
public sealed record SortSpec(string Column, bool Descending)
{
    /// <summary>
    /// Parses <c>sort</c>. Numeric sorts default to descending, because the agent almost always wants the
    /// biggest first. <paramref name="allowed"/> are the sortable columns.
    /// </summary>
    public static SortSpec Parse(string? sort, IReadOnlyCollection<string> allowed, SortSpec defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return defaultSort;

        var text = sort.Trim();
        var descending = true;
        if (text.StartsWith('-'))
        {
            text = text[1..];
        }
        else
        {
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                text = parts[0];
                descending = parts[1].ToLowerInvariant() switch
                {
                    "desc" => true,
                    "asc" => false,
                    _ => throw new InsightsArgumentException("sort", $"direction '{parts[1]}' must be asc or desc")
                };
            }
            else if (parts.Length > 2)
            {
                throw new InsightsArgumentException("sort", "use 'column', 'column desc', 'column asc' or '-column'");
            }
        }

        var column = allowed.FirstOrDefault(a => string.Equals(a, text, StringComparison.OrdinalIgnoreCase))
            ?? throw new InsightsArgumentException("sort", $"'{text}' is not sortable; use one of: {string.Join(", ", allowed)}");
        return new SortSpec(column, descending);
    }

    public override string ToString() => Descending ? $"{Column} desc" : $"{Column} asc";
}
