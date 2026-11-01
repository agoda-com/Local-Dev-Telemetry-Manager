using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>More flaky candidates than one page: ranking and enrichment must not depend on the page size.</summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindFlakyPagingTests
{
    private const int TESTS = 30;
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindFlakyPagingTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "100" });
        var start = McpTestHost.Now.AddDays(-10);
        var runs = new List<TestRun>();
        for (var i = 0; i < TESTS; i++)
        {
            // Test i: alternates for (6 + i % 9) runs, then settles on failing. Flip rates and fail rates differ and some tie.
            var length = 6 + i % 9;
            for (var r = 0; r < 14; r++)
            {
                var status = r < length ? (r % 2 == 0 ? "Passed" : "Failed") : "Failed";
                runs.Add(Seed.Run(start.AddHours(r * 3 + i % 3), [Seed.Case($"Flaky{i:D2}", status, 100 + i * 10 + r)], branch: "main"));
            }
        }

        await _host.SeedAsync(runs.ToArray());
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task FindFlakyTests_CursorWalkOverMoreCandidatesThanThePage_MatchesOneLargePage()
    {
        var large = await _client.CallAsync("find_flaky_tests", new { limit = 100 });
        var walked = await _client.CallAllPagesAsync("find_flaky_tests", new { limit = 5 });
        var first = await _client.CallAsync("find_flaky_tests", new { limit = 5 });

        large.IsError.ShouldBeFalse(large.ErrorText);
        large.Rows.Count.ShouldBe(TESTS);
        first.Rows.Count.ShouldBe(5);
        first.TotalRows.ShouldBe(TESTS);
        first.NextCursor.ShouldNotBeNull();
        walked.Select(r => r.ToString()).ShouldBe(large.Rows.Select(r => r.ToString()).ToList());
        walked.Select(r => r["test_key"]).Distinct().Count().ShouldBe(TESTS);
        // Enrichment ran for the page rows: sequences and ranked flip rates are present and descending.
        walked.ShouldAllBe(r => r["status_sequence"].Length > 0);
        walked.Select(r => r.Double("flip_rate")).ShouldBe(walked.Select(r => r.Double("flip_rate")).OrderByDescending(x => x).ToList());
    }
}
