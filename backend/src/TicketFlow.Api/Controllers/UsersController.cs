using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Users;

namespace TicketFlow.Api.Controllers;

/// <summary>User administration (admin only), plus the list of possible assignees for staff.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Paginated users ordered by name. Filters: role, isActive, search (name or e-mail).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<PagedResult<UserAccountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<UserAccountDto>> List([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        userService.ListAsync(query, cancellationToken);

    /// <summary>Active technicians and admins a ticket can be assigned to. Staff only.</summary>
    [HttpGet("assignees")]
    [Authorize(Policy = AuthorizationPolicies.StaffOnly)]
    [ProducesResponseType<IReadOnlyList<UserSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UserSummaryDto>> Assignees(CancellationToken cancellationToken) =>
        userService.ListAssigneesAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<UserAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserAccountDto> Get(int id, CancellationToken cancellationToken) =>
        userService.GetAsync(id, cancellationToken);

    /// <summary>Creates an active user with any role (unlike /auth/register, which always creates requesters).</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<UserAccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserAccountDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>Edits name, e-mail, role and status; sends a new password only if `password` is present.</summary>
    /// <remarks>Admins cannot deactivate themselves or remove their own Admin role.</remarks>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<UserAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<UserAccountDto> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        userService.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
}
