namespace Agoda.DevExTelemetry.Core.Configs;

public class AssemblyInfo
{
    public string Version => typeof(AssemblyInfo).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
