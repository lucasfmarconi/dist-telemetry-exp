using System.Diagnostics;
using LiteDB;
using TelemetryApi.Models;
using TelemetryApi.Telemetry;

namespace TelemetryApi.Data;

internal sealed class LiteDbRepository : IMachineReadingRepository, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<MachineReading> _collection;

    public LiteDbRepository(IConfiguration configuration)
    {
        var connectionString = configuration["LiteDb:ConnectionString"] ?? "./data/telemetry.db";
        var directory = Path.GetDirectoryName(connectionString);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _database = new LiteDatabase(connectionString);
        _collection = _database.GetCollection<MachineReading>("MachineReadings");
    }

    public Guid Insert(MachineReading reading)
    {
        using var activity = ActivitySources.LiteDb.StartActivity("litedb.insert MachineReadings");
        activity?.SetTag("db.system", "litedb");
        activity?.SetTag("db.operation", "insert");
        activity?.SetTag("db.name", "telemetry");
        activity?.SetTag("db.litedb.collection", "MachineReadings");

        try
        {
            var result = _collection.Insert(reading);
            return result.AsGuid;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public IEnumerable<MachineReading> Find(string? machineId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        using var activity = ActivitySources.LiteDb.StartActivity("litedb.find MachineReadings");
        activity?.SetTag("db.system", "litedb");
        activity?.SetTag("db.operation", "find");
        activity?.SetTag("db.name", "telemetry");
        activity?.SetTag("db.litedb.collection", "MachineReadings");

        var filterCount = (machineId is not null ? 1 : 0)
                        + (startDate is not null ? 1 : 0)
                        + (endDate is not null ? 1 : 0);
        activity?.SetTag("db.litedb.filter.count", filterCount);

        try
        {
            var query = _collection.Query();

            if (machineId is not null)
                query = query.Where(x => x.MachineId == machineId);

            if (startDate is not null)
                query = query.Where(x => x.Timestamp >= startDate);

            if (endDate is not null)
                query = query.Where(x => x.Timestamp <= endDate);

            return query.ToList();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public void Dispose() => _database.Dispose();
}
