using System.Globalization;
using Agoda.IoC.Core;
using Microsoft.Extensions.Configuration;

namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>Settings for the MCP server and the CSV exports, read from configuration once.</summary>
public interface IInsightsOptions
{
    /// <summary><c>Mcp:Enabled</c>, default true. When false, <c>/mcp</c> is not mapped.</summary>
    bool McpEnabled { get; }

    /// <summary><c>Mcp:DefaultRows</c>, default 100: rows per tool call when <c>limit</c> is not given.</summary>
    int DefaultRows { get; }

    /// <summary><c>Mcp:MaxRows</c>, default 500: the most rows one tool call returns.</summary>
    int MaxRows { get; }

    /// <summary><c>Mcp:PublicBaseUrl</c>: base for <c>export_url</c>; the request host is used when unset.</summary>
    string? PublicBaseUrl { get; }

    /// <summary><c>Export:MaxRows</c>, default 1,000,000: hard ceiling for one export.</summary>
    int ExportMaxRows { get; }

    /// <summary><c>DataRetention:RetentionDays</c>, default 90: <c>days</c> is capped at this.</summary>
    int RetentionDays { get; }
}

[RegisterSingleton]
public sealed class InsightsOptions : IInsightsOptions
{
    public InsightsOptions(IConfiguration configuration)
    {
        McpEnabled = !bool.TryParse(configuration["Mcp:Enabled"], out var enabled) || enabled;
        DefaultRows = ReadInt(configuration, "Mcp:DefaultRows", 100);
        MaxRows = ReadInt(configuration, "Mcp:MaxRows", 500);
        PublicBaseUrl = string.IsNullOrWhiteSpace(configuration["Mcp:PublicBaseUrl"])
            ? null
            : configuration["Mcp:PublicBaseUrl"]!.TrimEnd('/');
        ExportMaxRows = ReadInt(configuration, "Export:MaxRows", 1_000_000);
        RetentionDays = ReadInt(configuration, "DataRetention:RetentionDays", 90);
    }

    public bool McpEnabled { get; }
    public int DefaultRows { get; }
    public int MaxRows { get; }
    public string? PublicBaseUrl { get; }
    public int ExportMaxRows { get; }
    public int RetentionDays { get; }

    private static int ReadInt(IConfiguration configuration, string key, int defaultValue) =>
        int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v
            : defaultValue;
}
