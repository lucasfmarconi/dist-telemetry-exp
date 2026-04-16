using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orchestrator.Clients;
using Orchestrator.Services;

var builder = Host.CreateApplicationBuilder(args);

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4317";
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://api:8080";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "orchestrator", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource("Orchestrator.MQTT")
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
        })
        .AddConsoleExporter());

builder.Services.AddHttpClient<ITelemetryApiClient, TelemetryApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<TelemetryProcessor>();

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("Orchestrator starting — Mqtt:Host={MqttHost}, Api:BaseUrl={ApiUrl}, OTLP={Otlp}",
    builder.Configuration["Mqtt:Host"],
    apiBaseUrl,
    otlpEndpoint);

host.Run();