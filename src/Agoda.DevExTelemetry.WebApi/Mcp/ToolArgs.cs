using Agoda.DevExTelemetry.Core.Insights;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

/// <summary>
/// Shared descriptions for the common parameters, so every tool describes them the same way,
/// and a helper that turns them into <see cref="InsightsArgs"/>.
/// </summary>
public static class ToolArgs
{
    public const string REPOSITORY = "Repository name (RepositoryName), as listed by list_scopes kind=repository.";
    public const string PROJECT = "Project name (ProjectName), as listed by list_scopes kind=project.";
    public const string BRANCH = "Branch name.";
    public const string ENVIRONMENT = "local | ci | all. Inferred from debugger flag, hostname and run ids, so it can be wrong.";
    public const string DAYS = "Window length in days ending now. Default 14, capped at the retention period. Ignored when 'from' is given.";
    public const string FROM = "Window start, ISO-8601 UTC. Overrides 'days'.";
    public const string TO = "Window end, ISO-8601 UTC. Default now.";
    public const string LIMIT = "Rows to return. Default 100, max 500 (Mcp:MaxRows). Use export_url for more.";
    public const string CURSOR = "next_cursor from the previous result, to get the next page. Pass the other arguments unchanged.";
    public const string FORMAT = "csv (default) or json.";
    public const string SORT = "Column to sort by: 'col', 'col desc', 'col asc' or '-col'. Numeric sorts default to descending.";

    /// <summary>The common scope parameters as <see cref="InsightsArgs"/>; add the tool's own with <c>.With(...)</c>.</summary>
    public static InsightsArgs Scope(
        string? repository,
        string? project,
        string? branch,
        string? environment,
        int? days,
        string? from,
        string? to) =>
        new InsightsArgs()
            .With("repository", repository)
            .With("project", project)
            .With("branch", branch)
            .With("environment", environment)
            .With("days", days)
            .With("from", from)
            .With("to", to);
}
