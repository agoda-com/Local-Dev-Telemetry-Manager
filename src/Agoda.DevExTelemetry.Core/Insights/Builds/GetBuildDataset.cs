using System.Text.Json;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary><c>get_build</c>: one build by id as vertical <c>field, value</c> rows, with ExtraData flattened into <c>extra.*</c> rows.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetBuildDataset : IInsightsDataset
{
    public const int MAX_RAW_EXTRA = 2000;

    private readonly IBuildQueries _queries;

    public GetBuildDataset(IBuildQueries queries)
    {
        _queries = queries;
    }

    public string Name => DatasetNames.GET_BUILD;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var id = args.GetString("id") ?? throw new InsightsArgumentException("id", "is required; get it from get_builds");
        var detail = await _queries.GetAsync(id, cancellationToken)
            ?? throw new InsightsArgumentException("id", $"no build '{id}'; get_builds lists build ids");

        var b = detail.Build;
        var fields = new List<(string Field, object? Value)>
        {
            ("id", b.Id),
            ("received_at", b.ReceivedAt),
            ("repository", b.Repository),
            ("project", b.Project),
            ("build_category", b.BuildCategory),
            ("metric_type", b.MetricType),
            ("reload_type", b.ReloadType),
            ("time_ms", Stats.Ms(b.TimeMs)),
            ("branch", b.Branch),
            ("environment", b.Environment),
            ("platform", b.Platform),
            ("os", b.Os),
            ("cpu_count", b.CpuCount),
            ("tool_version", b.ToolVersion),
            ("debugger_attached", b.DebuggerAttached),
            ("commit_sha", b.CommitSha)
        };
        fields.AddRange(FlattenExtra(detail.ExtraData));

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageRows = fields.Skip(offset).Take(page.Limit).Select(f => new object?[] { f.Field, f.Value }).ToList();
        return new Table(["field", "value"], pageRows, fields.Count, Cursor.NextOffset(offset, pageRows.Count, fields.Count))
        {
            Applied = $"id={id}"
        };
    }

    /// <summary>
    /// The ExtraData JSON as <c>extra.path</c> rows: objects use dots, arrays use <c>[i]</c>. Invalid JSON or a
    /// non-object root becomes one <c>extra</c> row with the raw text cut to 2000 characters.
    /// </summary>
    public static IReadOnlyList<(string Field, object? Value)> FlattenExtra(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData)) return [];
        try
        {
            using var doc = JsonDocument.Parse(extraData);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [Raw(extraData)];
            var rows = new List<(string, object?)>();
            Walk(doc.RootElement, "extra", rows);
            return rows;
        }
        catch (JsonException)
        {
            return [Raw(extraData)];
        }
    }

    private static (string, object?) Raw(string text) =>
        ("extra", text.Length <= MAX_RAW_EXTRA ? text : text[..MAX_RAW_EXTRA]);

    private static void Walk(JsonElement element, string path, List<(string, object?)> rows)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, path + "." + property.Name, rows);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                    Walk(item, $"{path}[{i++}]", rows);
                break;
            case JsonValueKind.String:
                rows.Add((path, element.GetString()));
                break;
            case JsonValueKind.Number:
                rows.Add((path, element.TryGetInt64(out var l) ? l : element.GetDouble()));
                break;
            case JsonValueKind.True:
                rows.Add((path, true));
                break;
            case JsonValueKind.False:
                rows.Add((path, false));
                break;
            default:
                rows.Add((path, null));
                break;
        }
    }
}
