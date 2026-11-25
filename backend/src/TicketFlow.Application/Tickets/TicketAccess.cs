using System.Linq.Expressions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Tickets;

/// <summary>The authenticated user performing a ticket operation.</summary>
public sealed record Actor(int Id, string FullName, UserRole Role)
{
    public bool IsStaff => Role is UserRole.Admin or UserRole.Technician;
}

/// <summary>
/// Role rules for tickets:
/// - Requester: only their own tickets.
/// - Technician: tickets assigned to them and unassigned tickets.
/// - Admin: every ticket.
/// </summary>
public static class TicketAccess
{
    /// <summary>
    /// Visibility as an expression, so the database does the filtering (EF Core translates it to SQL).
    /// Must stay equivalent to <see cref="CanView"/>.
    /// </summary>
    public static Expression<Func<Ticket, bool>> VisibleTo(Actor actor)
    {
        var userId = actor.Id;
        return actor.Role switch
        {
            UserRole.Admin => _ => true,
            UserRole.Technician => t => t.AssigneeId == userId || t.AssigneeId == null,
            _ => t => t.RequesterId == userId,
        };
    }

    /// <summary>Visibility check for a ticket already loaded in memory.</summary>
    public static bool CanView(Actor actor, Ticket ticket) => actor.Role switch
    {
        UserRole.Admin => true,
        UserRole.Technician => ticket.AssigneeId == actor.Id || ticket.AssigneeId is null,
        _ => ticket.RequesterId == actor.Id,
    };

    /// <summary>
    /// Staff can make any valid transition on tickets they see. A requester can only close
    /// their own ticket once it is resolved (confirming the fix).
    /// </summary>
    public static bool CanChangeStatus(Actor actor, Ticket ticket, TicketStatus newStatus) =>
        actor.IsStaff ||
        (ticket.RequesterId == actor.Id && ticket.Status == TicketStatus.Resolved && newStatus == TicketStatus.Closed);

    public static bool CanAssign(Actor actor) => actor.IsStaff;

    public static bool CanDelete(Actor actor) => actor.Role == UserRole.Admin;
}
