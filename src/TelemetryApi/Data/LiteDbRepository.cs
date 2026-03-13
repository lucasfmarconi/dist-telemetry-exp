using LiteDB;
using TelemetryApi.Models;

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
        var result = _collection.Insert(reading);
        return result.AsGuid;
    }

    public IEnumerable<MachineReading> Find(string? machineId = null, DateTime? startDate = null, DateTime? endDate = null)
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

    public void Dispose() => _database.Dispose();
}
