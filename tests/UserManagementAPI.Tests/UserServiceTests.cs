using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.Models;
using UserManagementAPI.Services;
using Xunit;

namespace UserManagementAPI.Tests;

public sealed class UserServiceTests
{
    private static UserDetails Details(
        string email = "alice@example.com") =>
        new("Alice", "Martin", email, "IT");

    private static User NewUser(
        string email,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Alice",
            LastName = "Martin",
            Email = email,
            Department = "IT"
        };

    [Fact]
    public async Task GetById_ReturnsExistingUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = NewUser("alice@example.com");

        database.Db.Users.Add(user);
        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var result = await database.Service.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("Alice", result.FirstName);
        Assert.Equal("Martin", result.LastName);
        Assert.Equal("alice@example.com", result.Email);
        Assert.Equal("IT", result.Department);
    }

    [Fact]
    public async Task GetById_ReturnsNullForUnknownUser()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Create_PersistsUserAndNormalizesEmail()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.CreateAsync(
            Details(" Alice@Example.com "));

        Assert.Equal(UserWriteStatus.Success, result.Status);
        Assert.NotNull(result.User);
        Assert.NotEqual(Guid.Empty, result.User.Id);
        Assert.Equal("Alice@Example.com", result.User.Email);

        // A fresh context verifies what actually reached SQLite.
        await using var verification = database.CreateContext();
        var saved = await verification.Users.SingleAsync();

        Assert.Equal(result.User.Id, saved.Id);
        Assert.Equal("Alice", saved.FirstName);
        Assert.Equal("Martin", saved.LastName);
        Assert.Equal("IT", saved.Department);
        Assert.Equal("Alice@Example.com", saved.Email);
        Assert.Equal("ALICE@EXAMPLE.COM", saved.NormalizedEmail);
        Assert.NotEqual(default(DateTime), saved.CreatedAtUtc);
        Assert.Equal(saved.CreatedAtUtc, saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task Create_ReturnsDuplicateEmailWithoutAddingAnotherUser()
    {
        await using var database = await TestDatabase.CreateAsync();

        database.Db.Users.Add(NewUser("Alice@Example.com"));
        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var result = await database.Service.CreateAsync(
            Details(" alice@example.com "));

        Assert.Equal(UserWriteStatus.DuplicateEmail, result.Status);
        Assert.Null(result.User);

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Users.CountAsync());
    }

    [Fact]
    public async Task Update_PersistsChangesAndPreservesCreationTime()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = NewUser("alice@example.com");

        database.Db.Users.Add(user);
        await database.Db.SaveChangesAsync();

        var createdAt = user.CreatedAtUtc;
        database.Db.ChangeTracker.Clear();

        var result = await database.Service.UpdateAsync(
            user.Id,
            new UserDetails(
                "Alicia",
                "Jones",
                " Alicia@Example.com ",
                "HR"));

        Assert.Equal(UserWriteStatus.Success, result.Status);
        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal("Alicia", result.User.FirstName);
        Assert.Equal("Jones", result.User.LastName);
        Assert.Equal("Alicia@Example.com", result.User.Email);
        Assert.Equal("HR", result.User.Department);

        await using var verification = database.CreateContext();
        var saved = await verification.Users.SingleAsync();

        Assert.Equal("Alicia", saved.FirstName);
        Assert.Equal("Jones", saved.LastName);
        Assert.Equal("HR", saved.Department);
        Assert.Equal("Alicia@Example.com", saved.Email);
        Assert.Equal("ALICIA@EXAMPLE.COM", saved.NormalizedEmail);
        Assert.Equal(createdAt, saved.CreatedAtUtc);
        Assert.True(saved.UpdatedAtUtc >= createdAt);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForUnknownUser()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.UpdateAsync(
            Guid.NewGuid(),
            Details());

        Assert.Equal(UserWriteStatus.NotFound, result.Status);
        Assert.Null(result.User);
        Assert.Empty(await database.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Update_AllowsUsersOwnEmail()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = NewUser("alice@example.com");

        database.Db.Users.Add(user);
        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var result = await database.Service.UpdateAsync(
            user.Id,
            new UserDetails(
                "Alice",
                "Martin",
                " ALICE@example.com ",
                "HR"));

        Assert.Equal(UserWriteStatus.Success, result.Status);

        await using var verification = database.CreateContext();
        var saved = await verification.Users.SingleAsync();

        Assert.Equal("HR", saved.Department);
        Assert.Equal("ALICE@EXAMPLE.COM", saved.NormalizedEmail);
    }

    [Fact]
    public async Task Update_RejectsAnotherUsersEmailWithoutChangingRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var alice = NewUser("alice@example.com");
        var bob = NewUser("bob@example.com");

        database.Db.Users.AddRange(alice, bob);
        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var result = await database.Service.UpdateAsync(
            bob.Id,
            new UserDetails(
                "Changed",
                "Name",
                " ALICE@example.com ",
                "HR"));

        Assert.Equal(UserWriteStatus.DuplicateEmail, result.Status);

        await using var verification = database.CreateContext();
        var saved = await verification.Users.SingleAsync(
            user => user.Id == bob.Id);

        Assert.Equal("bob@example.com", saved.Email);
        Assert.Equal("Alice", saved.FirstName);
        Assert.Equal("Martin", saved.LastName);
        Assert.Equal("IT", saved.Department);
        Assert.Equal(2, await verification.Users.CountAsync());
    }

    [Fact]
    public async Task Delete_RemovesExistingUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        var user = NewUser("alice@example.com");

        database.Db.Users.Add(user);
        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var deleted = await database.Service.DeleteAsync(user.Id);

        Assert.True(deleted);
        Assert.Null(await database.Service.GetByIdAsync(user.Id));

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Users.ToListAsync());
    }

    [Fact]
    public async Task Delete_ReturnsFalseForUnknownUser()
    {
        await using var database = await TestDatabase.CreateAsync();

        var deleted = await database.Service.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyListForEmptyDatabase()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Service.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_UsesIdToBreakNameTiesAcrossPages()
    {
        await using var database = await TestDatabase.CreateAsync();

        var firstId = Guid.Parse(
            "00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse(
            "00000000-0000-0000-0000-000000000002");
        var thirdId = Guid.Parse(
            "00000000-0000-0000-0000-000000000003");

        // Identical names, deliberately inserted out of ID order.
        database.Db.Users.AddRange(
            NewUser("third@example.com", thirdId),
            NewUser("first@example.com", firstId),
            NewUser("second@example.com", secondId));

        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var firstPage = await database.Service.GetAllAsync(
            page: 1, pageSize: 2);

        var secondPage = await database.Service.GetAllAsync(
            page: 2, pageSize: 2);

        var repeatedPage = await database.Service.GetAllAsync(
            page: 1, pageSize: 2);

        Assert.Equal(
            new[] { firstId, secondId },
            firstPage.Select(user => user.Id).ToArray());

        Assert.Equal(thirdId, Assert.Single(secondPage).Id);

        Assert.Equal(
            firstPage.Select(user => user.Id).ToArray(),
            repeatedPage.Select(user => user.Id).ToArray());

        Assert.Empty(await database.Service.GetAllAsync(
            page: 3, pageSize: 2));
    }

    [Fact]
    public async Task GetAll_AppliesDefaultAndMaximumPageSizes()
    {
        await using var database = await TestDatabase.CreateAsync();

        for (var index = 0; index < 101; index++)
        {
            database.Db.Users.Add(
                NewUser($"user{index}@example.com"));
        }

        await database.Db.SaveChangesAsync();
        database.Db.ChangeTracker.Clear();

        var defaultPage = await database.Service.GetAllAsync();
        var maximumPage = await database.Service.GetAllAsync(
            pageSize: 100);

        Assert.Equal(25, defaultPage.Count);
        Assert.Equal(100, maximumPage.Count);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-1, 25)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task GetAll_RejectsInvalidPaging(int page, int pageSize)
    {
        await using var database = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => database.Service.GetAllAsync(page, pageSize));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Db { get; }
        public UserService Service { get; }

        private TestDatabase(SqliteConnection connection)
        {
            _connection = connection;
            Db = CreateContext();
            Service = new UserService(Db);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var database = new TestDatabase(connection);

            try
            {
                await database.Db.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new AppDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}