using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class DashboardService : IDashboardService
{
    private readonly TelemetryDbContext _db;

    public DashboardService(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<TestRunSummaryViewModel> GetTestRunSummaryAsync(FilterParams filters)
    {
        var query = ApplyTestRunFilters(_db.TestRuns.AsQueryable(), filters);

        var runs = await query.Select(r => new
        {
            r.ReceivedAt,
            r.TotalDurationMs,
            r.TotalTests,
            r.PassedTests,
            r.FailedTests
        }).ToListAsync();

        var totalRuns = runs.Count;
        var avgDuration = totalRuns > 0
            ? runs.Where(r => r.TotalDurationMs.HasValue).Select(r => r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average()
            : 0;

        var totalTests = runs.Sum(r => r.TotalTests ?? 0);
        var totalPassed = runs.Sum(r => r.PassedTests ?? 0);
        var passRate = totalTests > 0 ? (double)totalPassed / totalTests * 100 : 0;

        var durationTrend = runs
            .GroupBy(r => r.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyDataPoint(
                g.Key.ToString("yyyy-MM-dd"),
                g.Where(r => r.TotalDurationMs.HasValue).Select(r => r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average()))
            .ToList();

        var passFailTrend = runs
            .GroupBy(r => r.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyPassFail(
                g.Key.ToString("yyyy-MM-dd"),
                g.Sum(r => r.PassedTests ?? 0),
                g.Sum(r => r.FailedTests ?? 0)))
            .ToList();

        return new TestRunSummaryViewModel(totalRuns, avgDuration, passRate, durationTrend, passFailTrend);
    }

    public async Task<PaginatedResult<TestRunListItemViewModel>> GetTestRunsAsync(FilterParams filters)
    {
        var query = ApplyTestRunFilters(_db.TestRuns.AsQueryable(), filters);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.ReceivedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(r => new TestRunListItemViewModel(
                r.Id,
                r.ReceivedAt,
                r.ProjectName,
                r.TestRunner,
                r.TotalTests,
                r.PassedTests,
                r.FailedTests,
                r.TotalDurationMs,
                r.ExecutionEnvironment))
            .ToListAsync();

        return new PaginatedResult<TestRunListItemViewModel>(items, totalCount, filters.Page, filters.PageSize);
    }

    public async Task<TestRunDetailViewModel?> GetTestRunDetailAsync(string id)
    {
        var run = await _db.TestRuns
            .Include(r => r.TestCases)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (run == null)
            return null;

        var testCases = run.TestCases
            .OrderBy(tc => tc.Status == "Failed" ? 0 : 1)
            .ThenByDescending(tc => tc.DurationMs ?? 0)
            .Select(tc => new TestCaseViewModel(
                tc.Name,
                tc.FullName,
                tc.ClassName,
                tc.Status,
                tc.DurationMs,
                tc.ErrorMessage))
            .ToList();

        return new TestRunDetailViewModel(
            run.Id,
            run.RunId,
            run.ReceivedAt,
            run.UserName,
            run.Hostname,
            run.Branch,
            run.ProjectName,
            run.TestRunner,
            run.ExecutionEnvironment,
            run.TotalTests,
            run.PassedTests,
            run.FailedTests,
            run.SkippedTests,
            run.TotalDurationMs,
            testCases);
    }

    public async Task<ApiBuildSummaryViewModel> GetApiBuildSummaryAsync(FilterParams filters)
    {
        var query = ApplyBuildMetricFilters(
            _db.BuildMetrics.Where(b => b.BuildCategory == "API"),
            filters);

        var metrics = await query.Select(b => new
        {
            b.ReceivedAt,
            b.MetricType,
            b.TimeTakenMs
        }).ToListAsync();

        var compileMetrics = metrics.Where(m => m.MetricType == ".Net").ToList();
        var startupMetrics = metrics.Where(m => m.MetricType == ".AspNetStartup").ToList();
        var responseMetrics = metrics.Where(m => m.MetricType == ".AspNetResponse").ToList();

        var avgCompile = compileMetrics.Count > 0 ? compileMetrics.Average(m => m.TimeTakenMs) : 0;
        var avgStartup = startupMetrics.Count > 0 ? startupMetrics.Average(m => m.TimeTakenMs) : 0;
        var avgResponse = responseMetrics.Count > 0 ? responseMetrics.Average(m => m.TimeTakenMs) : 0;

        var compileTrend = compileMetrics
            .GroupBy(m => m.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Average(m => m.TimeTakenMs)))
            .ToList();

        var startupTrend = startupMetrics
            .GroupBy(m => m.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Average(m => m.TimeTakenMs)))
            .ToList();

        var firstResponseTrend = responseMetrics
            .GroupBy(m => m.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyDataPoint(g.Key.ToString("yyyy-MM-dd"), g.Average(m => m.TimeTakenMs)))
            .ToList();

        var allDates = metrics
            .Select(m => m.ReceivedAt.Date)
            .Distinct()
            .OrderBy(d => d);

        var dailyLifecycle = allDates.Select(date =>
        {
            var dayMetrics = metrics.Where(m => m.ReceivedAt.Date == date).ToList();
            return new DailyLifecyclePoint(
                date.ToString("yyyy-MM-dd"),
                dayMetrics.Where(m => m.MetricType == ".Net").Select(m => m.TimeTakenMs).DefaultIfEmpty(0).Average(),
                dayMetrics.Where(m => m.MetricType == ".AspNetStartup").Select(m => m.TimeTakenMs).DefaultIfEmpty(0).Average(),
                dayMetrics.Where(m => m.MetricType == ".AspNetResponse").Select(m => m.TimeTakenMs).DefaultIfEmpty(0).Average());
        }).ToList();

        return new ApiBuildSummaryViewModel(
            avgCompile, avgStartup, avgResponse,
            compileTrend, startupTrend, firstResponseTrend,
            dailyLifecycle);
    }

    public async Task<ClientsideBuildSummaryViewModel> GetClientsideBuildSummaryAsync(FilterParams filters)
    {
        var query = ApplyBuildMetricFilters(
            _db.BuildMetrics.Where(b => b.BuildCategory == "Clientside"),
            filters);

        var metrics = await query.Select(b => new
        {
            b.ReceivedAt,
            b.ReloadType,
            b.TimeTakenMs
        }).ToListAsync();

        var hotMetrics = metrics.Where(m => m.ReloadType == "hot").ToList();
        var fullMetrics = metrics.Where(m => m.ReloadType != "hot").ToList();

        var totalCount = metrics.Count;
        var hotReloadPercent = totalCount > 0 ? (double)hotMetrics.Count / totalCount * 100 : 0;
        var avgHotReloadTime = hotMetrics.Count > 0 ? hotMetrics.Average(m => m.TimeTakenMs) : 0;
        var avgFullReloadTime = fullMetrics.Count > 0 ? fullMetrics.Average(m => m.TimeTakenMs) : 0;

        var today = DateTime.UtcNow.Date;
        var totalReloadsToday = metrics.Count(m => m.ReceivedAt.Date == today);

        var reloadCountTrend = metrics
            .GroupBy(m => m.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyReloadCount(
                g.Key.ToString("yyyy-MM-dd"),
                g.Count(m => m.ReloadType == "hot"),
                g.Count(m => m.ReloadType != "hot")))
            .ToList();

        var reloadTimeTrend = metrics
            .GroupBy(m => m.ReceivedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyReloadTime(
                g.Key.ToString("yyyy-MM-dd"),
                g.Where(m => m.ReloadType == "hot").Select(m => m.TimeTakenMs).DefaultIfEmpty(0).Average(),
                g.Where(m => m.ReloadType != "hot").Select(m => m.TimeTakenMs).DefaultIfEmpty(0).Average()))
            .ToList();

        return new ClientsideBuildSummaryViewModel(
            hotReloadPercent, avgHotReloadTime, avgFullReloadTime,
            totalReloadsToday, reloadCountTrend, reloadTimeTrend);
    }

    public async Task<PaginatedResult<BuildMetricListItemViewModel>> GetBuildMetricsAsync(FilterParams filters)
    {
        var query = ApplyBuildMetricFilters(_db.BuildMetrics.AsQueryable(), filters);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(b => b.ReceivedAt)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(b => new BuildMetricListItemViewModel(
                b.Id,
                b.ReceivedAt,
                b.ProjectName,
                b.MetricType,
                b.BuildCategory,
                b.ReloadType,
                b.TimeTakenMs,
                b.ToolVersion,
                b.ExecutionEnvironment))
            .ToListAsync();

        return new PaginatedResult<BuildMetricListItemViewModel>(items, totalCount, filters.Page, filters.PageSize);
    }

    private static IQueryable<TestRun> ApplyTestRunFilters(IQueryable<TestRun> query, FilterParams filters)
    {
        if (!string.IsNullOrEmpty(filters.Environment) &&
            !string.Equals(filters.Environment, "all", StringComparison.OrdinalIgnoreCase))
        {
            var env = string.Equals(filters.Environment, "local", StringComparison.OrdinalIgnoreCase)
                ? "Local"
                : "CI";
            query = query.Where(r => r.ExecutionEnvironment == env);
        }

        if (!string.IsNullOrEmpty(filters.Platform))
            query = query.Where(r => r.TestRunner == filters.Platform);

        if (!string.IsNullOrEmpty(filters.Project))
            query = query.Where(r => r.ProjectName == filters.Project);

        if (!string.IsNullOrEmpty(filters.Repository))
            query = query.Where(r => r.RepositoryName == filters.Repository);

        if (!string.IsNullOrEmpty(filters.Branch))
            query = query.Where(r => r.Branch == filters.Branch);

        if (filters.From.HasValue)
            query = query.Where(r => r.ReceivedAt >= filters.From.Value);

        if (filters.To.HasValue)
            query = query.Where(r => r.ReceivedAt <= filters.To.Value);

        return query;
    }

    private static IQueryable<BuildMetric> ApplyBuildMetricFilters(IQueryable<BuildMetric> query, FilterParams filters)
    {
        if (!string.IsNullOrEmpty(filters.Environment) &&
            !string.Equals(filters.Environment, "all", StringComparison.OrdinalIgnoreCase))
        {
            var env = string.Equals(filters.Environment, "local", StringComparison.OrdinalIgnoreCase)
                ? "Local"
                : "CI";
            query = query.Where(b => b.ExecutionEnvironment == env);
        }

        if (!string.IsNullOrEmpty(filters.Platform))
            query = query.Where(b => b.MetricType == filters.Platform);

        if (!string.IsNullOrEmpty(filters.Project))
            query = query.Where(b => b.ProjectName == filters.Project);

        if (!string.IsNullOrEmpty(filters.Repository))
            query = query.Where(b => b.RepositoryName == filters.Repository);

        if (!string.IsNullOrEmpty(filters.Branch))
            query = query.Where(b => b.Branch == filters.Branch);

        if (filters.From.HasValue)
            query = query.Where(b => b.ReceivedAt >= filters.From.Value);

        if (filters.To.HasValue)
            query = query.Where(b => b.ReceivedAt <= filters.To.Value);

        return query;
    }
}
