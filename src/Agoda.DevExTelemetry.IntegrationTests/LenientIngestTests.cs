using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

/// <summary>
/// /vite, /webpack and /vitest accept the field shapes JS clients really send (C3): an OS-type
/// platform string, a numeric timeTaken, an ISO builtAt, a cpuSpeed array, an errorMessage array and
/// a test case <c>filename</c>. A shape outside the contract is still a 400.
/// </summary>
[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class LenientIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public LenientIngestTests(DatabaseProvider provider) => _provider = provider;

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

    [TestCase("Linux", nameof(PlatformID.Unix))]
    [TestCase("Darwin", nameof(PlatformID.MacOSX))]
    [TestCase("Windows_NT", nameof(PlatformID.Win32NT))]
    [TestCase("SunOS", nameof(PlatformID.Other))]
    public async Task Vite_WhenPlatformIsLinuxString_StoresUnix(string osType, string expected)
    {
        var response = await PostAsync("/vite", VitePayload(platform: osType));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Platform.ShouldBe(expected);
    }

    [Test]
    public async Task Vite_WhenTimeTakenIsNumberAndBuiltAtIsIso_StoresRow()
    {
        var response = await PostAsync("/vite", VitePayload(
            timeTaken: 4210.5, builtAt: "2026-11-02T00:00:00.000Z", cpuSpeed: new[] { 3000, 3100 }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.TimeTakenMs.ShouldBe(4210.5);
        metric.ExtraData.ShouldNotBeNull();
        metric.ExtraData.ShouldContain("1793577600000");
        metric.ExtraData.ShouldContain("[3000,3100]");
    }

    [Test]
    public async Task Vite_WhenPlatformIsObject_Returns400()
    {
        var response = await PostAsync("/vite", VitePayload(platform: new { name = "Linux" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        (await db.BuildMetrics.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Webpack_WhenFieldsAreNodeShaped_StoresRow()
    {
        var response = await PostAsync("/webpack", new
        {
            id = "webpack-lenient-1",
            platform = "Darwin",
            timeTaken = 5000,
            timestamp = "2026-11-02T00:00:00Z",
            builtAt = 1793577600000L,
            cpuSpeed = new[] { 2400 },
            devFeedback = new[] { new { type = "hmr", timeTaken = 200 } }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var metric = await db.BuildMetrics.SingleAsync();
        metric.Platform.ShouldBe(nameof(PlatformID.MacOSX));
        metric.TimeTakenMs.ShouldBe(5000);
        metric.ReloadType.ShouldBe("hot");
    }

    [Test]
    public async Task Vitest_WhenErrorMessageIsArray_StoresJoinedMessage()
    {
        var response = await PostAsync("/vitest", new
        {
            id = "vitest-lenient-1",
            platform = "Linux",
            testcases = new object[]
            {
                new
                {
                    name = "fails",
                    status = "fail",
                    errorMessage = new[] { "expected 1 to be 2", "at src/a.test.ts:3:7" },
                    filename = "src/a.test.ts"
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var run = await db.TestRuns.SingleAsync();
        run.Platform.ShouldBe(nameof(PlatformID.Unix));
        var testCase = await db.TestCases.SingleAsync();
        testCase.Status.ShouldBe("Failed");
        testCase.ErrorMessage.ShouldBe("expected 1 to be 2\nat src/a.test.ts:3:7");
        testCase.ClassName.ShouldBe("src/a.test.ts");
    }

    [Test]
    public async Task Vitest_WhenBothFileAndFilenameAreSent_StoresFile()
    {
        var response = await PostAsync("/vitest", new
        {
            id = "vitest-lenient-2",
            platform = 4,
            testcases = new object[]
            {
                new { name = "passes", status = "pass", file = "src/file.test.ts", filename = "src/filename.test.ts" }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var testCase = await db.TestCases.SingleAsync();
        testCase.ClassName.ShouldBe("src/file.test.ts");
    }

    private static object VitePayload(
        object? platform = null, object? timeTaken = null, object? builtAt = null, object? cpuSpeed = null) =>
        new
        {
            id = Guid.NewGuid().ToString(),
            userName = "dev",
            cpuCount = 8,
            hostname = "dev-box",
            platform = platform ?? "Linux",
            os = "6.18.0",
            type = "vite",
            timeTaken = timeTaken ?? 1234,
            timestamp = 1793577600000L,
            builtAt = builtAt ?? 1793577600000L,
            cpuSpeed = cpuSpeed ?? new[] { 3000 },
            viteVersion = "4.52.4"
        };

    private Task<HttpResponseMessage> PostAsync(string route, object payload) =>
        TestFixtures.PostJsonAsync(_client, route, payload);
}
