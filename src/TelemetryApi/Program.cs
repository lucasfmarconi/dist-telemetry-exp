using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TelemetryApi.Data;

var builder = WebApplication.CreateBuilder(args);

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "telemetry-api", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("TelemetryApi")
            .AddSource("TelemetryApi.LiteDB")
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(otlpEndpoint);
                otlpOptions.Protocol = OtlpExportProtocol.Grpc;
            })
            .AddConsoleExporter();
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