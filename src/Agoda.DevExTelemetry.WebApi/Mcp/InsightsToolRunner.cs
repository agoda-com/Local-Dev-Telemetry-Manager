using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Output;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

/// <summary>The structured content of every insights tool result.</summary>
public sealed record InsightsResultMeta(
    [property: JsonPropertyName("total_rows")] int TotalRows,
    [property: JsonPropertyName("returned")] int Returned,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("next_cursor")] string? NextCursor,
    [property: JsonPropertyName("export_url")] string? ExportUrl,
    [property: JsonPropertyName("window")] InsightsResultWindow? Window);

public sealed record InsightsResultWindow(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To);

/// <summary>
/// Runs a dataset for an MCP tool call and wraps the page in the standard envelope:
/// content[0] is the one-line summary, content[1] is the CSV (or JSON with <c>format=json</c>),
/// and the structured content is <see cref="InsightsResultMeta"/>.
/// </summary>
public interface IInsightsToolRunner
{
    Task<CallToolResult> RunAsync(
        string dataset,
        InsightsArgs args,
        int? limit,
        string? cursor,
        string? format,
        CancellationToken cancellationToken);

    /// <summary>The <c>/api/export/{dataset}.csv</c> URL for these arguments.</summary>
    string ExportUrl(string dataset, InsightsArgs args);
}

[RegisterPerRequest]
public sealed class InsightsToolRunner : IInsightsToolRunner
{
    private readonly IInsightsDatasetResolver _datasets;
    private readonly IInsightsOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InsightsToolRunner(
        IInsightsDatasetResolver datasets,
        IInsightsOptions options,
        IHttpContextAccessor httpContextAccessor)
    {
        _datasets = datasets;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<CallToolResult> RunAsync(
        string dataset,
        InsightsArgs args,
        int? limit,
        string? cursor,
        string? format,
        CancellationToken cancellationToken)
    {
        try
        {
            var outputFormat = new InsightsArgs().With("format", format)
                .GetChoice("format", TableFormatter.CSV, TableFormatter.JSON) ?? TableFormatter.CSV;
            if (limit is <= 0)
                throw new InsightsArgumentException("limit", "must be at least 1");
            var pageSize = Math.Min(limit ?? _options.DefaultRows, _options.MaxRows);

            var source = _datasets.Find(dataset)
                ?? throw new InvalidOperationException($"Dataset '{dataset}' is not registered");
            foreach (var paging in DatasetNames.PagingArgs) args.Without(paging);

            var table = await source.QueryAsync(args, new PageRequest(pageSize, cursor), cancellationToken);

            var truncated = table.NextCursor != null;
            var exportUrl = ExportUrl(dataset, args);
            var meta = new InsightsResultMeta(
                table.TotalRows,
                table.Rows.Count,
                truncated,
                table.NextCursor,
                exportUrl,
                table.Window == null
                    ? null
                    : new InsightsResultWindow(
                        TableFormatter.FormatTimestamp(table.Window.From),
                        TableFormatter.FormatTimestamp(table.Window.To)));

            var body = outputFormat == TableFormatter.JSON ? TableFormatter.ToJson(table) : TableFormatter.ToCsv(table);

            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = Summarise(dataset, table, meta) },
                    new TextContentBlock { Text = body }
                ],
                StructuredContent = JsonSerializer.SerializeToElement(meta)
            };
        }
        catch (InsightsArgumentException ex)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"Invalid argument {ex.Message}" }]
            };
        }
    }

    public string ExportUrl(string dataset, InsightsArgs args)
    {
        var baseUrl = _options.PublicBaseUrl;
        if (baseUrl == null)
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            baseUrl = request == null ? string.Empty : $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        var query = args.ToQueryString(DatasetNames.PagingArgs);
        return $"{baseUrl}/api/export/{dataset}.csv" + (query.Length > 0 ? "?" + query : string.Empty);
    }

    private static string Summarise(string dataset, Table table, InsightsResultMeta meta)
    {
        var parts = new List<string> { $"{dataset}: {meta.Returned} of {meta.TotalRows} rows" };
        if (table.Window != null) parts.Add($"window {table.Window}");
        if (!string.IsNullOrWhiteSpace(table.Applied)) parts.Add(table.Applied);
        if (meta.Truncated) parts.Add("truncated: pass cursor=next_cursor for more, or fetch export_url");
        parts.AddRange(table.Notes.Select(n => "note: " + n));
        return string.Join(" | ", parts);
    }
}
