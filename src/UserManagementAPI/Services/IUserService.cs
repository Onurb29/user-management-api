using UserManagementAPI.Contracts;

namespace UserManagementAPI.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetAllAsync(
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<UserWriteResult> CreateAsync(
        UserDetails details,
        CancellationToken cancellationToken = default);

    Task<UserWriteResult> UpdateAsync(
        Guid id,
        UserDetails details,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}