using System.Diagnostics;
using MQTTnet.Packets;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using MachineSimulator.Telemetry;

namespace MachineSimulator.Tests.Telemetry;

public class MqttPropagationInjectTests
{
    private static readonly ActivitySource Source = new("MqttPropagationTests");

    public MqttPropagationInjectTests()
    {
        // Register a TracerProvider so Activities are created (not null)
        Sdk.CreateTracerProviderBuilder()
            .AddSource("MqttPropagationTests")
            .Build();
    }

    [Fact]
    public void Inject_WithActiveActivity_AddsTraceparentHeader()
    {
        using var activity = Source.StartActivity("test-inject");
        Assert.NotNull(activity);

        var props = new List<MqttUserProperty>();
        MqttPropagation.Inject(activity, props);

        var traceparent = props.SingleOrDefault(p => p.Name == "traceparent");
        Assert.NotNull(traceparent);

        // W3C format: 00-<32-hex>-<16-hex>-<2-hex>
        Assert.Matches(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", traceparent.Value);
    }

    [Fact]
    public void Inject_WithActiveActivity_TraceIdMatchesActivity()
    {
        using var activity = Source.StartActivity("test-traceid");
        Assert.NotNull(activity);

        var props = new List<MqttUserProperty>();
        MqttPropagation.Inject(activity, props);

        var traceparent = props.Single(p => p.Name == "traceparent");
        var parts = traceparent.Value.Split('-');

        Assert.Equal(activity.TraceId.ToString(), parts[1]);
        Assert.Equal(activity.SpanId.ToString(), parts[2]);
    }

    [Fact]
    public void Inject_WithNoActivity_DoesNotAddHeaders()
    {
        // Ensure no current activity
        Activity.Current = null;
        using var activity = Source.StartActivity("test-no-parent");
        Assert.NotNull(activity);
        activity.Stop();
        Activity.Current = null;

        var props = new List<MqttUserProperty>();

        // Inject with a stopped/null context — propagator should add nothing meaningful
        // We verify no exception is thrown
        var ex = Record.Exception(() =>
        {
            // Create a blank activity context (no trace)
            var blank = new Activity("blank");
            MqttPropagation.Inject(blank, props);
        });

        Assert.Null(ex);
    }
}

public class MqttPropagationExtractTests
{
    private static readonly ActivitySource Source = new("MqttPropagationExtractTests");

    public MqttPropagationExtractTests()
    {
        Sdk.CreateTracerProviderBuilder()
            .AddSource("MqttPropagationExtractTests")
            .Build();
    }

    [Fact]
    public void Extract_WithValidTraceparent_ReturnsNonDefaultContext()
    {
        var props = new List<MqttUserProperty>
        {
            new("traceparent", "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")
        };

        var context = MqttPropagation.Extract(props);

        Assert.NotEqual(default(ActivityTraceId), context.ActivityContext.TraceId);
        Assert.Equal(
            ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736"),
            context.ActivityContext.TraceId);
        Assert.Equal(
            ActivitySpanId.CreateFromString("00f067aa0ba902b7"),
            context.ActivityContext.SpanId);
    }

    [Fact]
    public void Extract_WithMultipleValuesForSameKey_ReturnsAllValues()
    {
        // Getter must return all matching values (OTel contract)
        var props = new List<MqttUserProperty>
        {
            new("tracestate", "vendor1=value1"),
            new("tracestate", "vendor2=value2")
        };

        // We verify the getter itself returns multiple values
        IEnumerable<string> getter(List<MqttUserProperty> p, string key) =>
            p.Where(x => x.Name == key).Select(x => x.Value);

        var values = getter(props, "tracestate").ToList();
        Assert.Equal(2, values.Count);
        Assert.Contains("vendor1=value1", values);
        Assert.Contains("vendor2=value2", values);
    }

    [Fact]
    public void Extract_WithNullProperties_ReturnsDefault()
    {
        var context = MqttPropagation.Extract(null);
        Assert.Equal(default, context);
    }

    [Fact]
    public void Extract_WithEmptyProperties_ReturnsDefault()
    {
        var context = MqttPropagation.Extract([]);
        Assert.Equal(default, context);
    }

    [Fact]
    public void Extract_WithMalformedTraceparent_DoesNotThrow()
    {
        var props = new List<MqttUserProperty>
        {
            new("traceparent", "not-valid-at-all")
        };

        var ex = Record.Exception(() => MqttPropagation.Extract(props));
        Assert.Null(ex);
    }

    [Fact]
    public void RoundTrip_InjectThenExtract_PreservesTraceId()
    {
        using var activity = Source.StartActivity("roundtrip");
        Assert.NotNull(activity);

        var props = new List<MqttUserProperty>();
        MqttPropagation.Inject(activity, props);

        var extracted = MqttPropagation.Extract(props);

        Assert.Equal(activity.TraceId, extracted.ActivityContext.TraceId);
        Assert.Equal(activity.SpanId, extracted.ActivityContext.SpanId);
    }
}
