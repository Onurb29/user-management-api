using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using UserManagementAPI.Contracts;
using UserManagementAPI.Services;

namespace UserManagementAPI.Controllers;

[ApiController]
[Authorize(Policy = "ManageUsers")]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (((long)page - 1) * pageSize > int.MaxValue)
        {
            ModelState.AddModelError(nameof(page), "The requested page is too large.");
            return ValidationProblem(ModelState);
        }

        return Ok(await users.GetAllAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return InvalidId();
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(request.ToDetails(), cancellationToken);
        if (result.Status == UserWriteStatus.DuplicateEmail) return DuplicateEmail();
        if (result.Status != UserWriteStatus.Success || result.User is null)
            throw new InvalidOperationException("Unexpected create outcome.");

        return CreatedAtAction(nameof(GetById), new { id = result.User.Id }, result.User);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return InvalidId();
        var result = await users.UpdateAsync(id, request.ToDetails(), cancellationToken);
        return result.Status switch
        {
            UserWriteStatus.Success => NoContent(),
            UserWriteStatus.NotFound => NotFound(),
            UserWriteStatus.DuplicateEmail => DuplicateEmail(),
            _ => throw new InvalidOperationException("Unexpected update outcome.")
        };
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return InvalidId();
        return await users.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    private ObjectResult InvalidId() => Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "ID must be a non-empty GUID.");

    private ObjectResult DuplicateEmail() => Problem(
        statusCode: StatusCodes.Status409Conflict, title: "A user with this email already exists.");
}
