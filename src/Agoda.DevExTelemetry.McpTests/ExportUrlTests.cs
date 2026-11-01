using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture]
public class ExportUrlTests
{
    [Test]
    public async Task ExportUrl_PublicBaseUrlSet_UsesIt()
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite,
            new Dictionary<string, string?> { ["Mcp:PublicBaseUrl"] = "https://dx.example.com/" });
        await using var client = await host.ConnectAsync();

        var result = await client.CallAsync("list_scopes", new { kind = "branch" });

        result.ExportUrl.ShouldBe("https://dx.example.com/api/export/list_scopes.csv?kind=branch");
    }

    [Test]
    public async Task ExportUrl_NoPublicBaseUrl_UsesRequestHost()
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite);
        await using var client = await host.ConnectAsync();

        var result = await client.CallAsync("list_scopes");

        result.ExportUrl.ShouldBe("http://localhost/api/export/list_scopes.csv");
    }

    [Test]
    public async Task ExportUrl_BehindProxy_UsesForwardedProtoAndHost()
    {
        using var host = new McpTestHost(DatabaseProvider.Sqlite);
        var http = host.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-Forwarded-Proto"] = "https",
                    ["X-Forwarded-Host"] = "compilation-metrics.internal, proxy-2"
                }
            },
            http, loggerFactory: null, ownsHttpClient: true);
        await using var client = await McpClient.CreateAsync(transport);

        var result = await client.CallAsync("list_scopes");

        result.ExportUrl.ShouldBe("https://compilation-metrics.internal/api/export/list_scopes.csv");
    }
}
