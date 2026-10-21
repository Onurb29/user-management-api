using UserManagementAPI.Contracts;

namespace UserManagementAPI.Services;

public enum UserWriteStatus
{
    Success,
    NotFound,
    DuplicateEmail
}

public sealed record UserWriteResult(
    UserWriteStatus Status,
    UserResponse? User = null);