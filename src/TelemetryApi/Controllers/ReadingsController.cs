using Microsoft.AspNetCore.Mvc;
using TelemetryApi.Data;
using TelemetryApi.Models;

namespace TelemetryApi.Controllers;

[ApiController]
[Route("api/readings")]
public class ReadingsController : ControllerBase
{
    private readonly IMachineReadingRepository _repository;

    public ReadingsController(IMachineReadingRepository repository)
    {
        _repository = repository;
    }

    [HttpPost]
    public IActionResult Insert([FromBody] MachineReading reading)
    {
        var id = _repository.Insert(reading);
        return CreatedAtAction(nameof(GetByMachineId), new { machineId = reading.MachineId }, new { id, reading });
    }

    [HttpGet]
    public IActionResult GetAll(
        [FromQuery] string? machineId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var readings = _repository.Find(machineId, startDate, endDate);
        return Ok(readings);
    }

    [HttpGet("{machineId}")]
    public IActionResult GetByMachineId(string machineId)
    {
        var readings = _repository.Find(machineId).ToList();
        return readings.Any() ? Ok(readings) : NotFound();
    }
}