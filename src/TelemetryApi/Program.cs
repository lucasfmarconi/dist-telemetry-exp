using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelemetryApi.Data;

var builder = WebApplication.CreateBuilder(args);

var tracingOtlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];

builder.Services.AddOpenTelemetry().ConfigureResource(resource =>
        resource.AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing =>
    {
        tracing.AddSource("TelemetryApi");
        tracing.AddAspNetCoreInstrumentation();
        if (tracingOtlpEndpoint != null)
        {
            tracing.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(tracingOtlpEndpoint);
            });
        }
        else
        {
            tracing.AddConsoleExporter();
        }
    });

builder.Services.AddSingleton<IMachineReadingRepository, LiteDbRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();