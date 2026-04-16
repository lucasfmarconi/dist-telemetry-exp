namespace Orchestrator.Models;

public record MachineReading(
    string MachineId,
    DateTime Timestamp,
    double Temperature,
    double Pressure,
    double Rpm
);