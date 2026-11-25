using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;

namespace TicketFlow.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The dashboard uses the same visibility rules as the ticket list: admins get the
/// company-wide numbers, technicians and requesters get the numbers of the tickets they can see.
/// </summary>
public sealed class DashboardService(IDashboardRepository dashboard, IUserRepository users) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var actor = await users.GetActorAsync(userId, cancellationToken);
        return await dashboard.GetSummaryAsync(TicketAccess.VisibleTo(actor), cancellationToken);
    }
}
