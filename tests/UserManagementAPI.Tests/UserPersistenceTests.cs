using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.Models;

namespace UserManagementAPI.Tests;

public sealed class UserPersistenceTests
{
    private static User NewUser(string email) => new()
    {
        FirstName = "Alice",
        LastName = "Martin",
        Email = email,
        Department = "IT"
    };

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(), $"user-management-tests-{Guid.NewGuid():N}.db");

        public AppDbContext CreateContext()
        {
            // Disable pooling so the test file can be safely deleted afterward.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_path};Pooling=False")
                .Options;

            return new AppDbContext(options);
        }

        public ValueTask DisposeAsync()
        {
            File.Delete(_path);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task MigrationsCreateDatabaseAndUserPersistsAcrossContexts()
    {
        await using var testDatabase = new SqliteTestDatabase();
        Guid userId;
        DateTime createdAt;

        await using (var db = testDatabase.CreateContext())
        {
            await db.Database.MigrateAsync();

            var user = NewUser(" Alice@Example.com ");
            db.Users.Add(user);
            await db.SaveChangesAsync();

            userId = user.Id;
            createdAt = user.CreatedAtUtc;

            Assert.Equal("Alice@Example.com", user.Email);
            Assert.Equal("ALICE@EXAMPLE.COM", user.NormalizedEmail);
            Assert.NotEqual(default(DateTime), createdAt);
            Assert.Equal(createdAt, user.UpdatedAtUtc);
        }

        // New DbContext + new connection, same physical SQLite file.
        await using (var db = testDatabase.CreateContext())
        {
            Assert.True(await db.Database.CanConnectAsync());

            var saved = await db.Users.SingleAsync(x => x.Id == userId);
            Assert.Equal("Alice@Example.com", saved.Email);
            Assert.Equal("ALICE@EXAMPLE.COM", saved.NormalizedEmail);
            Assert.Equal(createdAt, saved.CreatedAtUtc);
        }
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateNormalizedEmail()
    {
        await using var testDatabase = new SqliteTestDatabase();

        await using (var db = testDatabase.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Users.Add(NewUser("Alice@Example.com"));
            await db.SaveChangesAsync();
        }

        await using (var db = testDatabase.CreateContext())
        {
            db.Users.Add(NewUser(" alice@example.com "));

            var error = await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync());

            var sqliteError = Assert.IsType<SqliteException>(
                error.InnerException);

            // 19 = SQLITE_CONSTRAINT: rejected by the database itself.
            Assert.Equal(19, sqliteError.SqliteErrorCode);
            Assert.Contains("Users.NormalizedEmail", sqliteError.Message);
        }
    }

    [Fact]
    public async Task UpdateRefreshesTimestampButPreservesCreationTime()
    {
        await using var testDatabase = new SqliteTestDatabase();
        DateTime originalCreated;

        await using (var db = testDatabase.CreateContext())
        {
            await db.Database.MigrateAsync();
            var user = NewUser("alice@example.com");
            db.Users.Add(user);
            await db.SaveChangesAsync();
            originalCreated = user.CreatedAtUtc;
        }

        // Ensure UpdatedAtUtc advances even on low-resolution clocks.
        await Task.Delay(20);

        await using (var db = testDatabase.CreateContext())
        {
            var user = await db.Users.SingleAsync();
            user.Email = " Bob@Example.com ";
            user.CreatedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        await using (var db = testDatabase.CreateContext())
        {
            var saved = await db.Users.SingleAsync();

            Assert.Equal("Bob@Example.com", saved.Email);
            Assert.Equal("BOB@EXAMPLE.COM", saved.NormalizedEmail);
            Assert.Equal(originalCreated, saved.CreatedAtUtc);
            Assert.True(saved.UpdatedAtUtc > saved.CreatedAtUtc);
        }
    }

    [Fact]
    public async Task MigratingLegacyRecordBackfillsNormalizedEmailAndTimestamps()
    {
        await using var testDatabase = new SqliteTestDatabase();

        await using (var db = testDatabase.CreateContext())
        {
            // Reproduce an existing installation using the first migration.
            await db.Database.MigrateAsync("20261020220203_InitialCreate");

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Users" ("Id", "FirstName", "LastName", "Email", "Department")
                VALUES ('550e8400-e29b-41d4-a716-446655440000',
                        'Legacy', 'User', ' Legacy@Example.com ', 'IT');
                """);

            // Upgrade existing records using the second migration.
            await db.Database.MigrateAsync();
        }

        await using (var db = testDatabase.CreateContext())
        {
            var legacyUser = await db.Users.SingleAsync();
            Assert.Equal("LEGACY@EXAMPLE.COM", legacyUser.NormalizedEmail);
            Assert.NotEqual(default(DateTime), legacyUser.CreatedAtUtc);
            Assert.NotEqual(default(DateTime), legacyUser.UpdatedAtUtc);
        }
    }
}
