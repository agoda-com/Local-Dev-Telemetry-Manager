using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>More failing candidates than one page: ranking and enrichment must not depend on the page size.</summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class FindFailingPagingTests
{
    private const int TESTS = 30;
    private readonly DatabaseProvider _provider;
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    public FindFailingPagingTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(_provider, new Dictionary<string, string?> { ["Mcp:MaxRows"] = "100" });
        var start = McpTestHost.Now.AddDays(-10);
        var runs = new List<TestRun>();
        for (var i = 0; i < TESTS; i++)
        {
            // Even tests broke recently after many passes (listed by the recent rule); odd tests mostly fail (listed by the rate rule).
            var sequence = i % 2 == 0 ? "PPPPPPP" + new string('F', 3 + i % 4) : "FFFFFFFFP" + new string('F', i % 3);
            for (var r = 0; r < sequence.Length; r++)
            {
                runs.Add(Seed.Run(start.AddHours(r * 2 + i % 5),
                    [Seed.Case($"Broken{i:D2}", sequence[r] == 'P' ? "Passed" : "Failed", 100 + r, error: $"boom {i}")], branch: "main"));
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
    public async Task FindFailingTests_CursorWalkOverMoreCandidatesThanThePage_MatchesOneLargePage()
    {
        var large = await _client.CallAsync("find_failing_tests", new { limit = 100 });
        var walked = await _client.CallAllPagesAsync("find_failing_tests", new { limit = 5 });
        var first = await _client.CallAsync("find_failing_tests", new { limit = 5 });

        large.IsError.ShouldBeFalse(large.ErrorText);
        large.Rows.Count.ShouldBe(TESTS);
        first.Rows.Count.ShouldBe(5);
        first.TotalRows.ShouldBe(TESTS);
        first.NextCursor.ShouldNotBeNull();
        walked.Select(r => r.ToString()).ShouldBe(large.Rows.Select(r => r.ToString()).ToList());
        walked.Select(r => r["test_key"]).Distinct().Count().ShouldBe(TESTS);
        walked.ShouldAllBe(r => r["status_sequence"].Length > 0 && r["latest_error"].StartsWith("boom"));
    }
}
