using Agoda.IoC.Core;

namespace Agoda.DevExTelemetry.Core.Insights;

[RegisterPerRequest]
public sealed class InsightsDatasetResolver : IInsightsDatasetResolver
{
    private readonly IReadOnlyDictionary<string, IInsightsDataset> _datasets;

    public InsightsDatasetResolver(IEnumerable<IInsightsDataset> datasets)
    {
        _datasets = datasets.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IInsightsDataset? Find(string name) => _datasets.GetValueOrDefault(name);

    public IReadOnlyList<string> Names => _datasets.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
}
