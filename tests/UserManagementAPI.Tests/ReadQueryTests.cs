using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UserManagementAPI.Data;
using UserManagementAPI.Models;
using UserManagementAPI.Services;
using Xunit;

namespace UserManagementAPI.Tests;

public sealed class ReadQueryTests
{
    [Fact]
    public async Task PagedReadProjectsInSqlUsesSortIndexAndDoesNotTrackEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var capture = new QueryCapture();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(capture).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        db.Users.Add(new User { FirstName = "Alice", LastName = "Martin", Email = "alice@example.com", Department = "IT" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var users = await new UserService(db).GetAllAsync();
        Assert.Single(users);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Contains("ORDER BY", capture.Sql);
        Assert.Contains("LIMIT", capture.Sql);
        Assert.Contains("OFFSET", capture.Sql);
        Assert.DoesNotContain("NormalizedEmail", capture.Sql);
        Assert.DoesNotContain("CreatedAtUtc", capture.Sql);
        Assert.DoesNotContain("UpdatedAtUtc", capture.Sql);
        Assert.Contains(capture.Parameters, p => Equals(p.Value, 25));

        await using var plan = connection.CreateCommand();
        plan.CommandText = "EXPLAIN QUERY PLAN " + capture.Sql;
        foreach (var (name, value) in capture.Parameters) plan.Parameters.AddWithValue(name, value);
        await using var reader = await plan.ExecuteReaderAsync();
        var details = new List<string>();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.Contains(details, line => line.Contains("USING INDEX IX_Users_LastName_FirstName_Id"));
        Assert.DoesNotContain(details, line => line.Contains("TEMP B-TREE"));
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public string Sql { get; private set; } = string.Empty;
        public (string Name, object Value)[] Parameters { get; private set; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Sql = command.CommandText;
            Parameters = command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value!)).ToArray();
            return ValueTask.FromResult(result);
        }
    }
}
