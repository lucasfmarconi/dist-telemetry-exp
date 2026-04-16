using System.Diagnostics;
using MQTTnet.Packets;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace MachineSimulator.Telemetry;

public static class MqttPropagation
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static void Inject(Activity activity, List<MqttUserProperty> userProperties)
    {
        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            userProperties,
            static (props, key, value) => props.Add(new MqttUserProperty(key, value))
        );
    }

    public static PropagationContext Extract(List<MqttUserProperty>? userProperties)
    {
        if (userProperties is null or { Count: 0 })
            return default;

        return Propagator.Extract(
            default,
            userProperties,
            static (props, key) => props
                .Where(p => p.Name == key)
                .Select(p => p.Value)
        );
    }
}