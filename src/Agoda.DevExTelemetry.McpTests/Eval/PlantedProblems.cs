using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.DevExTelemetry.McpTests.Infrastructure;

namespace Agoda.DevExTelemetry.McpTests.Eval;

/// <summary>
/// The agent-eval dataset (issue #27, Phase 5): a repository's telemetry with known problems planted in
/// realistic noise. An agent investigating it, with all tools or with the data tools only, should find:
/// <list type="number">
/// <item><see cref="FLAKY_TEST"/>: flickers, failing 40% of CI runs.</item>
/// <item><see cref="BROKEN_TEST"/>: broke <see cref="BROKEN_DAYS_AGO"/> days ago and stayed broken.</item>
/// <item><see cref="SLOW_CLASS"/>: slow because of class-level setup (every test in it takes ~2.5 s).</item>
/// <item>The <c>.Net</c> compile of <see cref="API_PROJECT"/>: p50 steps up 35% from <see cref="REGRESSION_DAYS_AGO"/>
/// days ago, when <see cref="NEW_TOOL_VERSION"/> rolled out.</item>
/// </list>
/// and must not report the noise:
/// <list type="bullet">
/// <item><see cref="TDD_TEST"/>: fails then passes, but only in local runs (TDD).</item>
/// <item><see cref="RARE_TEST"/>: very slow and failing, but with 2 runs, below every min_runs.</item>
/// <item>The <c>vitehmr</c> series: 4 samples, one very slow, below every min_samples.</item>
/// </list>
/// Everything is deterministic and relative to <c>now</c>.
/// </summary>
public static class PlantedProblems
{
    public const string REPOSITORY = "shop";
    public const string TEST_PROJECT = "Shop.Tests";
    public const string API_PROJECT = "Shop.Api";
    public const string WEB_PROJECT = "shop-web";
    public const string RUNNER = "NUnit";

    public const string FLAKY_TEST = "Shop.Tests.CheckoutTests.PlacesOrder";
    public const string BROKEN_TEST = "Shop.Tests.CartTests.AppliesVoucher";
    public const string SLOW_CLASS = "Shop.Tests.InventoryTests";
    public const string TDD_TEST = "Shop.Tests.LoginTests.RejectsBadPassword";
    public const string RARE_TEST = "Shop.Tests.ReportTests.ExportsYearEnd";

    public const int CI_RUNS = 30;
    public const int BROKEN_DAYS_AGO = 4;
    public const int REGRESSION_DAYS_AGO = 5;
    public const string OLD_TOOL_VERSION = "10.0.100";
    public const string NEW_TOOL_VERSION = "10.0.200";

    public static string SuiteId => StableId.Suite(REPOSITORY, TEST_PROJECT, RUNNER);
    public static string CompileSeriesId => StableId.Series(REPOSITORY, API_PROJECT, ".Net", null);
    public static string HmrSeriesId => StableId.Series(REPOSITORY, WEB_PROJECT, "vitehmr", "hot");

    private static readonly string[] OtherTests =
    [
        "Shop.Tests.CheckoutTests.CalculatesTax", "Shop.Tests.CheckoutTests.RejectsEmptyCart",
        "Shop.Tests.CartTests.AddsItem", "Shop.Tests.CartTests.RemovesItem", "Shop.Tests.CartTests.MergesGuestCart",
        "Shop.Tests.LoginTests.AcceptsValidPassword", "Shop.Tests.LoginTests.LocksAfterFiveAttempts",
        "Shop.Tests.SearchTests.FindsByName", "Shop.Tests.SearchTests.FindsBySku", "Shop.Tests.SearchTests.PagesResults",
        "Shop.Tests.PricingTests.AppliesBulkDiscount", "Shop.Tests.PricingTests.RoundsCurrency",
        "Shop.Tests.ShippingTests.QuotesExpress", "Shop.Tests.ShippingTests.QuotesStandard",
        "Shop.Tests.AccountTests.UpdatesEmail", "Shop.Tests.AccountTests.DeletesAccount"
    ];

    private static readonly string[] InventoryTests =
    [
        "ReservesStock", "ReleasesStock", "CountsStock", "ReordersLowStock",
        "MovesStockBetweenWarehouses", "WritesOffDamagedStock", "AuditsStock", "ImportsStockFile"
    ];

    public static IReadOnlyList<object> Build(DateTime now)
    {
        var entities = new List<object>();
        entities.AddRange(CiRuns(now));
        entities.AddRange(LocalRuns(now));
        entities.AddRange(RareRuns(now));
        entities.AddRange(CompileBuilds(now));
        entities.AddRange(HmrNoise(now));
        return entities;
    }

