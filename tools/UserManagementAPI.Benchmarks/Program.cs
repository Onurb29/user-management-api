using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

// Synthetic local SQLite query/materialization measurements, not HTTP throughput.
const int iterations = 15;
var measurements = new List<object>();
foreach (var size in new[] { 1_000, 10_000 })
{
    var path = Path.Combine(Path.GetTempPath(), $"users-benchmark-{Guid.NewGuid():N}.db");
    try
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        for (var n = 0; n < size; n++)
            db.Users.Add(new User { FirstName = $"User{n:D5}", LastName = $"Family{n % 100:D3}",
                Email = $"synthetic{n}@example.com", Department = n % 2 == 0 ? "IT" : "HR" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new UserService(db);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Users_LastName_FirstName_Id");
        var planBefore = await QueryPlan(db);
        var baseline = await Measure(async () => (await db.Users.AsNoTracking()
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync()).Count);
        var pagedBeforeIndex = await Measure(async () => (await service.GetAllAsync()).Count);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IX_Users_LastName_FirstName_Id ON Users (LastName, FirstName, Id)");
        var planAfter = await QueryPlan(db);
        var pagedWithIndex = await Measure(async () => (await service.GetAllAsync()).Count);
        measurements.Add(new { rows = size, baseline, pagedBeforeIndex, pagedWithIndex, planBefore, planAfter });
    }
    finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    capturedAtUtc = DateTimeOffset.UtcNow,
    runtime = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    iterations,
    scope = "Local SQLite query and materialization, 2 warmups, 15 samples, median elapsed and process allocated bytes. No HTTP/network/concurrency throughput claims.",
    measurements
}, new JsonSerializerOptions { WriteIndented = true }));

static async Task<object> Measure(Func<Task<int>> query)
{
    for (var n = 0; n < 2; n++) await query();
    var times = new List<double>();
    var allocations = new List<long>();
    var count = 0;
    for (var n = 0; n < iterations; n++)
    {
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        count = await query();
        times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        allocations.Add(GC.GetTotalAllocatedBytes(precise: true) - before);
    }
    times.Sort(); allocations.Sort();
    return new { returnedRows = count, medianMilliseconds = times[iterations / 2], medianAllocatedBytes = allocations[iterations / 2] };
}

static async Task<string[]> QueryPlan(AppDbContext db)
{
    await db.Database.OpenConnectionAsync();
    try
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT Id, FirstName, LastName, Email, Department FROM Users ORDER BY LastName, FirstName, Id LIMIT 25";
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync()) lines.Add(reader.GetString(3));
        return lines.ToArray();
    }
    finally { await db.Database.CloseConnectionAsync(); }
}
