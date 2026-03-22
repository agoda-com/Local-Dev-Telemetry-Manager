using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Agoda.DevExTelemetry.IntegrationTests;

public static class TestFixtures
{
    public static object CreateDotnetBuildPayload(string type = ".Net", string? timeTaken = "1234",
        int platform = 4, bool isDebuggerAttached = false, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform,
            os = "Ubuntu 22.04",
            timeTaken,
            branch = "main",
            type,
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            date = "2026-03-22T10:00:00Z",
            isDebuggerAttached,
            tags = new Dictionary<string, string> { ["env"] = "dev" }
        };

    public static object CreateNUnitPayload(int testCaseCount = 3, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            nUnitTestCases = Enumerable.Range(1, testCaseCount).Select(i => new
            {
                id = $"tc-{i}",
                name = $"Test_{i}",
                fullName = $"Namespace.Class.Test_{i}",
                className = "Namespace.Class",
                methodName = $"Test_{i}",
                result = i % 3 == 0 ? "Skipped" : i % 2 == 0 ? "Failed" : "Passed",
                duration = 1.5 * i,
                startTime = "2026-03-22T10:00:00Z",
                endTime = "2026-03-22T10:00:01Z",
                errorMessage = i % 2 == 0 ? "Test failed" : (string?)null
            }).ToList()
        };

    public static object CreateJUnitPayload(int testCaseCount = 3, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            jUnitTestCases = Enumerable.Range(1, testCaseCount).Select(i => new
            {
                id = $"tc-{i}",
                name = $"Test_{i}",
                fullName = $"com.test.Class.Test_{i}",
                className = "com.test.Class",
                methodName = $"Test_{i}",
                result = "Passed",
                duration = 42.0,
                startTime = "2026-03-22T10:00:00Z",
                endTime = "2026-03-22T10:00:01Z",
                errorMessage = (string?)null
            }).ToList()
        };

    public static object CreateJestPayload(int suiteCount = 1, int testPerSuite = 2, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            testRunner = "Jest",
            testRunnerVersion = "29.0.0",
            testCaseSummary = new
            {
                numFailedTestSuites = 0,
                numPassedTestSuites = suiteCount,
                numTotalTestSuites = suiteCount,
                numFailedTests = 0,
                numPassedTests = suiteCount * testPerSuite,
                numPendingTests = 0,
                numTotalTests = suiteCount * testPerSuite,
                startTime = 1711094400000L,
                success = true,
                testResults = Enumerable.Range(1, suiteCount).Select(s => new
                {
                    testFilePath = $"/src/tests/suite{s}.test.ts",
                    numFailingTests = 0,
                    numPassingTests = testPerSuite,
                    numPendingTests = 0,
                    testResults = Enumerable.Range(1, testPerSuite).Select(t => new
                    {
                        title = $"should test {t}",
                        fullName = $"Suite {s} should test {t}",
                        ancestorTitles = new[] { $"Suite {s}" },
                        status = "passed",
                        duration = 50.0 * t,
                        failureMessages = new List<string>()
                    }).ToList()
                }).ToList()
            }
        };

    public static object CreateVitestPayload(int testCaseCount = 2, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            files = new[]
            {
                new { name = "test/utils.test.ts", duration = 150.0, status = "pass" },
                new { name = "test/app.test.ts", duration = 200.0, status = "pass" }
            },
            testcases = Enumerable.Range(1, testCaseCount).Select(i => new
            {
                name = $"test case {i}",
                fullName = $"utils > test case {i}",
                file = "test/utils.test.ts",
                status = "passed",
                duration = 50.0 * i,
                errorMessage = (string?)null
            }).ToList()
        };

    public static object CreateScalaTestPayload(int testCaseCount = 2, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            runId = Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repositoryUrl = "https://github.com/test/scala-repo",
            repositoryName = "scala-repo",
            isDebuggerAttached = false,
            totalTests = testCaseCount + 1,
            succeededTests = testCaseCount,
            failedTests = 1,
            ignoredTests = 0,
            totalDuration = 5000.0,
            scalaTestCases = Enumerable.Range(1, testCaseCount).Select(i => new
            {
                name = $"test case {i}",
                className = "com.test.ScalaSuite",
                result = "Passed",
                duration = 100.0 * i,
                errorMessage = (string?)null
            }).ToList()
        };

    public static object CreateWebpackPayload(bool withHmrFeedback = false, string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            timestamp = 1711094400000L,
            builtAt = 1711094400000L,
            totalMemory = 16_000_000_000L,
            cpuModels = new[] { "Intel Core i7" },
            cpuSpeed = 3.6,
            nodeVersion = "20.0.0",
            v8Version = "11.0",
            commitSha = "abc123",
            timeTaken = "5000",
            devFeedback = withHmrFeedback
                ? new[] { new { type = "hmr", timeTaken = "200" } }
                : Array.Empty<object>()
        };

    public static object CreateVitePayload(string type = "vite", string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount = 8,
            hostname = "dev-workstation",
            platform = 4,
            os = "Ubuntu 22.04",
            branch = "main",
            type,
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = false,
            timestamp = 1711094400000L,
            builtAt = 1711094400000L,
            totalMemory = 16_000_000_000L,
            cpuModels = new[] { "Intel Core i7" },
            cpuSpeed = 3.6,
            nodeVersion = "20.0.0",
            v8Version = "11.0",
            commitSha = "def456",
            timeTaken = "3000",
            viteVersion = "5.0.0",
            bundleStats = new { totalSize = 1024 },
            file = "src/main.tsx",
            moduleCount = 42,
            devFeedback = Array.Empty<object>()
        };

    public static object CreateGradlePayload(string? durationMs = "5000", string? cpuCount = "8",
        string? id = null) =>
        new
        {
            id = id ?? Guid.NewGuid().ToString(),
            userName = "testuser",
            cpuCount,
            hostname = "dev-workstation",
            platform = "Linux",
            os = "Ubuntu 22.04",
            branch = "main",
            projectName = "TestProject",
            repository = "https://github.com/test/repo",
            repositoryName = "test-repo",
            isDebuggerAttached = "false",
            durationMs,
            configurationDurationMs = "1000",
            requestedTasks = "assembleDebug",
            gradleVersion = "8.5",
            rootProject = "app",
            success = "true",
            buildId = Guid.NewGuid().ToString(),
            cacheRatio = "0.75"
        };

    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync(url, content);
    }

    public static async Task<HttpResponseMessage> PostGzippedJsonAsync(HttpClient client, string url,
        object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var memoryStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            await gzipStream.WriteAsync(jsonBytes);
        }

        memoryStream.Position = 0;
        var content = new StreamContent(memoryStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Headers.ContentEncoding.Add("gzip");

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        return await client.SendAsync(request);
    }

    public static string CreateJUnitXml(int testCount = 3, string suiteName = "TestSuite") =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <testsuite name="{suiteName}" tests="{testCount}" failures="1" errors="0" skipped="0" time="1.5">
         {string.Join("\n", Enumerable.Range(1, testCount).Select(i =>
             i == 1
                 ? $"""  <testcase name="test_{i}" classname="{suiteName}" time="0.5"><failure message="assertion failed">stack trace</failure></testcase>"""
                 : $"""  <testcase name="test_{i}" classname="{suiteName}" time="0.5"/>"""))}
         </testsuite>
         """;
}
