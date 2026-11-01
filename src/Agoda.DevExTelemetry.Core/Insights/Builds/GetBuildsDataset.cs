using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary><c>get_builds</c>: raw build rows, newest first, with a keyset cursor.</summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class GetBuildsDataset : IInsightsDataset
{
    private static readonly TableSchema<BuildRow> Schema = new(
        ("id", r => r.Id),
        ("received_at", r => r.ReceivedAt),
        ("repository", r => r.Repository),
        ("project", r => r.Project),
        ("build_category", r => r.BuildCategory),
        ("metric_type", r => r.MetricType),
        ("reload_type", r => r.ReloadType),
        ("time_ms", r => Stats.Ms(r.TimeMs)),
        ("branch", r => r.Branch),
        ("environment", r => r.Environment),
        ("platform", r => r.Platform),
        ("os", r => r.Os),
        ("cpu_count", r => r.CpuCount),
        ("tool_version", r => r.ToolVersion),
        ("debugger_attached", r => r.DebuggerAttached),
        ("commit_sha", r => r.CommitSha));

    private readonly IBuildQueries _queries;
    private readonly IBuildSeriesCatalog _catalog;
    private readonly TimeProvider _time;
    private readonly IInsightsOptions _options;

    public GetBuildsDataset(IBuildQueries queries, IBuildSeriesCatalog catalog, TimeProvider time, IInsightsOptions options)
    {
        _queries = queries;
        _catalog = catalog;
        _time = time;
        _options = options;
    }

    public string Name => DatasetNames.GET_BUILDS;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var scope = InsightsScope.Resolve(args, _time.GetUtcNow().UtcDateTime, _options.RetentionDays);
        var filter = await BuildFilterArgs.ReadAsync(args, _catalog, cancellationToken);
        var after = Cursor.DecodeKeyset(page.Cursor);

        var (rows, total, hasMore) = await _queries.GetBuildsAsync(
            scope, filter, after.HasValue ? (after.Value.Timestamp, after.Value.Id) : null, page.Limit, cancellationToken);

        var next = hasMore && rows.Count > 0 ? Cursor.EncodeKeyset(rows[^1].ReceivedAt, rows[^1].Id) : null;
        return Schema.ToTable(rows, total, next) with
        {
            Window = scope.Window,
            Applied = string.Join(" ", new[] { scope.Describe(), filter.Describe() }.Where(s => s.Length > 0))
        };
    }
}

/// <summary>Reads the shared build filters, resolving <c>series_id</c>.</summary>
public static class BuildFilterArgs
{
    public static async Task<BuildFilter> ReadAsync(InsightsArgs args, IBuildSeriesCatalog catalog, CancellationToken cancellationToken)
    {
        var seriesId = args.GetString("series_id");
        var series = seriesId == null ? null : await catalog.ResolveAsync(seriesId, cancellationToken);
        return BuildFilter.FromArgs(args, series);
    }
}
