using Agoda.DevExTelemetry.McpTests.Infrastructure;
using ModelContextProtocol.Client;

namespace Agoda.DevExTelemetry.McpTests;

/// <summary>Helpers for the build analysis fixtures.</summary>
internal static class BuildAnalysisTestSupport
{
    /// <summary>
    /// Runs an evidence call and follows its cursors, so a small <c>Mcp:MaxRows</c> does not hide rows.
    /// Fails if the call errors.
    /// </summary>
    public static async Task<List<CsvRow>> EvidenceRowsAsync(this McpClient client, string evidence)
    {
        var rows = new List<CsvRow>();
        var call = evidence;
        for (var guard = 0; guard < 1_000; guard++)
        {
            var result = await client.CallEvidenceAsync(call);
            if (result.IsError) throw new InvalidOperationException(result.ErrorText);
            rows.AddRange(result.Rows);
            if (result.NextCursor == null) return rows;
            call = evidence + " cursor=" + result.NextCursor;
        }
        throw new InvalidOperationException("Cursor never ended");
    }
}
