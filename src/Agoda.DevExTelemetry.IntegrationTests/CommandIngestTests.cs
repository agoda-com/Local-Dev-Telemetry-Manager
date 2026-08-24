using System.Net;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;

namespace Agoda.DevExTelemetry.IntegrationTests;

[TestFixture(DatabaseProvider.Sqlite)]
[TestFixture(DatabaseProvider.PostgreSql)]
public class CommandIngestTests
{
    private readonly DatabaseProvider _provider;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public CommandIngestTests(DatabaseProvider provider) => _provider = provider;

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

    [Test]
    public async Task POST_Command_InstallWithTimers_PersistsEventAndTimerRows()
    {
        var payload = TestFixtures.CreateCommandPayload(
            phase: "install",
            measurementSource: "npm-timing",
            npmTimers: new Dictionary<string, double>
            {
                ["idealTree"] = 8120,
                ["reify"] = 39880
            });

        var response = await TestFixtures.PostJsonAsync(_client, "/command", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var evt = await db.CommandEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.MeasurementSource.ShouldBe("npm-timing");

        var timers = await db.CommandEventNpmTimers
            .OrderBy(t => t.TimerName)
            .ToListAsync();
        timers.Count.ShouldBe(2);
        timers[0].TimerName.ShouldBe("idealTree");
        timers[0].DurationMs.ShouldBe(8120);
        timers[1].TimerName.ShouldBe("reify");
        timers[1].DurationMs.ShouldBe(39880);
    }

    [Test]
    public async Task POST_Command_Devserver_WithPrebundledFalse_PersistsFalse()
    {
        var payload = TestFixtures.CreateCommandPayload(
            phase: "devserver",
            prebundled: false);

        await TestFixtures.PostJsonAsync(_client, "/command", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var evt = await db.CommandEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.Prebundled.ShouldBe(false);
    }

    [Test]
    public async Task POST_Command_Devserver_WithoutPrebundled_PersistsNull()
    {
        var payload = TestFixtures.CreateCommandPayload(
            phase: "devserver",
            includePrebundled: false);

        await TestFixtures.PostJsonAsync(_client, "/command", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var evt = await db.CommandEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.Prebundled.ShouldBeNull();
    }

    [Test]
    public async Task POST_Command_WithSpooledAt_PersistsValue()
    {
        const long spooledAt = 1785900160500;
        var payload = TestFixtures.CreateCommandPayload(
            phase: "devserver",
            spooledAt: spooledAt);

        await TestFixtures.PostJsonAsync(_client, "/command", payload);
        await _factory.DrainBackgroundQueuesAsync();

        using var db = _factory.CreateDbContext();
        var evt = await db.CommandEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.SpooledAt.ShouldBe(spooledAt);
    }
}
