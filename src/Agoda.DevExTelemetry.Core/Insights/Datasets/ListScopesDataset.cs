using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights.Datasets;

/// <summary>
/// <c>list_scopes</c>: every repository, project, branch, platform, test runner and metric type in the
/// store, one row per value. Wraps <see cref="ITelemetryRepository.GetAvailableFiltersAsync"/>, so it
/// matches the dashboard's filter lists.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class ListScopesDataset : IInsightsDataset
{
    private static readonly TableSchema<(string Kind, string Value)> Schema = new(
        ("kind", r => r.Kind),
        ("value", r => r.Value));

    private readonly ITelemetryRepository _repository;

    public ListScopesDataset(ITelemetryRepository repository)
    {
        _repository = repository;
    }

    public string Name => DatasetNames.LIST_SCOPES;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var kind = args.GetChoice("kind", "repository", "project", "branch", "platform", "test_runner", "metric_type");
        var filters = await _repository.GetAvailableFiltersAsync();

        var rows = filters.Repositories.Select(v => ("repository", v))
            .Concat(filters.Projects.Select(v => ("project", v)))
            .Concat(filters.Branches.Select(v => ("branch", v)))
            .Concat(filters.Platforms.Select(v => ("platform", v)))
            .Concat(filters.TestRunners.Select(v => ("test_runner", v)))
            .Concat(filters.MetricTypes.Select(v => ("metric_type", v)))
            .Where(r => kind == null || r.Item1 == kind)
            .ToList();

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageRows = rows.Skip(offset).Take(page.Limit).ToList();
        return Schema.ToTable(pageRows, rows.Count, Cursor.NextOffset(offset, pageRows.Count, rows.Count)) with
        {
            Applied = kind == null ? null : $"kind={kind}"
        };
    }
}
