using MachineSimulator.Services;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

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
        }));

builder.Services.AddHostedService<TelemetryPublisher>();

var host = builder.Build();

Log.Information("MachineSimulator starting — Mqtt:Host={MqttHost}, PublishInterval={Interval}s, OTLP={Otlp}, Seq={Seq}",
    builder.Configuration["Mqtt:Host"],
    builder.Configuration.GetValue("Mqtt:PublishIntervalSeconds", 5),
    otlpEndpoint,
    builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");

host.Run();