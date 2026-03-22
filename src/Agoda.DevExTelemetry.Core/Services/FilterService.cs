using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Dashboard;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Services;

[RegisterPerRequest]
public class FilterService : IFilterService
{
    private readonly TelemetryDbContext _db;

    public FilterService(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<FilterOptionsViewModel> GetAvailableFiltersAsync()
    {
        var buildMetricProjects = _db.BuildMetrics.Select(b => b.ProjectName);
        var testRunProjects = _db.TestRuns.Select(t => t.ProjectName);
        var projects = await buildMetricProjects
            .Union(testRunProjects)
            .Where(p => p != "")
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        var buildMetricRepos = _db.BuildMetrics.Select(b => b.RepositoryName);
        var testRunRepos = _db.TestRuns.Select(t => t.RepositoryName);
        var repositories = await buildMetricRepos
            .Union(testRunRepos)
            .Where(r => r != "")
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        var buildMetricBranches = _db.BuildMetrics.Select(b => b.Branch);
        var testRunBranches = _db.TestRuns.Select(t => t.Branch);
        var branches = await buildMetricBranches
            .Union(testRunBranches)
            .Where(b => b != "")
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        var buildMetricPlatforms = _db.BuildMetrics.Select(b => b.Platform);
        var testRunPlatforms = _db.TestRuns.Select(t => t.Platform);
        var platforms = await buildMetricPlatforms
            .Union(testRunPlatforms)
            .Where(p => p != "")
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        var testRunners = await _db.TestRuns
            .Select(t => t.TestRunner)
            .Where(t => t != "")
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        var metricTypes = await _db.BuildMetrics
            .Select(b => b.MetricType)
            .Where(m => m != "")
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        return new FilterOptionsViewModel(projects, repositories, branches, platforms, testRunners, metricTypes);
    }
}
