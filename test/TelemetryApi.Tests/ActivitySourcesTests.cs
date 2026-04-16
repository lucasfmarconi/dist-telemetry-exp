using System.Diagnostics;
using TelemetryApi.Telemetry;

namespace TelemetryApi.Tests;

public class ActivitySourcesTests
{
    [Fact]
    public void LiteDb_IsNotNull()
    {
        Assert.NotNull(ActivitySources.LiteDb);
    }

    [Fact]
    public void LiteDb_HasCorrectName()
    {
        Assert.Equal("TelemetryApi.LiteDB", ActivitySources.LiteDb.Name);
    }

    [Fact]
    public void LiteDb_CanStartAndStopActivity()
    {
        var recorded = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "TelemetryApi.LiteDB",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => recorded.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = ActivitySources.LiteDb.StartActivity("test"))
        {
            Assert.NotNull(activity);
        }

        Assert.Single(recorded);
        Assert.Equal("test", recorded[0].OperationName);
    }
}