using System.ComponentModel.DataAnnotations;

namespace UserManagementAPI.Services;

public sealed record UserDetails(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, StringLength(254), EmailAddress]
    [property: RegularExpression(@"^[\x21-\x7E]+$")] string Email,
    [property: Required, StringLength(100)] string Department);
