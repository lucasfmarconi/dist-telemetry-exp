using TelemetryApi.Models;

namespace TelemetryApi.Data;

public interface IMachineReadingRepository
{
    Guid Insert(MachineReading reading);
    IEnumerable<MachineReading> Find(string? machineId = null, DateTime? startDate = null, DateTime? endDate = null);
}
