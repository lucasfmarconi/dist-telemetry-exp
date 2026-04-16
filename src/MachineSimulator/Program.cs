using MachineSimulator.Services;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "machine-simulator", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource("MachineSimulator.MQTT")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
        })
        .AddConsoleExporter());

builder.Services.AddHostedService<TelemetryPublisher>();

var host = builder.Build();
host.Run();