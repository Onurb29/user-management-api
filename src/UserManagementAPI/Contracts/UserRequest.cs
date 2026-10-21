using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Services;

namespace UserManagementAPI.Contracts;

public abstract class UserRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    private string _email = string.Empty;
    [Required, StringLength(254), EmailAddress]
    [RegularExpression(@"^[\x21-\x7E]+$", ErrorMessage = "Use an ASCII email address without spaces.")]
    public string Email
    {
        get => _email;
        set => _email = value?.Trim() ?? string.Empty;
    }

    [Required, StringLength(100)]
    public string Department { get; set; } = string.Empty;

    public UserDetails ToDetails() => new(
        FirstName.Trim(), LastName.Trim(), Email, Department.Trim());
}

public sealed class CreateUserRequest : UserRequest;
public sealed class UpdateUserRequest : UserRequest;
