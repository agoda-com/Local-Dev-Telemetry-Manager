using System.Net;
using System.Text;
using System.Text.Json;
using Agoda.DevExTelemetry.Core.Models.Ingest;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests.Contracts;

/// <summary>
/// Posts each client's real payload, byte for byte, from Contracts/Fixtures to its route and reads the
/// stored row back. The fixtures are the contract with devfeedback-js, agoda-test-metrics and
/// Agoda.Tests.Metrics: change a fixture only when the client changes what it sends.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class ClientFixtureContractTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ClientFixtureContractTests(DatabaseProvider provider) => _provider = provider;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory(_provider);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // Red until "Lenient /vite, /webpack and /vitest ingest": /vite returns 400 for a string platform,
    // a numeric timeTaken, an ISO builtAt and a cpuSpeed array.
    [Test]
    public async Task Fixture_Vite_IsStored()
    {
        var response = await PostFixtureAsync("/vite", "vite-devfeedback-2.2.0.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Id.ShouldBe("5b0c9a40-b84a-11f1-8d6a-0242ac120002");
        metric.MetricType.ShouldBe("vite");
        metric.Platform.ShouldBe(nameof(PlatformID.Unix));
        metric.TimeTakenMs.ShouldBe(4210);
        metric.ToolVersion.ShouldBe("4.52.4");
        metric.SourceEndpoint.ShouldBe("/vite");
    }

    // Red until "New /playwright and /command endpoints": /command does not exist yet (405, the
    // fallback SPA route only answers GET).
    [Test]
    public async Task Fixture_Command_IsStored()
    {
        var response = await PostFixtureAsync("/command", "command-devfeedback-2.2.0.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Id.ShouldBe("5b0c9a41-b84a-11f1-8d6a-0242ac120002");
        metric.MetricType.ShouldBe("command");
        metric.Platform.ShouldBe(nameof(PlatformID.Unix));
        metric.TimeTakenMs.ShouldBe(9875);
        metric.SourceEndpoint.ShouldBe("/command");
        metric.ExtraData.ShouldNotBeNull();
        metric.ExtraData.ShouldContain("pnpm run test");
        metric.ExtraData.ShouldContain("aborted");
        metric.ExtraData.ShouldContain("SIGINT");
    }

    // Red until "Lenient /vite, /webpack and /vitest ingest": /vitest returns 400 for a string platform
    // and an errorMessage array.
    [Test]
    public async Task Fixture_Vitest_IsStored()
    {
        var response = await PostFixtureAsync("/vitest", "vitest-test-metrics-0.1.0.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.SingleAsync();
        run.Id.ShouldBe("contract-0001");
        run.RunId.ShouldBe("run-0001");
        run.TestRunner.ShouldBe("Vitest");
        run.Platform.ShouldBe(nameof(PlatformID.Unix));
        run.TotalTests.ShouldBe(2);
        run.PassedTests.ShouldBe(1);
        run.FailedTests.ShouldBe(1);

        var cases = await db.TestCases.OrderBy(c => c.Id).ToListAsync();
        cases.Select(c => (c.Name, c.Status, c.ClassName)).ShouldBe(new (string, string, string?)[]
        {
            ("passes", "Passed", "src/a.test.ts"),
            ("fails", "Failed", "src/a.test.ts")
        });
        cases[1].ErrorMessage.ShouldBe("expected 1 to be 2");
    }

    // Red until "New /playwright and /command endpoints": /playwright does not exist yet (405, the
    // fallback SPA route only answers GET).
    [Test]
    public async Task Fixture_Playwright_IsStored()
    {
        var response = await PostFixtureAsync("/playwright", "playwright-test-metrics-0.1.0.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.SingleAsync();
        run.Id.ShouldBe("contract-0001");
        run.RunId.ShouldBe("run-0002");
        run.TestRunner.ShouldBe("Playwright");
        run.Platform.ShouldBe(nameof(PlatformID.Unix));
        run.TotalTests.ShouldBe(4);
        run.PassedTests.ShouldBe(2);
        run.FailedTests.ShouldBe(2);

        // One row per attempt: "flaky" failed on retry 0 and passed on retry 1; timedOut is stored as Failed.
        var cases = await db.TestCases.OrderBy(c => c.Id).ToListAsync();
        cases.Select(c => (c.Name, c.Status)).ShouldBe(new[]
        {
            ("passes", "Passed"),
            ("flaky", "Failed"),
            ("flaky", "Passed"),
            ("fails", "Failed")
        });
        cases[1].ErrorMessage.ShouldBe("locator not visible");
    }

    [Test]
    public async Task Fixture_Nunit_IsStored()
    {
        var response = await PostFixtureAsync("/dotnet/nunit", "nunit-1.0.128.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.SingleAsync();
        run.Id.ShouldBe("3f2b8e1c-6a4d-4f0e-9b7a-2c5d8e9f0a11");
        run.RunId.ShouldBe("7d3e2a90-1c4b-4e8f-a6d5-0b9c8e7f6a52");
        run.TestRunner.ShouldBe("NUnit");
        run.Platform.ShouldBe(nameof(PlatformID.Unix));
        run.TotalTests.ShouldBe(2);
        run.PassedTests.ShouldBe(1);
        run.FailedTests.ShouldBe(1);

        var cases = await db.TestCases.OrderBy(c => c.Id).ToListAsync();
        cases.Select(c => (c.Name, c.Status, c.ClassName, c.MethodName)).ShouldBe(new (string, string, string?, string?)[]
        {
            ("Passes", "Passed", "Demo.Tests.SampleTests", "Passes"),
            ("Fails", "Failed", "Demo.Tests.SampleTests", "Fails")
        });
        cases[0].FullName.ShouldBe("Demo.Tests.SampleTests.Passes");
        cases[0].DurationMs.ShouldNotBeNull();
        cases[0].DurationMs!.Value.ShouldBe(22, tolerance: 0.001);
    }

    [Test]
    public async Task Fixture_Playwright_ReadsIntoPlaywrightPayload()
    {
        var payload = JsonSerializer.Deserialize<PlaywrightPayload>(
            await ReadFixtureAsync("playwright-test-metrics-0.1.0.json"), WebJson)!;

        payload.Platform.ShouldBe(PlatformID.Unix);
        payload.TimeTaken.ShouldBe("1234");
        payload.Timestamp.ShouldBe(1793577600000L);
        payload.BuiltAt.ShouldBe(1793577600000L);
        payload.CpuSpeed.ShouldBe(new List<double> { 3000 });
        payload.Workers.ShouldBe(2);
        payload.Retries.ShouldBe(1);
        payload.Files!.Single().Duration.ShouldBe(900);
        payload.TestCases!.Select(t => (t.Name, t.Status, t.Retry)).ShouldBe(new (string?, string?, int)[]
        {
            ("passes", "passed", 0),
            ("flaky", "failed", 0),
            ("flaky", "passed", 1),
            ("fails", "timedOut", 0)
        });
        payload.TestCases![1].StartTime.ShouldBe(1793577600300L);
        payload.TestCases![1].ErrorMessage.ShouldBe("locator not visible");
        payload.TestCases![0].ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task Fixture_Command_ReadsIntoCommandPayload()
    {
        var payload = JsonSerializer.Deserialize<CommandPayload>(
            await ReadFixtureAsync("command-devfeedback-2.2.0.json"), WebJson)!;

        payload.Type.ShouldBe("command");
        payload.Platform.ShouldBe(PlatformID.Unix);
        payload.TimeTaken.ShouldBe("9875");
        payload.BuiltAt.ShouldBe(1793577600000L);
        payload.Phase.ShouldBe("test");
        payload.Command.ShouldBe("pnpm run test");
        payload.ExitCode.ShouldBe(130);
        payload.Success.ShouldBe(false);
        payload.Outcome.ShouldBe("aborted");
        payload.Signal.ShouldBe("SIGINT");
        payload.Runner.ShouldBe("pnpm");
        payload.Target.ShouldBe("test");
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static Task<string> ReadFixtureAsync(string fixtureName) =>
        File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory, "Contracts", "Fixtures", fixtureName));

    private async Task<HttpResponseMessage> PostFixtureAsync(string route, string fixtureName)
    {
        var json = await ReadFixtureAsync(fixtureName);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(route, content);
    }
}
