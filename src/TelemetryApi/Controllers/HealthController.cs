using Microsoft.AspNetCore.Mvc;

namespace TelemetryApi.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        service = "telemetry-api",
        version = "1.0.0"
    });
}