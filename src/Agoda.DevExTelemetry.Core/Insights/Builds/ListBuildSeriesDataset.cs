using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary><c>list_build_series</c>: one row per build series (repository, project, metric type, reload type) in the window.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class ListBuildSeriesDataset : IInsightsDataset
{
    private static readonly TableSchema<BuildSeriesRow> Schema = new(
        ("series_id", r => r.SeriesId),
        ("repository", r => r.Repository),
        ("project", r => r.Project),
        ("build_category", r => r.BuildCategory),
        ("metric_type", r => r.MetricType),
        ("reload_type", r => r.ReloadType),
        ("count", r => r.Count),
        ("last_at", r => r.LastAt),
        ("avg_ms", r => Stats.Ms(r.AvgMs)),
        ("p50_ms", r => Stats.Ms(r.P50Ms)),
        ("p95_ms", r => Stats.Ms(r.P95Ms)));

    private readonly IBuildQueries _queries;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public ListBuildSeriesDataset(IBuildQueries queries, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.LIST_BUILD_SERIES;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var filter = new BuildFilter
        {
            MetricType = args.GetString("metric_type"),
            BuildCategory = args.GetString("build_category"),
            ReloadType = args.GetString("reload_type")
        };
        var sort = SortSpec.Parse(args.GetString("sort"), IBuildQueries.SeriesSortColumns, new SortSpec("count", true));
        var offset = Cursor.DecodeOffset(page.Cursor);

        var (rows, total) = await _queries.ListSeriesAsync(scope, filter, sort, offset, page.Limit, cancellationToken);
        return Schema.ToTable(rows, total, Cursor.NextOffset(offset, rows.Count, total)) with
        {
            Window = scope.Window,
            Applied = string.Join(" ", new[] { scope.Describe(), filter.Describe(), $"sort={sort}" }.Where(s => s.Length > 0))
        };
    }
}