    /// <summary>30 CI runs over the last 14 days, every test in every run.</summary>
    private static IEnumerable<TestRun> CiRuns(DateTime now)
    {
        for (var i = 0; i < CI_RUNS; i++)
        {
            var at = now.AddDays(-14).AddHours(i * 11.2 + 1);
            var daysAgo = (now - at).TotalDays;
            var cases = new List<TestCase>();

            // 1. Flaky: fails in runs 0, 3, 6, 9, 10, 13, ... (40%), with a timing error.
            var flakyFails = i * 7 % 10 < 4;
            cases.Add(Case(FLAKY_TEST, flakyFails ? "Failed" : "Passed", 180 + i % 5 * 10,
                flakyFails ? "System.TimeoutException: payment gateway stub did not answer within 2000 ms" : null));

            // 2. Broken: passes until BROKEN_DAYS_AGO, fails every run since.
            var broken = daysAgo < BROKEN_DAYS_AGO;
            cases.Add(Case(BROKEN_TEST, broken ? "Failed" : "Passed", 60 + i % 3 * 5,
                broken ? "System.NullReferenceException: Object reference not set to an instance of an object.\n   at Shop.VoucherService.Apply(Cart cart)" : null));

            // 3. Slow class: every test pays ~2.5 s of setup.
            foreach (var test in InventoryTests)
                cases.Add(Case($"{SLOW_CLASS}.{test}", "Passed", 2400 + i * 13 % 200, null));

            // The local-only TDD test always passes in CI.
            cases.Add(Case(TDD_TEST, "Passed", 40, null));

            foreach (var test in OtherTests)
                cases.Add(Case(test, "Passed", 20 + (i * 31 + test.Length * 7) % 90, null));

            yield return Seed.Run(at, cases, repository: REPOSITORY, project: TEST_PROJECT, runner: RUNNER,
                environment: "CI", branch: i % 4 == 3 ? $"feature/shop-{i}" : "main", cpuCount: 8);
        }
    }

    /// <summary>12 local runs of the login tests where the developer goes red-green-red-green (TDD).</summary>
    private static IEnumerable<TestRun> LocalRuns(DateTime now)
    {
        for (var i = 0; i < 12; i++)
        {
            var at = now.AddDays(-3).AddMinutes(i * 9);
            var fails = i % 2 == 0;
            yield return Seed.Run(at,
            [
                Case(TDD_TEST, fails ? "Failed" : "Passed", 35, fails ? "Expected: False  But was: True" : null),
                Case("Shop.Tests.LoginTests.AcceptsValidPassword", "Passed", 30, null)
            ], repository: REPOSITORY, project: TEST_PROJECT, runner: RUNNER, environment: "Local",
                branch: "feature/login-lockout", cpuCount: 12);
        }
    }

    /// <summary>A very slow, failing test with only 2 runs: below every min_runs.</summary>
    private static IEnumerable<TestRun> RareRuns(DateTime now)
    {
        yield return Seed.Run(now.AddDays(-6), [Case(RARE_TEST, "Failed", 95_000, "Out of memory")],
            repository: REPOSITORY, project: TEST_PROJECT, runner: RUNNER, environment: "CI");
        yield return Seed.Run(now.AddDays(-2), [Case(RARE_TEST, "Passed", 91_000, null)],
            repository: REPOSITORY, project: TEST_PROJECT, runner: RUNNER, environment: "CI");
    }

    /// <summary>
    /// The .Net compile of Shop.Api: 6 builds a day for 35 days on 8- and 16-core machines. From
    /// REGRESSION_DAYS_AGO days ago, every build runs on the new tool version and is 35% slower.
    /// </summary>
    private static IEnumerable<BuildMetric> CompileBuilds(DateTime now)
    {
        var dayStart = now.Date;
        for (var day = 35; day >= 0; day--)
        {
            for (var b = 0; b < 6; b++)
            {
                var at = dayStart.AddDays(-day).AddHours(8 + b * 1.5);
                if (at > now) continue;
                var regressed = day <= REGRESSION_DAYS_AGO;
                var baseMs = 20_000 + (day * 37 + b * 211) % 2_000;
                yield return Seed.Build(at, regressed ? baseMs * 1.35 : baseMs,
                    metricType: ".Net", repository: REPOSITORY, project: API_PROJECT, category: "API",
                    environment: "Local", cpuCount: b % 2 == 0 ? 8 : 16, os: b % 3 == 0 ? "Windows 11" : "macOS 15",
                    toolVersion: regressed ? NEW_TOOL_VERSION : OLD_TOOL_VERSION, branch: b % 2 == 0 ? "main" : "feature/checkout");
            }
        }
    }

    /// <summary>4 hot reloads, one very slow: too few samples for any analysis threshold.</summary>
    private static IEnumerable<BuildMetric> HmrNoise(DateTime now)
    {
        double[] times = [180, 210, 9_500, 190];
        for (var i = 0; i < times.Length; i++)
            yield return Seed.Build(now.AddDays(-1 - i), times[i], metricType: "vitehmr", repository: REPOSITORY,
                project: WEB_PROJECT, category: "Clientside", reloadType: "hot", toolVersion: "5.4.0");
    }

    private static TestCase Case(string nameKey, string status, double durationMs, string? error)
    {
        var dot = nameKey.LastIndexOf('.');
        return Seed.Case(nameKey[(dot + 1)..], status, durationMs, className: nameKey[..dot], fullName: nameKey, error: error);
    }
}
