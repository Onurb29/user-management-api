namespace UserManagementAPI.Services;

public sealed record UserDetails(
    string FirstName,
    string LastName,
    string Email,
    string Department);