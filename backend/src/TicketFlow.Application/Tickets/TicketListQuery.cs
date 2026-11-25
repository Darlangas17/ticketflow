using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Tickets;

/// <summary>Query-string parameters of GET /api/tickets. Every filter is optional.</summary>
public sealed record TicketListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }
    public int? BranchId { get; init; }
    public int? CategoryId { get; init; }
    public int? AssigneeId { get; init; }

    /// <summary>Text to find in the title or description, or a ticket number.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// One of <see cref="TicketSort.Allowed"/>; a leading "-" sorts descending
    /// (e.g. "-priority"). Defaults to newest first.
    /// </summary>
    public string? Sort { get; init; }
}

public static class TicketSort
{
    public const string Default = "-createdAt";

    public static readonly IReadOnlyList<string> Fields = ["createdAt", "updatedAt", "priority", "status", "title"];

    public static readonly IReadOnlyList<string> Allowed = [.. Fields, .. Fields.Select(f => "-" + f)];

    /// <summary>Splits "-priority" into ("priority", descending: true). Field names are case-insensitive.</summary>
    public static (string Field, bool Descending) Parse(string? sort)
    {
        var value = string.IsNullOrWhiteSpace(sort) ? Default : sort.Trim();
        var descending = value.StartsWith('-');
        var field = descending ? value[1..] : value;
        return (field.ToLowerInvariant(), descending);
    }

    public static bool IsValid(string? sort) =>
        string.IsNullOrWhiteSpace(sort) ||
        Allowed.Contains(sort.Trim(), StringComparer.OrdinalIgnoreCase);
}
