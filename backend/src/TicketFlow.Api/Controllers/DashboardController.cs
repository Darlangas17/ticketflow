using Microsoft.AspNetCore.Mvc;
using TicketFlow.Api.Auth;
using TicketFlow.Application.Dashboard;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Ticket totals by status, priority, branch and category, and the average resolution time.</summary>
    /// <remarks>
    /// Uses the same visibility as GET /api/tickets: admins get company-wide numbers;
    /// technicians and requesters get the numbers of the tickets they can see.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummaryDto>(StatusCodes.Status200OK)]
    public Task<DashboardSummaryDto> Summary(CancellationToken cancellationToken) =>
        dashboardService.GetSummaryAsync(User.GetUserId(), cancellationToken);
}
