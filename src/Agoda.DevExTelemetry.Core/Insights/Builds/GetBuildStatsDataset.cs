using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary><c>get_build_stats</c>: build timing statistics grouped by any combination of safe dimensions.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetBuildStatsDataset : IInsightsDataset
{
    private static readonly string[] StatColumns = ["count", "avg_ms", "p50_ms", "p95_ms", "max_ms", "total_ms"];

    private readonly IBuildQueries _queries;
    private readonly IBuildSeriesCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public GetBuildStatsDataset(IBuildQueries queries, IBuildSeriesCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.GET_BUILD_STATS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var groupBy = ParseGroupBy(args);
        var filter = await BuildFilterArgs.ReadAsync(args, _catalog, cancellationToken);

        var sortable = groupBy.Concat(StatColumns).ToList();
        var sort = args.Has("sort") ? SortSpec.Parse(args.GetString("sort"), sortable, new SortSpec(groupBy[0], false)) : null;
        var offset = Cursor.DecodeOffset(page.Cursor);

        var (rows, total) = await _queries.GetStatsAsync(scope, filter, groupBy, sort, offset, page.Limit, cancellationToken);

        var columns = new List<(string Name, Func<BuildStatsRow, object?> Value)>();
        for (var i = 0; i < groupBy.Count; i++)
        {
            var index = i;
            columns.Add((groupBy[i], r => r.Values[index]));
        }
        columns.Add(("count", r => r.Count));
        columns.Add(("avg_ms", r => Stats.Ms(r.AvgMs)));
        columns.Add(("p50_ms", r => Stats.Ms(r.P50Ms)));
        columns.Add(("p95_ms", r => Stats.Ms(r.P95Ms)));
        columns.Add(("max_ms", r => Stats.Ms(r.MaxMs)));
        columns.Add(("total_ms", r => Stats.Ms(r.TotalMs)));

        var schema = new TableSchema<BuildStatsRow>(columns.ToArray());
        var applied = new[] { scope.Describe(), $"group_by={string.Join(",", groupBy)}", filter.Describe(), sort == null ? "" : $"sort={sort}" };
        return schema.ToTable(rows, total, Cursor.NextOffset(offset, rows.Count, total)) with
        {
            Window = scope.Window,
            Applied = string.Join(" ", applied.Where(s => s.Length > 0))
        };
    }

    private static List<string> ParseGroupBy(InsightsArgs args)
    {
        var requested = args.GetList("group_by");
        if (requested.Count == 0)
            throw new InsightsArgumentException("group_by", $"is required; a comma list of: {string.Join(", ", IBuildQueries.StatsGroupColumns)}");

        var result = new List<string>();
        foreach (var name in requested)
        {
            var match = IBuildQueries.StatsGroupColumns.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InsightsArgumentException("group_by", $"'{name}' is not a group column; use any of: {string.Join(", ", IBuildQueries.StatsGroupColumns)}");
            if (!result.Contains(match)) result.Add(match);
        }
        return result;
    }
}
