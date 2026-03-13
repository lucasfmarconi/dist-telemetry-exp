using System.Diagnostics;

namespace TelemetryApi.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource LiteDb = new("TelemetryApi.LiteDB", "1.0.0");
}
