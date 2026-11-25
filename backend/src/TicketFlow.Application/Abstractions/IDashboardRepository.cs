using System.Linq.Expressions;
using TicketFlow.Application.Dashboard;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public interface IDashboardRepository
{
    /// <summary>Aggregates computed by the database over the tickets matching <paramref name="visibility"/>.</summary>
    Task<DashboardSummaryDto> GetSummaryAsync(
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default);
}
