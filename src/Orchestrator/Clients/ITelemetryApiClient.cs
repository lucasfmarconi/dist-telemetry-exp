using Orchestrator.Models;

namespace Orchestrator.Clients;

public interface ITelemetryApiClient
{
    Task PostReadingAsync(MachineReading reading, CancellationToken ct = default);
}