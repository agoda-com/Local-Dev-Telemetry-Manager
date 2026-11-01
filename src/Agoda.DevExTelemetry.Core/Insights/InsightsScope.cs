using System.Globalization;
using Agoda.DevExTelemetry.Core.Models.Entities;

namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>The time window a call covers, both ends UTC and inclusive.</summary>
public sealed record InsightsWindow(DateTime From, DateTime To)
{
    public override string ToString() =>
        $"{From.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}..{To.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}";
}

/// <summary>
/// The common parameters every list tool takes: <c>repository</c>, <c>project</c>, <c>branch</c>,
/// <c>environment</c> (local | ci | all), and <c>days</c> (default 14, capped at retention) or <c>from</c>/<c>to</c>.
/// Same semantics as the dashboard's <c>FilterParams</c>: repository matches <c>RepositoryName</c>,
/// project matches <c>ProjectName</c>.
/// </summary>
public sealed record InsightsScope(
    string? Repository,
    string? Project,
    string? Branch,
    string Environment,
    InsightsWindow Window)
{
    public const int DEFAULT_DAYS = 14;

    public const string ENV_ALL = "all";
    public const string ENV_LOCAL = "local";
    public const string ENV_CI = "ci";

    /// <summary>The names of the common parameters, so datasets can tell them apart from their own.</summary>
    public static readonly string[] ParameterNames =
        ["repository", "project", "branch", "environment", "days", "from", "to"];

    /// <summary>The stored <c>ExecutionEnvironment</c> value for the chosen environment, or null for all.</summary>
    public string? StoredEnvironment => Environment switch
    {
        ENV_LOCAL => "Local",
        ENV_CI => "CI",
        _ => null
    };

    /// <summary>
    /// Reads the common parameters. <c>from</c>/<c>to</c> win over <c>days</c>; <c>from</c> is never earlier than
    /// the retention period allows and <c>to</c> defaults to now.
    /// </summary>
    public static InsightsScope Resolve(
        InsightsArgs args,
        DateTime utcNow,
        int retentionDays,
        string defaultEnvironment = ENV_ALL,
        int defaultDays = DEFAULT_DAYS)
    {
        var environment = args.GetChoice("environment", ENV_LOCAL, ENV_CI, ENV_ALL) ?? defaultEnvironment;

        var earliest = utcNow.AddDays(-retentionDays);
        var to = args.GetDateTime("to") ?? utcNow;
        DateTime from;
        var explicitFrom = args.GetDateTime("from");
        if (explicitFrom.HasValue)
        {
            from = explicitFrom.Value;
        }
        else
        {
            var days = args.GetInt("days") ?? defaultDays;
            if (days <= 0)
                throw new InsightsArgumentException("days", "must be at least 1");
            from = to.AddDays(-Math.Min(days, retentionDays));
        }

        if (from < earliest) from = earliest;
        if (from > to)
            throw new InsightsArgumentException("from", "must be before 'to'");

        return new InsightsScope(
            args.GetString("repository"),
            args.GetString("project"),
            args.GetString("branch"),
            environment,
            new InsightsWindow(from, to));
    }

    public IQueryable<TestRun> Apply(IQueryable<TestRun> runs)
    {
        runs = runs.Where(r => r.ReceivedAt >= Window.From && r.ReceivedAt <= Window.To);
        var env = StoredEnvironment;
        if (env != null) runs = runs.Where(r => r.ExecutionEnvironment == env);
        if (Repository != null) runs = runs.Where(r => r.RepositoryName == Repository);
        if (Project != null) runs = runs.Where(r => r.ProjectName == Project);
        if (Branch != null) runs = runs.Where(r => r.Branch == Branch);
        return runs;
    }

    public IQueryable<BuildMetric> Apply(IQueryable<BuildMetric> metrics)
    {
        metrics = metrics.Where(m => m.ReceivedAt >= Window.From && m.ReceivedAt <= Window.To);
        var env = StoredEnvironment;
        if (env != null) metrics = metrics.Where(m => m.ExecutionEnvironment == env);
        if (Repository != null) metrics = metrics.Where(m => m.RepositoryName == Repository);
        if (Project != null) metrics = metrics.Where(m => m.ProjectName == Project);
        if (Branch != null) metrics = metrics.Where(m => m.Branch == Branch);
        return metrics;
    }

    /// <summary>Test cases whose run is in scope.</summary>
    public IQueryable<TestCase> Apply(IQueryable<TestCase> cases)
    {
        cases = cases.Where(c => c.TestRun.ReceivedAt >= Window.From && c.TestRun.ReceivedAt <= Window.To);
        var env = StoredEnvironment;
        if (env != null) cases = cases.Where(c => c.TestRun.ExecutionEnvironment == env);
        if (Repository != null) cases = cases.Where(c => c.TestRun.RepositoryName == Repository);
        if (Project != null) cases = cases.Where(c => c.TestRun.ProjectName == Project);
        if (Branch != null) cases = cases.Where(c => c.TestRun.Branch == Branch);
        return cases;
    }

    /// <summary>The scope in summary form, for example <c>repository=web-app environment=ci</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Repository != null) parts.Add($"repository={Repository}");
        if (Project != null) parts.Add($"project={Project}");
        if (Branch != null) parts.Add($"branch={Branch}");
        parts.Add($"environment={Environment}");
        return string.Join(" ", parts);
    }
}
