using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;

namespace TicketFlow.Api.Controllers;

/// <summary>
/// Support tickets. Visibility depends on the role: requesters see their own tickets,
/// technicians see tickets assigned to them plus unassigned ones, admins see everything.
/// Tickets outside the caller's visibility return 404.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class TicketsController(ITicketService ticketService) : ControllerBase
{
    /// <summary>Paginated list of the tickets the caller can see, with optional filters, search and sort.</summary>
    /// <remarks>
    /// `sort` accepts createdAt, updatedAt, priority, status or title; prefix with "-" for descending
    /// (default "-createdAt"). `search` looks in title and description, or matches a ticket number.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<TicketListItemDto>> List([FromQuery] TicketListQuery query, CancellationToken cancellationToken) =>
        ticketService.ListAsync(User.GetUserId(), query, cancellationToken);

    /// <summary>Ticket details with comments and change history.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TicketDetailsDto> Get(int id, CancellationToken cancellationToken) =>
        ticketService.GetAsync(User.GetUserId(), id, cancellationToken);

    /// <summary>Creates a ticket in Open status with the caller as requester.</summary>
    [HttpPost]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetailsDto>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    /// <summary>Edits title, description, priority and category. Closed tickets cannot be edited.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TicketDetailsDto> Update(int id, UpdateTicketRequest request, CancellationToken cancellationToken) =>
        ticketService.UpdateAsync(User.GetUserId(), id, request, cancellationToken);

    /// <summary>Moves the ticket to a new status and records it in the history.</summary>
    /// <remarks>
    /// Valid transitions: Open → InProgress → (OnHold ↔ InProgress) → Resolved → Closed.
    /// Any other transition returns 400. Requesters can only close their own resolved tickets.
    /// </remarks>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TicketDetailsDto> ChangeStatus(int id, ChangeTicketStatusRequest request, CancellationToken cancellationToken) =>
        ticketService.ChangeStatusAsync(User.GetUserId(), id, request, cancellationToken);

    /// <summary>Assigns the ticket to an active technician or admin (null unassigns it). Staff only.</summary>
    [HttpPatch("{id:int}/assign")]
    [Authorize(Policy = AuthorizationPolicies.StaffOnly)]
    [ProducesResponseType<TicketDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TicketDetailsDto> Assign(int id, AssignTicketRequest request, CancellationToken cancellationToken) =>
        ticketService.AssignAsync(User.GetUserId(), id, request, cancellationToken);

    /// <summary>Adds a comment to a ticket the caller can see.</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType<TicketCommentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketCommentDto>> AddComment(int id, AddCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await ticketService.AddCommentAsync(User.GetUserId(), id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, comment);
    }

    /// <summary>Deletes a ticket with its comments and history. Admin only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await ticketService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
