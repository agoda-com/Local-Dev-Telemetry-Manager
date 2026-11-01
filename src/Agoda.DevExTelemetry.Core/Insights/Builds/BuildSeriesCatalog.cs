using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Models.Entities;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;

namespace Agoda.DevExTelemetry.Core.Insights.Builds;

/// <summary>A build series: repository + project + metric type (+ reload type for client-side builds).</summary>
public sealed record BuildSeriesIdentity(string Repository, string Project, string BuildCategory, string MetricType, string? ReloadType)
{
    public string SeriesId => StableId.Series(Repository, Project, MetricType, ReloadType);
}

/// <summary>Turns a <c>series_id</c> argument back into the columns it hashes.</summary>
public interface IBuildSeriesCatalog
{
    Task<IReadOnlyList<BuildSeriesIdentity>> ListAsync(CancellationToken cancellationToken);

    /// <summary>The series, or an <see cref="InsightsArgumentException"/> naming <paramref name="argumentName"/>.</summary>
    Task<BuildSeriesIdentity> ResolveAsync(string seriesId, CancellationToken cancellationToken, string argumentName = "series_id");
}

[RegisterPerRequest]
public sealed class BuildSeriesCatalog : IBuildSeriesCatalog
{
    private readonly TelemetryDbContext _db;
    private IReadOnlyList<BuildSeriesIdentity>? _series;

    public BuildSeriesCatalog(TelemetryDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BuildSeriesIdentity>> ListAsync(CancellationToken cancellationToken)
    {
        if (_series != null) return _series;
        var rows = await _db.BuildMetrics
            .Select(b => new { b.RepositoryName, b.ProjectName, b.BuildCategory, b.MetricType, b.ReloadType })
            .Distinct()
            .ToListAsync(cancellationToken);
        // A series whose rows disagree on BuildCategory is still one series; keep the first category seen.
        _series = rows
            .Select(r => new BuildSeriesIdentity(r.RepositoryName, r.ProjectName, r.BuildCategory, r.MetricType,
                string.IsNullOrEmpty(r.ReloadType) ? null : r.ReloadType))
            .GroupBy(s => s.SeriesId)
            .Select(g => g.OrderBy(s => s.BuildCategory, StringComparer.Ordinal).First())
            .ToList();
        return _series;
    }

    public async Task<BuildSeriesIdentity> ResolveAsync(string seriesId, CancellationToken cancellationToken, string argumentName = "series_id")
    {
        var series = await ListAsync(cancellationToken);
        return series.FirstOrDefault(s => string.Equals(s.SeriesId, seriesId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InsightsArgumentException(argumentName, $"no build series '{seriesId}'; list_build_series shows the series ids");
    }
}

public static class BuildSeriesQueries
{
    /// <summary>Only the metrics of one series. A null reload type matches null or empty.</summary>
    public static IQueryable<BuildMetric> InSeries(this IQueryable<BuildMetric> metrics, BuildSeriesIdentity series)
    {
        metrics = metrics.Where(b =>
            b.RepositoryName == series.Repository &&
            b.ProjectName == series.Project &&
            b.MetricType == series.MetricType);
        return series.ReloadType == null
            ? metrics.Where(b => b.ReloadType == null || b.ReloadType == "")
            : metrics.Where(b => b.ReloadType == series.ReloadType);
    }
}
