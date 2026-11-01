using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Insights.Tests;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Analysis;

/// <summary>
/// <c>find_regressions kind=test</c>: tests whose median duration rose. Groups are test keys (suite + name key).
/// Samples are test executions with a duration that <b>Passed</b>. Failed executions are left out on purpose:
/// failed tests often abort early or time out, which distorts timing in either direction.
/// See <see cref="IRegressionDetector"/> for the flagging rules.
/// </summary>
[RegisterPerRequest(For = typeof(IRegressionDetector))]
public sealed class TestRegressionDetector : IRegressionDetector
{
    private const string PASSED = "Passed";
    private const int KEY_CHUNK = 400;

    private readonly TelemetryDbContext _db;

    public TestRegressionDetector(TelemetryDbContext db)
    {
        _db = db;
    }

    public string Kind => "test";

    public async Task<IReadOnlyList<RegressionRow>> FindAsync(RegressionQuery query, CancellationToken cancellationToken)
    {
        var currentFrom = query.CurrentFrom;
        var minSamples = query.MinSamples;

        // Step 1, in SQL: passing samples per test in each window; keep only tests with enough in both.
        var candidates = await query.Scope.Apply(_db.TestCases).Facts()
            .Where(f => f.Status == PASSED && f.DurationMs != null)
            .GroupBy(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey })
            .Select(g => new
            {
                g.Key.Repository,
                g.Key.Project,
                g.Key.TestRunner,
                g.Key.NameKey,
                Baseline = g.Sum(f => f.ReceivedAt < currentFrom ? 1 : 0),
                Current = g.Sum(f => f.ReceivedAt >= currentFrom ? 1 : 0)
            })
            .Where(x => x.Baseline >= minSamples && x.Current >= minSamples)
            .ToListAsync(cancellationToken);

        var rows = new List<RegressionRow>();
        var scopeArgs = Evidence.ScopeArgs(query.Scope);

        // Step 2: a narrow (key, time, duration) projection for the surviving tests only, in chunks of name keys. Each chunk is
        // streamed in (test, time) order and folded one test at a time: a test's durations are compared and dropped before the
        // next test's are read, so only one test's samples are ever in memory and only result rows accumulate.
        foreach (var chunk in candidates.Chunk(KEY_CHUNK))
        {
            var nameKeys = chunk.Select(c => c.NameKey).Distinct().ToList();
            var wanted = chunk.Select(c => (c.Repository, c.Project, c.TestRunner, c.NameKey)).ToHashSet();
            var stream = query.Scope.Apply(_db.TestCases).Facts()
                .Where(f => f.Status == PASSED && f.DurationMs != null && nameKeys.Contains(f.NameKey))
                .OrderBy(f => f.Repository).ThenBy(f => f.Project).ThenBy(f => f.TestRunner).ThenBy(f => f.NameKey)
                .ThenBy(f => f.ReceivedAt)
                .Select(f => new { f.Repository, f.Project, f.TestRunner, f.NameKey, f.ReceivedAt, Duration = f.DurationMs!.Value });

            (string Repository, string Project, string TestRunner, string NameKey)? current = null;
            var durations = new List<DurationSample>();
            await foreach (var s in stream.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                var key = (s.Repository, s.Project, s.TestRunner, s.NameKey);
                if (current != key)
                {
                    Flush(current, durations, wanted, query, scopeArgs, rows);
                    current = key;
                    durations = [];
                }

                durations.Add(new DurationSample(DateTime.SpecifyKind(s.ReceivedAt, DateTimeKind.Utc), s.Duration));
            }

            Flush(current, durations, wanted, query, scopeArgs, rows);
        }

        return rows;
    }

    private void Flush(
        (string Repository, string Project, string TestRunner, string NameKey)? test, List<DurationSample> durations,
        HashSet<(string, string, string, string)> wanted, RegressionQuery query, InsightsArgs scopeArgs, List<RegressionRow> rows)
    {
        if (test is not { } c || !wanted.Contains(c)) return;
        var comparison = RegressionMath.Compare(durations, query);
        if (comparison == null) return;

        var testKey = TestKey.Format(StableId.Suite(c.Repository, c.Project, c.TestRunner), c.NameKey);
        rows.Add(new RegressionRow(
            Kind, testKey, c.Repository, c.Project, c.NameKey,
            comparison.BaselineSamples, comparison.CurrentSamples,
            comparison.BeforeP50Ms, comparison.AfterP50Ms, comparison.ShiftStartedOn,
            RegressionMath.Reason(comparison, "passing runs"),
            Evidence.Call("get_test", scopeArgs.Clone().With("test_key", testKey).With("status", "passed"))));
    }
}
