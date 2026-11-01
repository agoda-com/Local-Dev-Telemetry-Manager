using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// <c>find_regressions kind=suite</c>: suites (repository + project + runner) whose median run duration rose.
/// Samples are all test runs with a total duration, whatever their outcome. A run's duration depends on how many
/// tests ran (a filtered or partial run is shorter, a failed run may stop early), so a shift can come from the
/// run mix rather than from slower tests; check the evidence's counts. See <see cref="IRegressionDetector"/>.
/// </summary>
[RegisterPerRequest(For = typeof(IRegressionDetector))]
public sealed class SuiteRegressionDetector : IRegressionDetector
{
    private readonly TelemetryDbContext _db;

    public SuiteRegressionDetector(TelemetryDbContext db)
    {
        _db = db;
    }

    public string Kind => "suite";

    public async Task<IReadOnlyList<RegressionRow>> FindAsync(RegressionQuery query, CancellationToken cancellationToken)
    {
        var currentFrom = query.CurrentFrom;
        var minSamples = query.MinSamples;

        // Step 1, in SQL: run counts per suite in each window.
        var candidates = await query.Scope.Apply(_db.TestRuns)
            .Where(r => r.TotalDurationMs != null)
            .GroupBy(r => new { r.RepositoryName, r.ProjectName, r.TestRunner })
            .Select(g => new
            {
                g.Key.RepositoryName,
                g.Key.ProjectName,
                g.Key.TestRunner,
                Baseline = g.Sum(r => r.ReceivedAt < currentFrom ? 1 : 0),
                Current = g.Sum(r => r.ReceivedAt >= currentFrom ? 1 : 0)
            })
            .Where(x => x.Baseline >= minSamples && x.Current >= minSamples)
            .ToListAsync(cancellationToken);

        var rows = new List<RegressionRow>();
        var scopeArgs = Evidence.ScopeArgs(query.Scope);

        foreach (var c in candidates)
        {
            // Step 2: narrow (time, duration) projection of this suite's runs.
            var repository = c.RepositoryName;
            var project = c.ProjectName;
            var runner = c.TestRunner;
            var samples = (await query.Scope.Apply(_db.TestRuns)
                    .Where(r => r.TotalDurationMs != null && r.RepositoryName == repository
                        && r.ProjectName == project && r.TestRunner == runner)
                    .Select(r => new { r.ReceivedAt, Duration = r.TotalDurationMs!.Value })
                    .ToListAsync(cancellationToken))
                .Select(s => new DurationSample(DateTime.SpecifyKind(s.ReceivedAt, DateTimeKind.Utc), s.Duration))
                .ToList();

            var comparison = RegressionMath.Compare(samples, query);
            if (comparison == null) continue;

            var suiteId = StableId.Suite(repository, project, runner);
            rows.Add(new RegressionRow(
                Kind, suiteId, repository, project, $"{repository} / {project} / {runner}",
                comparison.BaselineSamples, comparison.CurrentSamples,
                comparison.BeforeP50Ms, comparison.AfterP50Ms, comparison.ShiftStartedOn,
                RegressionMath.Reason(comparison, "runs"),
                Evidence.Call("list_test_runs", scopeArgs.Clone().With("suite_id", suiteId))));
        }

        return rows;
    }
}
