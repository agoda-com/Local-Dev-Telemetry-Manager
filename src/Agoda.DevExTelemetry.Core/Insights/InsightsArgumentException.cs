namespace Agoda.DevExTelemetry.Core.Insights;

/// <summary>
/// A bad argument to an insights tool or export. MCP tools report it as a tool error and the
/// export endpoint returns 400, with this message, so the agent can correct the call.
/// </summary>
public sealed class InsightsArgumentException : Exception
{
    public string ArgumentName { get; }

    public InsightsArgumentException(string argumentName, string message)
        : base($"{argumentName}: {message}")
    {
        ArgumentName = argumentName;
    }
}
