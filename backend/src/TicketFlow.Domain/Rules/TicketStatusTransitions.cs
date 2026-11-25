using TicketFlow.Domain.Enums;

namespace TicketFlow.Domain.Rules;

/// <summary>
/// Ticket workflow: Open → InProgress → (OnHold ↔ InProgress) → Resolved → Closed.
/// Any other move (including staying in the same status) is invalid.
/// </summary>
public static class TicketStatusTransitions
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Allowed =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] = [TicketStatus.InProgress],
            [TicketStatus.InProgress] = [TicketStatus.OnHold, TicketStatus.Resolved],
            [TicketStatus.OnHold] = [TicketStatus.InProgress],
            [TicketStatus.Resolved] = [TicketStatus.Closed],
            [TicketStatus.Closed] = [],
        };

    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Statuses reachable from <paramref name="from"/> in one step.</summary>
    public static IReadOnlyList<TicketStatus> NextStatuses(TicketStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
