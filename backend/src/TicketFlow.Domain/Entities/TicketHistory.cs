namespace TicketFlow.Domain.Entities;

/// <summary>
/// Audit record of a single field change on a ticket (status, priority or assignee).
/// </summary>
public class TicketHistory
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int ChangedById { get; set; }
    public User ChangedBy { get; set; } = null!;

    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
}
