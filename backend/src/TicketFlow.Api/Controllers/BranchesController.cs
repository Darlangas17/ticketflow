using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Auth;
using TicketFlow.Application.Catalogs;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Api.Controllers;

/// <summary>Company branches. Any user can read them; only admins can change them.</summary>
[ApiController]
[Route("api/branches")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class BranchesController(IBranchService branchService) : ControllerBase
{
    /// <summary>Branches ordered by name. Only active ones, unless an admin asks for includeInactive=true.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<BranchDto>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        branchService.ListAsync(includeInactive && User.IsInRole(nameof(UserRole.Admin)), cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<BranchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BranchDto> Get(int id, CancellationToken cancellationToken) =>
        branchService.GetAsync(id, cancellationToken);

    /// <summary>Creates an active branch. Names are unique (case-insensitive).</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<BranchDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = await branchService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = branch.Id }, branch);
    }

    /// <summary>Edits a branch. Set isActive=false to stop new tickets for it while keeping its history.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<BranchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BranchDto> Update(int id, UpdateBranchRequest request, CancellationToken cancellationToken) =>
        branchService.UpdateAsync(id, request, cancellationToken);

    /// <summary>Deletes a branch without tickets. A branch with tickets returns 409: deactivate it instead.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await branchService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
