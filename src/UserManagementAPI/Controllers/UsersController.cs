using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Contracts;
using UserManagementAPI.Services;
namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
     private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users ;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var users = await _users.GetAllAsync(
            cancellationToken: cancellationToken);

        return Ok(users);
    }
}