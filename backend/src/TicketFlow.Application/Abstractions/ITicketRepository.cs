using System.Linq.Expressions;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public interface ITicketRepository
{
    /// <summary>Filtered, sorted and paginated list, restricted to the tickets matching <paramref name="visibility"/>.</summary>
    Task<PagedResult<TicketListItemDto>> ListAsync(
        TicketListQuery query,
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default);

    /// <summary>Ticket with comments and history, or null if it does not exist or is not visible.</summary>
    Task<TicketDetailsDto?> GetDetailsAsync(
        int id,
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default);

    /// <summary>Tracked ticket (without comments or history) for changes saved with <see cref="SaveChangesAsync"/>.</summary>
    Task<Ticket?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default);

    Task<bool> ActiveBranchExistsAsync(int branchId, CancellationToken cancellationToken = default);

    void Add(Ticket ticket);

    void Remove(Ticket ticket);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
