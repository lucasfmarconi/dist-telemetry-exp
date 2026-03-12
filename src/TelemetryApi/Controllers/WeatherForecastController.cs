using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TelemetryApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ActivitySource _source = new ActivitySource("TelemetryApi");

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        using var activity = _source.StartActivity("GetWeatherForecast");

        var aRange = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray();
        
        activity?.SetTag("p0", aRange[0].Summary);
        activity?.SetTag("p1", aRange[1].Summary);
        activity?.SetTag("p2", aRange[2].Summary);
        activity?.SetTag("p3", aRange[3].Summary);
        activity?.SetTag("p4", aRange[4].Summary);
        
        return aRange;
    }
}

public class WeatherForecast
{
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}