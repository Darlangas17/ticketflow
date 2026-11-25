using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dashboard;

public sealed record StatusCountDto(TicketStatus Status, int Count);

public sealed record PriorityCountDto(TicketPriority Priority, int Count);

public sealed record CatalogCountDto(int Id, string Name, int Count);

/// <param name="TotalTickets">Every ticket the caller can see.</param>
/// <param name="ActiveTickets">Tickets not yet Resolved or Closed.</param>
/// <param name="UnassignedTickets">Active tickets without an assignee.</param>
/// <param name="ResolvedTickets">Tickets that reached Resolved (including those later Closed).</param>
/// <param name="AverageResolutionHours">From creation to resolution; null when nothing has been resolved yet.</param>
/// <param name="ByStatus">Every status, in workflow order, including zero counts.</param>
/// <param name="ByPriority">Every priority, from Low to Critical, including zero counts.</param>
/// <param name="ByBranch">Active branches plus any inactive branch that has tickets, most tickets first.</param>
/// <param name="ByCategory">Every category, most tickets first.</param>
public sealed record DashboardSummaryDto(
    int TotalTickets,
    int ActiveTickets,
    int UnassignedTickets,
    int ResolvedTickets,
    double? AverageResolutionHours,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority,
    IReadOnlyList<CatalogCountDto> ByBranch,
    IReadOnlyList<CatalogCountDto> ByCategory);
