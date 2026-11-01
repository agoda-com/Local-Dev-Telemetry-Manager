using Agoda.IoC.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// <c>get_overview</c>: the top rows of every scope-wide analysis tool, in one call. Each section runs its
/// analysis dataset with the same scope arguments and default thresholds, so a row here is the same row the
/// section's tool returns. <c>explain_build_time</c> needs a series, so it is not a section; the overview
/// points at it from the slow-build rows instead.
/// </summary>
[RegisterPerRequest(For = typeof(IInsightsDataset))]
public sealed class OverviewDataset : IInsightsDataset
{
    public const int DEFAULT_TOP = 5;

    /// <summary>The sections, in output order.</summary>
    public static readonly string[] Sections =
    [
        DatasetNames.FIND_FAILING_TESTS,
        DatasetNames.FIND_FLAKY_TESTS,
        DatasetNames.FIND_SLOW_TESTS,
        DatasetNames.FIND_SLOW_BUILDS,
        DatasetNames.FIND_REGRESSIONS
    ];

    /// <summary>The column holding each section's subject id, in preference order.</summary>
    private static readonly string[] SubjectColumns = ["test_key", "series_id", "subject_id", "suite_id"];

    private static readonly string[] Columns = ["section", "rank", "subject_id", "repository", "project", Evidence.REASON, Evidence.EVIDENCE];

    // Resolved at query time: the dataset collection includes this dataset, so injecting it would be circular.
    private readonly IServiceProvider _services;

    public OverviewDataset(IServiceProvider services)
    {
        _services = services;
    }

    public string Name => DatasetNames.GET_OVERVIEW;

    public async Task<Table> QueryAsync(InsightsArgs args, PageRequest page, CancellationToken cancellationToken)
    {
        var top = args.GetInt("top", DEFAULT_TOP);
        if (top < 1) throw new InsightsArgumentException("top", "must be at least 1");

        // Each section takes only the common scope; per-section thresholds stay at their defaults.
        var sectionArgs = new InsightsArgs();
        foreach (var name in InsightsScope.ParameterNames)
            sectionArgs.With(name, args.GetString(name));
        // find_flaky_tests defaults to CI on its own; don't override that unless the caller chose an environment.

        var rows = new List<object?[]>();
        var notes = new List<string>();
        InsightsWindow? window = null;

        var datasets = _services.GetServices<IInsightsDataset>()
            .Where(d => d.Name != Name)
            .ToDictionary(d => d.Name, StringComparer.Ordinal);

        foreach (var section in Sections)
        {
            if (!datasets.TryGetValue(section, out var dataset)) continue;

            var table = await dataset.QueryAsync(sectionArgs.Clone(), new PageRequest(top, null), cancellationToken);
            window ??= table.Window;
            notes.AddRange(table.Notes.Select(n => $"{section}: {n}"));
            if (table.TotalRows > table.Rows.Count)
                notes.Add($"{section}: showing {table.Rows.Count} of {table.TotalRows}; call {section} for the rest");

            var subjectIndex = SubjectColumns.Select(c => IndexOf(table, c)).FirstOrDefault(i => i >= 0, -1);
            var repositoryIndex = IndexOf(table, "repository");
            var projectIndex = IndexOf(table, "project");
            var reasonIndex = IndexOf(table, Evidence.REASON);
            var evidenceIndex = IndexOf(table, Evidence.EVIDENCE);

            for (var i = 0; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                rows.Add([
                    section,
                    i + 1,
                    subjectIndex >= 0 ? row[subjectIndex] : null,
                    repositoryIndex >= 0 ? row[repositoryIndex] : null,
                    projectIndex >= 0 ? row[projectIndex] : null,
                    reasonIndex >= 0 ? row[reasonIndex] : null,
                    evidenceIndex >= 0 ? row[evidenceIndex] : null
                ]);
            }
        }

        var offset = Cursor.DecodeOffset(page.Cursor);
        var pageRows = rows.Skip(offset).Take(page.Limit).ToList();
        return new Table(Columns, pageRows, rows.Count, Cursor.NextOffset(offset, pageRows.Count, rows.Count))
        {
            Window = window,
            Applied = $"top={top}" + (sectionArgs.Values.Count > 0 ? " " + sectionArgs.ToDisplayString() : string.Empty),
            Notes = notes
        };
    }

    private static int IndexOf(Table table, string column)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (table.Columns[i] == column) return i;
        return -1;
    }
}
