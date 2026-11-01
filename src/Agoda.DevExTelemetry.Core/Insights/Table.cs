namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>
/// One page of a dataset: named columns and rows of plain values. Allowed cell values are
/// <c>null</c>, <c>string</c>, <c>bool</c>, <c>int</c>, <c>long</c>, <c>double</c> and UTC <c>DateTime</c>;
/// <see cref="Output.TableFormatter"/> renders them as the issue's output format requires.
/// Durations go in as whole milliseconds (<c>long</c>), rates as <c>double</c>.
/// </summary>
public sealed record Table(
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows,
    int TotalRows,
    string? NextCursor)
{
    /// <summary>The window the rows cover, or null when the dataset has no window (for example <c>list_scopes</c>).</summary>
    public InsightsWindow? Window { get; init; }

    /// <summary>
    /// What was applied, for the summary line: the scope plus dataset filters and any thresholds,
    /// for example <c>environment=ci min_runs=5</c>.
    /// </summary>
    public string? Applied { get; init; }

    /// <summary>Caveats for the summary line, for example the local-data warning on <c>find_flaky_tests</c>.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    public static Table Empty(IReadOnlyList<string> columns) => new(columns, [], 0, null);
}

/// <summary>
/// The column list of a row type, in output order. Declare one per dataset as a static field and
/// build pages with <see cref="ToTable"/>:
/// <code>
/// static readonly TableSchema&lt;SuiteRow&gt; Schema = new(
///     ("suite_id", r =&gt; r.SuiteId),
///     ("runs", r =&gt; r.Runs));
/// </code>
/// </summary>
public sealed class TableSchema<T>
{
    private readonly (string Name, Func<T, object?> Value)[] _columns;

    public TableSchema(params (string Name, Func<T, object?> Value)[] columns)
    {
        _columns = columns;
        Columns = columns.Select(c => c.Name).ToArray();
    }

    public IReadOnlyList<string> Columns { get; }

    public Table ToTable(IEnumerable<T> rows, int totalRows, string? nextCursor) =>
        new(Columns, rows.Select(r => _columns.Select(c => c.Value(r)).ToArray()).ToList(), totalRows, nextCursor);
}
