using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.Core.Insights.Output;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Agoda.DevExTelemetry.WebApi.Controllers;

/// <summary>
/// <c>GET /api/export/{dataset}.csv</c>: the full result of an MCP data or analysis tool as streamed CSV.
/// Takes the same query parameters as the tool (snake_case), has no row cap beyond <c>Export:MaxRows</c>
/// (or <c>limit</c>, if given), and reads the dataset page by page so memory stays flat.
/// </summary>
[Route("api/export")]
[ApiController]
public class ExportController : ControllerBase
{
    private const int EXPORT_PAGE_SIZE = 5000;

    private readonly IInsightsDatasetResolver _datasets;
    private readonly IInsightsOptions _options;

    public ExportController(IInsightsDatasetResolver datasets, IInsightsOptions options)
    {
        _datasets = datasets;
        _options = options;
    }

    [HttpGet("{dataset}.csv")]
    public async Task Export(string dataset, CancellationToken cancellationToken)
    {
        var source = _datasets.Find(dataset);
        if (source == null)
        {
            await WritePlainAsync(StatusCodes.Status404NotFound,
                $"Unknown dataset '{dataset}'. Datasets: {string.Join(", ", _datasets.Names)}", cancellationToken);
            return;
        }

        var args = InsightsArgs.FromPairs(Request.Query.Select(q =>
            new KeyValuePair<string, string?>(q.Key, q.Value.ToString())));

        Table firstPage;
        int cap;
        try
        {
            cap = Math.Min(args.GetInt("limit") ?? _options.ExportMaxRows, _options.ExportMaxRows);
            foreach (var paging in DatasetNames.PagingArgs) args.Without(paging);
            firstPage = await source.QueryAsync(args, new PageRequest(Math.Min(EXPORT_PAGE_SIZE, cap), null), cancellationToken);
        }
        catch (InsightsArgumentException ex)
        {
            await WritePlainAsync(StatusCodes.Status400BadRequest, $"Invalid argument {ex.Message}", cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{source.Name}.csv\"";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), 64 * 1024);
        TableFormatter.WriteCsvHeader(writer, firstPage.Columns);

        var written = 0;
        var page = firstPage;
        while (true)
        {
            foreach (var row in page.Rows)
            {
                if (written >= cap) break;
                TableFormatter.WriteCsvRow(writer, row);
                written++;
            }

            if (page.NextCursor == null || written >= cap) break;
            await writer.FlushAsync(cancellationToken);
            page = await source.QueryAsync(args,
                new PageRequest(Math.Min(EXPORT_PAGE_SIZE, cap - written), page.NextCursor), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }

    private async Task WritePlainAsync(int status, string message, CancellationToken cancellationToken)
    {
        Response.StatusCode = status;
        Response.ContentType = "text/plain; charset=utf-8";
        await Response.WriteAsync(message, cancellationToken);
    }
}
