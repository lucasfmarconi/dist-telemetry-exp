using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TelemetryApi.Data;
using TelemetryApi.Models;

namespace TelemetryApi.Controllers;

[ApiController]
[Route("api/readings")]
public class ReadingsController(IMachineReadingRepository repository, ILogger<ReadingsController> logger)
    : ControllerBase
{
    [HttpPost]
    public IActionResult Insert([FromBody] MachineReading reading)
    {
        var id = repository.Insert(reading);
        logger.LogInformation("Inserted reading for {MachineId} metric={Metric} value={Value} TraceId={TraceId}",
            reading.MachineId, reading.Metric, reading.Value, Activity.Current?.TraceId);
        return CreatedAtAction(nameof(GetByMachineId), new { machineId = reading.MachineId }, new { id, reading });
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? machineId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var readings = repository.Find(machineId, startDate, endDate).ToList();
        logger.LogDebug("GetAll returned {Count} readings for machineId={MachineId}", readings.Count, machineId);
        return Ok(readings);
    }

    [HttpGet("{machineId}")]
    public IActionResult GetByMachineId(string machineId)
    {
        var readings = repository.Find(machineId).ToList();
        return readings.Any() ? Ok(readings) : NotFound();
    }
}