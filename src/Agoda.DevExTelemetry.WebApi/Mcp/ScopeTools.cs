using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Agoda.DevExTelemetry.Core.Insights;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Agoda.DevExTelemetry.WebApi.Mcp;

[McpServerToolType]
public sealed class ScopeTools
{
    private readonly IInsightsToolRunner _runner;

    public ScopeTools(IInsightsToolRunner runner)
    {
        _runner = runner;
    }

    [McpServerTool(Name = DatasetNames.LIST_SCOPES, Title = "List scopes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        What is in the store: every repository, project, branch, platform, test runner and metric type, one row per value.
        Start here to find the names to pass as repository, project, branch, test_runner or metric_type.
        Columns: kind, value. kind is one of repository, project, branch, platform, test_runner, metric_type.
        Filters: kind.
        """)]
    public Task<CallToolResult> ListScopes(
        [Description("Only this kind: repository | project | branch | platform | test_runner | metric_type.")] string? kind = null,
        [Description(ToolArgs.LIMIT)] int? limit = null,
        [Description(ToolArgs.CURSOR)] string? cursor = null,
        [Description(ToolArgs.FORMAT)] string? format = null,
        CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            DatasetNames.LIST_SCOPES,
            new InsightsArgs().With("kind", kind),
            limit, cursor, format, cancellationToken);
}
