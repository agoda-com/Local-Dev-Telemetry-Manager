using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture]
public class TestDataJunitIngestTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task POST_TestDataJunit_ParsesMultipartXml()
    {
        var xml = TestFixtures.CreateJUnitXml(testCount: 3);
        using var content = CreateMultipartContent(xml);

        var response = await _client.PostAsync("/testdata/junit", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var runs = await db.TestRuns.ToListAsync();
        var cases = await db.TestCases.ToListAsync();

        runs.Count.ShouldBe(1);
        cases.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task POST_TestDataJunit_HandlesMultipleXmlFiles()
    {
        var xml1 = TestFixtures.CreateJUnitXml(testCount: 2, suiteName: "Suite1");
        var xml2 = TestFixtures.CreateJUnitXml(testCount: 3, suiteName: "Suite2");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("testuser"), "userName");
        content.Add(new StringContent("dev-workstation"), "hostname");
        content.Add(new StringContent("Linux"), "platform");
        content.Add(new StringContent("Ubuntu 22.04"), "os");
        content.Add(new StringContent("main"), "branch");
        content.Add(new StringContent("TestProject"), "projectName");
        content.Add(new StringContent("https://github.com/test/repo"), "repository");
        content.Add(new StringContent("test-repo"), "repositoryName");
        content.Add(new StringContent("false"), "isDebuggerAttached");

        var fileContent1 = new ByteArrayContent(Encoding.UTF8.GetBytes(xml1));
        fileContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        content.Add(fileContent1, "files", "results1.xml");

        var fileContent2 = new ByteArrayContent(Encoding.UTF8.GetBytes(xml2));
        fileContent2.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        content.Add(fileContent2, "files", "results2.xml");

        var response = await _client.PostAsync("/testdata/junit", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var cases = await db.TestCases.ToListAsync();
        cases.Count.ShouldBe(5);
    }

    [Test]
    public async Task POST_TestDataJunit_ReturnsJsonResponse()
    {
        var xml = TestFixtures.CreateJUnitXml(testCount: 3);
        using var content = CreateMultipartContent(xml);

        var response = await _client.PostAsync("/testdata/junit", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("localTestRun", out _).ShouldBeTrue();
        json.RootElement.TryGetProperty("testSuiteCounts", out _).ShouldBeTrue();
    }

    private static MultipartFormDataContent CreateMultipartContent(string xml)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("testuser"), "userName");
        content.Add(new StringContent("dev-workstation"), "hostname");
        content.Add(new StringContent("Linux"), "platform");
        content.Add(new StringContent("Ubuntu 22.04"), "os");
        content.Add(new StringContent("main"), "branch");
        content.Add(new StringContent("TestProject"), "projectName");
        content.Add(new StringContent("https://github.com/test/repo"), "repository");
        content.Add(new StringContent("test-repo"), "repositoryName");
        content.Add(new StringContent("false"), "isDebuggerAttached");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
        content.Add(fileContent, "files", "results.xml");

        return content;
    }
}
