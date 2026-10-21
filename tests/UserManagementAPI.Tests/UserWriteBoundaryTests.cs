using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UserManagementAPI.Data;
using UserManagementAPI.Models;
using UserManagementAPI.Services;
using Xunit;

namespace UserManagementAPI.Tests;

public sealed class UserWriteBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailCollisionAfterPrecheckReturnsDuplicateAndContextCanBeReused(bool update)
    {
        var path = Path.Combine(Path.GetTempPath(), $"users-race-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        try
        {
            var existing = new User { FirstName = "Original", LastName = "Name", Email = "original@example.com", Department = "IT" };
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
                if (update) { setup.Users.Add(existing); await setup.SaveChangesAsync(); }
            }
            // Insert a competitor after the service's AnyAsync check but before its SaveChanges.
            var interceptor = new CompetingInsert(options);
            var racedOptions = new DbContextOptionsBuilder<AppDbContext>(options).AddInterceptors(interceptor).Options;
            await using var db = new AppDbContext(racedOptions);
            var service = new UserService(db);
            var details = new UserDetails("Changed", "Name", "race@example.com", "HR");
            var result = update
                ? await service.UpdateAsync(existing.Id, details)
                : await service.CreateAsync(details);
            Assert.Equal(UserWriteStatus.DuplicateEmail, result.Status);
            Assert.Empty(db.ChangeTracker.Entries<User>());
            var next = await service.CreateAsync(details with { Email = "next@example.com" });
            Assert.Equal(UserWriteStatus.Success, next.Status);
            await using var check = new AppDbContext(options);
            Assert.Equal(update ? 3 : 2, await check.Users.CountAsync());
            if (update)
            {
                var unchanged = await check.Users.SingleAsync(u => u.Id == existing.Id);
                Assert.Equal("original@example.com", unchanged.Email);
                Assert.Equal("Original", unchanged.FirstName);
                Assert.Equal("IT", unchanged.Department);
            }
        }
        finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
    }

    [Fact]
    public async Task ServiceRejectsInvalidInputEvenWithoutAController()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var service = new UserService(db);
        foreach (var details in new[]
        {
            new UserDetails(" ", "Name", "a@example.com", "IT"),
            new UserDetails("Alice", "Name", "invalid", "IT"),
            new UserDetails("Alice", "Name", "a@example.com", new string('x', 101))
        })
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(details));
        Assert.Empty(await db.Users.ToListAsync());
    }

    private sealed class CompetingInsert(DbContextOptions<AppDbContext> options) : SaveChangesInterceptor
    {
        private bool _inserted;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_inserted)
            {
                _inserted = true;
                await using var competitor = new AppDbContext(options);
                competitor.Users.Add(new User { FirstName = "Competitor", LastName = "Name", Email = "RACE@example.com", Department = "IT" });
                await competitor.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }
}
