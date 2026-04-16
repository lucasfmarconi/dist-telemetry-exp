using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TelemetryApi.Data;
using TelemetryApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

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
            });
    });

builder.Services.AddSingleton<IMachineReadingRepository, LiteDbRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

Log.Information("TelemetryApi starting — LiteDb={LiteDb}, OTLP={Otlp}, Seq={Seq}",
    app.Configuration["LiteDb:ConnectionString"],
    otlpEndpoint,
    app.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<TraceCorrelationMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();