namespace TelemetryApi.Models;

public record MachineReading(
    string MachineId,
    string Metric,
    double Value,
    string Unit,
    string Status,
    DateTime Timestamp,
    string? TraceId = null
)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
