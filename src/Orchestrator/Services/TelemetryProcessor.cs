using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Orchestrator.Clients;
using Orchestrator.Models;
using Orchestrator.Telemetry;

namespace Orchestrator.Services;

public class TelemetryProcessor(
    ILogger<TelemetryProcessor> logger,
    IConfiguration configuration,
    ITelemetryApiClient apiClient)
    : BackgroundService
{
    public static readonly ActivitySource ActivitySource = new("Orchestrator.MQTT", "1.0.0");

    private readonly IMqttClient _mqttClient = new MqttClientFactory().CreateMqttClient();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TelemetryProcessor starting");

        var mqttHost = configuration["Mqtt:Host"] ?? "localhost";

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttHost, 1883)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithClientId($"orchestrator-{Guid.NewGuid()}")
            .Build();

        _mqttClient.DisconnectedAsync += async args =>
        {
            if (stoppingToken.IsCancellationRequested)
                return;

            logger.LogWarning("MQTT disconnected: {Reason}. Reconnecting...", args.Reason);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            try
            {
                await _mqttClient.ConnectAsync(options, stoppingToken);
                logger.LogInformation("MQTT reconnected to {Host}", mqttHost);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MQTT reconnect failed");
            }
        };

        _mqttClient.ApplicationMessageReceivedAsync += async args =>
        {
            var topic = args.ApplicationMessage.Topic;

            try
            {
                var parentContext = MqttPropagation.Extract(args.ApplicationMessage.UserProperties);

                using var activity = ActivitySource.StartActivity(
                    "mqtt.process " + topic,
                    ActivityKind.Consumer,
                    parentContext.ActivityContext);

                activity?.SetTag("messaging.system", "mqtt");
                activity?.SetTag("messaging.destination", topic);
                activity?.SetTag("messaging.operation", "process");

                var reading = JsonSerializer.Deserialize<MachineReading>(Encoding.UTF8.GetString(args.ApplicationMessage.Payload));

                if (reading is not null)
                {
                    logger.LogInformation(
                        "Received reading from {MachineId}: T={Temperature:F1} P={Pressure:F1} RPM={Rpm:F0}",
                        reading.MachineId, reading.Temperature, reading.Pressure, reading.Rpm);

                    await apiClient.PostReadingAsync(reading, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message on topic {Topic}", topic);
            }

            await Task.CompletedTask;
        };

        var retries = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _mqttClient.ConnectAsync(options, stoppingToken);
                logger.LogInformation("MQTT connected to {Host}", mqttHost);
                break;
            }
            catch (Exception ex) when (retries < 5)
            {
                retries++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retries));
                logger.LogWarning(ex, "MQTT connection attempt {Retry} failed. Retrying in {Delay}s", retries, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }

        await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("machines/+/telemetry", MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), stoppingToken);

        logger.LogInformation("Subscribed to machines/+/telemetry");

        await Task.Delay(Timeout.Infinite, stoppingToken);

        logger.LogInformation("TelemetryProcessor stopped");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_mqttClient.IsConnected)
            _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);

        _mqttClient.Dispose();
        return base.StopAsync(cancellationToken);
    }
}