using System.Diagnostics;
using System.Net.Http.Json;
using Orchestrator.Models;

namespace Orchestrator.Clients;

public sealed class TelemetryApiClient(HttpClient httpClient, ILogger<TelemetryApiClient> logger)
    : ITelemetryApiClient
{
    public async Task PostReadingAsync(MachineReading reading, CancellationToken ct = default)
    {
        var traceId = Activity.Current?.TraceId.ToString();

        var metrics = new[]
        {
            new ApiReading(reading.MachineId, "temperature", reading.Temperature, "celsius", reading.Timestamp, traceId),
            new ApiReading(reading.MachineId, "pressure",    reading.Pressure,    "psi",     reading.Timestamp, traceId),
            new ApiReading(reading.MachineId, "rpm",         reading.Rpm,         "rpm",     reading.Timestamp, traceId),
        };

        foreach (var metric in metrics)
        {
            var response = await httpClient.PostAsJsonAsync("/api/readings", metric, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("API returned {StatusCode} posting {Metric} for {MachineId}",
                    (int)response.StatusCode, metric.Metric, metric.MachineId);
        }
    }

    // Matches TelemetryApi.Models.MachineReading schema
    private sealed record ApiReading(
        string MachineId,
        string Metric,
        double Value,
        string Unit,
        DateTime Timestamp,
        string? TraceId,
        string Status = "nominal"
    );
}