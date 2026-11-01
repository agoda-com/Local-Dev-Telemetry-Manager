using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Tests;

/// <summary>A test suite: repository (RepositoryName) + project (ProjectName) + test runner.</summary>
public sealed record TestSuiteIdentity(string Repository, string Project, string TestRunner)
{
    public string SuiteId => StableId.Suite(Repository, Project, TestRunner);
}

/// <summary>
/// Turns <c>suite_id</c> and <c>test_key</c> arguments back into the columns they hash, so queries can
/// filter in SQL. Suites are found across all retained data, not just the call's window.
/// </summary>
public interface ITestSuiteCatalog
{
    Task<IReadOnlyList<TestSuiteIdentity>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The suite, or an <see cref="InsightsArgumentException"/> naming <paramref name="argumentName"/>.</summary>
    Task<TestSuiteIdentity> ResolveAsync(string suiteId, CancellationToken cancellationToken, string argumentName = "suite_id");

    /// <summary>Splits and resolves a <c>{suite_id}:{name_key}</c> test key.</summary>
    Task<(TestSuiteIdentity Suite, string NameKey)> ResolveTestKeyAsync(string testKey, CancellationToken cancellationToken, string argumentName = "test_key");
}

[RegisterPerRequest]
public sealed class TestSuiteCatalog : ITestSuiteCatalog
{
    private readonly TelemetryDbContext _db;
    private IReadOnlyList<TestSuiteIdentity>? _suites;

    public TestSuiteCatalog(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TestSuiteIdentity>> ListAsync(CancellationToken cancellationToken)
    {
        if (_suites != null) return _suites;
        var triples = await _db.TestRuns
            .Select(r => new { r.RepositoryName, r.ProjectName, r.TestRunner })
            .Distinct()
            .ToListAsync(cancellationToken);
        _suites = triples
            .Select(t => new TestSuiteIdentity(t.RepositoryName, t.ProjectName, t.TestRunner))
            .ToList();
        return _suites;
    }

    public async Task<TestSuiteIdentity> ResolveAsync(string suiteId, CancellationToken cancellationToken, string argumentName = "suite_id")
    {
        var suites = await ListAsync(cancellationToken);
        return suites.FirstOrDefault(s => string.Equals(s.SuiteId, suiteId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InsightsArgumentException(argumentName, $"no suite '{suiteId}'; list_test_suites shows the suite ids");
    }

    public async Task<(TestSuiteIdentity Suite, string NameKey)> ResolveTestKeyAsync(string testKey, CancellationToken cancellationToken, string argumentName = "test_key")
    {
        if (!TestKey.TryParse(testKey, out var suiteId, out var nameKey))
            throw new InsightsArgumentException(argumentName, $"'{testKey}' is not a test key; use the test_key column of get_tests");
        var suite = await ResolveAsync(suiteId, cancellationToken, argumentName);
        return (suite, nameKey);
    }
}
