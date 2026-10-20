
using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<User>> GetAll()
    {
        var users = new List<User> // Sample list of users
        {
            new User
            {
                FirstName = "Alice",
                LastName = "Martin",
                Email = "alice@example.com",
                Department = "IT"
            }
        };

        return Ok(users);
    }
}