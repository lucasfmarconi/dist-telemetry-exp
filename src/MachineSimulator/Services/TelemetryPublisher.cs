using System.Diagnostics;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MachineSimulator.Models;
using MachineSimulator.Telemetry;

namespace MachineSimulator.Services;

public class TelemetryPublisher(ILogger<TelemetryPublisher> logger, IConfiguration configuration)
    : BackgroundService
{
    private IMqttClient _mqttClient = new MqttClientFactory().CreateMqttClient();

    public static readonly ActivitySource ActivitySource = new("MachineSimulator.MQTT", "1.0.0");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TelemetryPublisher starting");

        var mqttHost = configuration["Mqtt:Host"] ?? "localhost";
        
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttHost, 1883)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithClientId($"machine-simulator-{Guid.NewGuid()}")
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

        var publishInterval = TimeSpan.FromSeconds(
            configuration.GetValue("Mqtt:PublishIntervalSeconds", 5));

        const string machineId = "machine-001";
        const string topic = $"machines/{machineId}/telemetry";

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(publishInterval, stoppingToken);

            var reading = new MachineReading(
                MachineId: machineId,
                Timestamp: DateTime.UtcNow,
                Temperature: Random.Shared.NextDouble() * 100,
                Pressure: Random.Shared.NextDouble() * 50,
                Rpm: Random.Shared.NextDouble() * 5000
            );

            var payload = JsonSerializer.SerializeToUtf8Bytes(reading);
            var userProperties = new List<MqttUserProperty>();

            using var activity = ActivitySource.StartActivity("mqtt.publish " + topic, ActivityKind.Producer);

            if (activity is not null)
            {
                MqttPropagation.Inject(activity, userProperties);
                activity.SetTag("messaging.system", "mqtt");
                activity.SetTag("messaging.destination", topic);
                activity.SetTag("messaging.operation", "publish");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();

            message.UserProperties = userProperties;

            await _mqttClient.PublishAsync(message, stoppingToken);
            logger.LogInformation("Published reading for {MachineId}: T={Temperature:F1} P={Pressure:F1} RPM={Rpm:F0} TraceId={TraceId}",
                reading.MachineId, reading.Temperature, reading.Pressure, reading.Rpm, activity?.TraceId);
        }

        if (_mqttClient.IsConnected)
        {
            logger.LogInformation("Disconnecting MQTT client");
            await _mqttClient.DisconnectAsync(cancellationToken: stoppingToken);
        }

        logger.LogInformation("TelemetryPublisher stopped");
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if(_mqttClient.IsConnected)
            _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        
        _mqttClient.Dispose();
        return base.StopAsync(cancellationToken);
    }
}