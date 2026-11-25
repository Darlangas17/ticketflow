using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Tickets;

// ----- Requests -----

/// <param name="Priority">Optional; defaults to Medium.</param>
public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority? Priority,
    int CategoryId,
    int BranchId);

public sealed record UpdateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    int CategoryId);

public sealed record ChangeTicketStatusRequest(TicketStatus Status);

/// <param name="AssigneeId">Technician or admin to assign; null removes the assignee.</param>
public sealed record AssignTicketRequest(int? AssigneeId);

public sealed record AddCommentRequest(string Body);

// ----- Responses -----

public sealed record CatalogItemDto(int Id, string Name);

public sealed record UserSummaryDto(int Id, string FullName);

public sealed record TicketListItemDto(
    int Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    CatalogItemDto Category,
    CatalogItemDto Branch,
    UserSummaryDto Requester,
    UserSummaryDto? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt);

public sealed record TicketCommentDto(int Id, UserSummaryDto Author, string Body, DateTime CreatedAt);

public sealed record TicketHistoryDto(
    int Id,
    string Field,
    string? OldValue,
    string? NewValue,
    UserSummaryDto ChangedBy,
    DateTime ChangedAt);

public sealed record TicketDetailsDto(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    CatalogItemDto Category,
    CatalogItemDto Branch,
    UserSummaryDto Requester,
    UserSummaryDto? Assignee,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    IReadOnlyList<TicketCommentDto> Comments,
    IReadOnlyList<TicketHistoryDto> History)
{
    /// <summary>Statuses the ticket can move to next (helps the UI show only valid actions).</summary>
    public IReadOnlyList<TicketStatus> AllowedNextStatuses { get; init; } = [];
}
