using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using TelemetryApi.Data;
using TelemetryApi.Models;

namespace TelemetryApi.Tests;

/// <summary>Minimal IConfiguration backed by a dictionary — avoids external package dependency.</summary>
file sealed class DictConfig : IConfiguration
{
    private readonly Dictionary<string, string?> _data;
    public DictConfig(Dictionary<string, string?> data) => _data = data;
    public string? this[string key] { get => _data.GetValueOrDefault(key); set { } }
    public IConfigurationSection GetSection(string key) => null!;
    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => null!;
}

public class LiteDbRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IConfiguration _config;

    public LiteDbRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"telemetry-test-{Guid.NewGuid()}.db");
        _config = new DictConfig(new Dictionary<string, string?> { ["LiteDb:ConnectionString"] = _dbPath });
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private LiteDbRepository CreateRepo() => new(_config);

    private static (List<Activity> Activities, ActivityListener Listener) CaptureActivities()
    {
        var activities = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "TelemetryApi.LiteDB",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);
        return (activities, listener);
    }

    // --- Subtask 3: initialization ---

    [Fact]
    public void Constructor_CreatesDbFileAtConfiguredPath()
    {
        using var repo = CreateRepo();
        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void Constructor_FallsBackToDefaultPath_WhenNoConfig()
    {
        var defaultDbPath = Path.GetFullPath("./data/telemetry.db");
        try
        {
            var emptyConfig = new DictConfig(new Dictionary<string, string?>());
            using var repo = new LiteDbRepository(emptyConfig);
            // should not throw
        }
        finally
        {
            if (File.Exists(defaultDbPath)) File.Delete(defaultDbPath);
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var repo = CreateRepo();
        repo.Dispose();
        repo.Dispose(); // must not throw
    }

    // --- Subtask 4: Insert ---

    [Fact]
    public void Insert_ReturnsNonEmptyGuid()
    {
        using var repo = CreateRepo();
        var reading = new MachineReading("machine-001", "temperature", 72.5, "celsius", "nominal", DateTime.UtcNow);
        var id = repo.Insert(reading);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void Insert_PersistsReadingToDb()
    {
        using var repo = CreateRepo();
        var reading = new MachineReading("machine-001", "temperature", 72.5, "celsius", "nominal", DateTime.UtcNow);
        repo.Insert(reading);
        var results = repo.Find(machineId: "machine-001").ToList();
        Assert.Single(results);
        Assert.Equal(72.5, results[0].Value);
    }

    [Fact]
    public void Insert_CreatesSpanWithCorrectNameAndKind()
    {
        var (activities, listener) = CaptureActivities();
        using (listener)
        using (var repo = CreateRepo())
        {
            repo.Insert(new MachineReading("machine-001", "temperature", 72.5, "celsius", "nominal", DateTime.UtcNow));
        }

        var span = Assert.Single(activities);
        Assert.Equal("litedb.insert MachineReadings", span.OperationName);
        Assert.Equal(ActivityKind.Internal, span.Kind);
    }

    [Fact]
    public void Insert_SpanHasRequiredDbTags()
    {
        var (activities, listener) = CaptureActivities();
        using (listener)
        using (var repo = CreateRepo())
        {
            repo.Insert(new MachineReading("machine-001", "temperature", 72.5, "celsius", "nominal", DateTime.UtcNow));
        }

        var span = Assert.Single(activities);
        Assert.Equal("litedb", span.GetTagItem("db.system"));
        Assert.Equal("insert", span.GetTagItem("db.operation"));
        Assert.Equal("telemetry", span.GetTagItem("db.name"));
        Assert.Equal("MachineReadings", span.GetTagItem("db.litedb.collection"));
    }

    // --- Subtask 5: Find ---

    [Fact]
    public void Find_NoFilters_ReturnsAllRecords()
    {
        using var repo = CreateRepo();
        repo.Insert(new MachineReading("machine-001", "temperature", 70.0, "celsius", "nominal", DateTime.UtcNow));
        repo.Insert(new MachineReading("machine-002", "pressure", 1.2, "psi", "nominal", DateTime.UtcNow));

        var results = repo.Find().ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Find_ByMachineId_ReturnsOnlyMatchingRecords()
    {
        using var repo = CreateRepo();
        repo.Insert(new MachineReading("machine-001", "temperature", 70.0, "celsius", "nominal", DateTime.UtcNow));
        repo.Insert(new MachineReading("machine-002", "pressure", 1.2, "psi", "nominal", DateTime.UtcNow));

        var results = repo.Find(machineId: "machine-001").ToList();
        Assert.Single(results);
        Assert.Equal("machine-001", results[0].MachineId);
    }

    [Fact]
    public void Find_ByDateRange_ReturnsOnlyRecordsInRange()
    {
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        using var repo = CreateRepo();
        repo.Insert(new MachineReading("machine-001", "temperature", 68.0, "celsius", "nominal", baseTime.AddHours(-2)));
        repo.Insert(new MachineReading("machine-001", "temperature", 72.0, "celsius", "nominal", baseTime));
        repo.Insert(new MachineReading("machine-001", "temperature", 76.0, "celsius", "nominal", baseTime.AddHours(2)));

        var results = repo.Find(startDate: baseTime.AddMinutes(-1), endDate: baseTime.AddMinutes(1)).ToList();
        Assert.Single(results);
        Assert.Equal(72.0, results[0].Value);
    }

    [Fact]
    public void Find_SpanHasRequiredDbTags()
    {
        var (activities, listener) = CaptureActivities();
        using (listener)
        using (var repo = CreateRepo())
        {
            repo.Find();
        }

        var span = Assert.Single(activities, a => a.OperationName == "litedb.find MachineReadings");
        Assert.Equal("litedb", span.GetTagItem("db.system"));
        Assert.Equal("find", span.GetTagItem("db.operation"));
        Assert.Equal("telemetry", span.GetTagItem("db.name"));
        Assert.Equal("MachineReadings", span.GetTagItem("db.litedb.collection"));
    }

    [Fact]
    public void Find_FilterCountTag_IsZeroWithNoFilters()
    {
        var (activities, listener) = CaptureActivities();
        using (listener)
        using (var repo = CreateRepo())
        {
            repo.Find();
        }

        var span = Assert.Single(activities);
        Assert.Equal(0, span.GetTagItem("db.litedb.filter.count"));
    }

    [Fact]
    public void Find_FilterCountTag_ReflectsActiveFilters()
    {
        var (activities, listener) = CaptureActivities();
        using (listener)
        using (var repo = CreateRepo())
        {
            repo.Find(machineId: "machine-001", startDate: DateTime.UtcNow);
        }

        var span = Assert.Single(activities);
        Assert.Equal(2, span.GetTagItem("db.litedb.filter.count"));
    }
}