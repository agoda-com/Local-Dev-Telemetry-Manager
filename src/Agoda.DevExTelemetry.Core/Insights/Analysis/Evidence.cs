namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// The <c>evidence</c> column of analysis rows: a data tool call the agent can run to see the rows behind
/// the <c>reason</c>, for example <c>get_test test_key=ab12cd34:Ns.Cls.Test environment=ci days=14</c>.
/// </summary>
public static class Evidence
{
    /// <summary>Common columns every analysis row ends with.</summary>
    public const string REASON = "reason";
    public const string EVIDENCE = "evidence";

    public static string Call(string tool, InsightsArgs args) =>
        args.Values.Count == 0 ? tool : tool + " " + args.ToDisplayString(DatasetNames.PagingArgs);

    /// <summary>
    /// The scope arguments (repository, project, branch, environment) of <paramref name="scope"/> plus an explicit
    /// <c>from</c>/<c>to</c> for its window, so the evidence call covers exactly the rows the analysis saw.
    /// </summary>
    public static InsightsArgs ScopeArgs(InsightsScope scope) =>
        new InsightsArgs()
            .With("repository", scope.Repository)
            .With("project", scope.Project)
            .With("branch", scope.Branch)
            .With("environment", scope.Environment == InsightsScope.ENV_ALL ? null : scope.Environment)
            .With("from", scope.Window.From)
            .With("to", scope.Window.To);
}
