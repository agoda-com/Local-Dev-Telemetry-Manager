using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.McpTests;

[TestFixture]
public class RecipePromptTests
{
    private McpTestHost _host = null!;
    private McpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _host = new McpTestHost(DatabaseProvider.Sqlite);
        _client = await _host.ConnectAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        _host.Dispose();
    }

    [Test]
    public async Task ListPrompts_ReturnsTheFourRecipes()
    {
        var prompts = await _client.ListPromptsAsync();

        prompts.Select(p => p.Name).OrderBy(n => n).ShouldBe([
            "investigate_build_regressions", "investigate_flaky_tests", "investigate_slow_tests", "triage_dev_loop"
        ]);
        prompts.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.Description));
    }

    [TestCase("investigate_slow_tests", "find_slow_tests")]
    [TestCase("investigate_flaky_tests", "find_flaky_tests")]
    [TestCase("investigate_build_regressions", "find_regressions")]
    public async Task GetPrompt_StartsFromItsAnalysisTool(string prompt, string tool)
    {
        var result = await _client.GetPromptAsync(prompt, new Dictionary<string, object?> { ["repository"] = "web-app" });

        var text = string.Join("\n", result.Messages.Select(m => m.Content).OfType<TextContentBlock>().Select(t => t.Text));
        text.ShouldContain(tool);
        text.ShouldContain("repository=web-app");
    }

    [Test]
    public async Task GetPrompt_TriageDevLoop_StartsFromOverview()
    {
        var result = await _client.GetPromptAsync("triage_dev_loop", new Dictionary<string, object?> { ["repository"] = "web-app" });

        var text = string.Join("\n", result.Messages.Select(m => m.Content).OfType<TextContentBlock>().Select(t => t.Text));
        text.ShouldContain("get_overview");
        text.ShouldContain("investigate_flaky_tests");
    }
}
