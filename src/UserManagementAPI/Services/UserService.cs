using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Contracts;
using UserManagementAPI.Data;
using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public sealed class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page), "Page must be at least 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), "Page size must be between 1 and 100.");
        }

        // Calculate using long to prevent integer overflow.
        var offset = ((long)page - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page), "The requested page is too large.");
        }

        return await _db.Users
            .AsNoTracking()
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(user => new UserResponse(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Department))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserResponse(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Department))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserWriteResult> CreateAsync(
        UserDetails details,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);

        var normalizedEmail = NormalizeEmail(details.Email);

        var emailExists = await _db.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return new UserWriteResult(UserWriteStatus.DuplicateEmail);
        }

        var user = new User
        {
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            Department = details.Department
        };

        _db.Users.Add(user);

        try
        {
            // AppDbContext normalizes email and sets audit timestamps.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateEmail(exception))
        {
            // Another request may have saved this email after our check.
            // Stop tracking the failed insert so it cannot be retried
            // accidentally by a later SaveChanges call.
            _db.Entry(user).State = EntityState.Detached;

            return new UserWriteResult(UserWriteStatus.DuplicateEmail);
        }

        return new UserWriteResult(
            UserWriteStatus.Success,
            ToResponse(user));
    }

    public async Task<UserWriteResult> UpdateAsync(
        Guid id,
        UserDetails details,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);

        // This query tracks the entity because we intend to change it.
        var user = await _db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return new UserWriteResult(UserWriteStatus.NotFound);
        }

        var normalizedEmail = NormalizeEmail(details.Email);

        // Exclude this user so keeping their own email is allowed.
        var emailExists = await _db.Users.AnyAsync(
            other => other.Id != id &&
                     other.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return new UserWriteResult(UserWriteStatus.DuplicateEmail);
        }

        // Only change the entity after the duplicate check succeeds.
        user.FirstName = details.FirstName;
        user.LastName = details.LastName;
        user.Email = details.Email;
        user.Department = details.Department;

        try
        {
            // AppDbContext preserves CreatedAtUtc and updates UpdatedAtUtc.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateEmail(exception))
        {
            // Discard the tracked changes from this failed update.
            _db.Entry(user).State = EntityState.Detached;

            return new UserWriteResult(UserWriteStatus.DuplicateEmail);
        }

        return new UserWriteResult(
            UserWriteStatus.Success,
            ToResponse(user));
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return false;
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Must match the normalization used by AppDbContext.
        return email.Trim().ToUpperInvariant();
    }

    private static UserResponse ToResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Department);
    }

    private static bool IsDuplicateEmail(DbUpdateException exception)
    {
        // 2067 means a SQLite UNIQUE constraint violation.
        // Check the column too, so unrelated constraints are not
        // incorrectly reported as duplicate emails.
        return exception.InnerException is SqliteException sqlite &&
               sqlite.SqliteExtendedErrorCode == 2067 &&
               sqlite.Message.Contains(
                   "UNIQUE constraint failed: Users.NormalizedEmail",
                   StringComparison.Ordinal);
    }
}