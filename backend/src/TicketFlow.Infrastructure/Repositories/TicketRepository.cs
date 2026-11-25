using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Repositories;

public sealed class TicketRepository(TicketFlowDbContext db) : ITicketRepository
{
    public async Task<PagedResult<TicketListItemDto>> ListAsync(
        TicketListQuery query,
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default)
    {
        var tickets = ApplyFilters(db.Tickets.AsNoTracking().Where(visibility), query);

        var totalCount = await tickets.CountAsync(cancellationToken);

        // Projection: SQL selects only the listed columns and joins the names in the same query (no N+1).
        var items = await ApplySort(tickets, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailsDto?> GetDetailsAsync(
        int id,
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default) =>
        db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Where(visibility)
            .Select(t => new TicketDetailsDto(
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                new CatalogItemDto(t.Category.Id, t.Category.Name),
                new CatalogItemDto(t.Branch.Id, t.Branch.Name),
                new UserSummaryDto(t.Requester.Id, t.Requester.FullName),
                t.Assignee == null ? null : new UserSummaryDto(t.Assignee.Id, t.Assignee.FullName),
                t.CreatedAt,
                t.UpdatedAt,
                t.ResolvedAt,
                t.Comments
                    .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                    .Select(c => new TicketCommentDto(c.Id, new UserSummaryDto(c.Author.Id, c.Author.FullName), c.Body, c.CreatedAt))
                    .ToList(),
                t.History
                    .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
                    .Select(h => new TicketHistoryDto(
                        h.Id, h.Field, h.OldValue, h.NewValue,
                        new UserSummaryDto(h.ChangedBy.Id, h.ChangedBy.FullName),
                        h.ChangedAt))
                    .ToList()))
            // One query per collection instead of a single JOIN that multiplies comments × history rows.
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Ticket?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> CategoryExistsAsync(int categoryId, CancellationToken cancellationToken = default) =>
        db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken);

    public Task<bool> ActiveBranchExistsAsync(int branchId, CancellationToken cancellationToken = default) =>
        db.Branches.AnyAsync(b => b.Id == branchId && b.IsActive, cancellationToken);

    public void Add(Ticket ticket) => db.Tickets.Add(ticket);

    public void Remove(Ticket ticket) => db.Tickets.Remove(ticket);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    private static readonly Expression<Func<Ticket, TicketListItemDto>> ToListItem = t => new TicketListItemDto(
        t.Id,
        t.Title,
        t.Status,
        t.Priority,
        new CatalogItemDto(t.Category.Id, t.Category.Name),
        new CatalogItemDto(t.Branch.Id, t.Branch.Name),
        new UserSummaryDto(t.Requester.Id, t.Requester.FullName),
        t.Assignee == null ? null : new UserSummaryDto(t.Assignee.Id, t.Assignee.FullName),
        t.CreatedAt,
        t.UpdatedAt,
        t.ResolvedAt);

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> tickets, TicketListQuery query)
    {
        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (query.BranchId is { } branchId)
        {
            tickets = tickets.Where(t => t.BranchId == branchId);
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            tickets = tickets.Where(t => t.AssigneeId == assigneeId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Parameterized by EF Core (no SQL injection). "123" or "#123" also matches the ticket number.
            var search = query.Search.Trim();
            int? ticketId = int.TryParse(search.TrimStart('#'), out var parsedId) ? parsedId : null;
            tickets = tickets.Where(t =>
                t.Title.Contains(search) ||
                t.Description.Contains(search) ||
                (ticketId != null && t.Id == ticketId));
        }

        return tickets;
    }

    private static IQueryable<Ticket> ApplySort(IQueryable<Ticket> tickets, string? sort)
    {
        var (field, descending) = TicketSort.Parse(sort);

        // Status and priority are stored as text, so they are sorted by their business order
        // (Low < Medium < High < Critical), not alphabetically.
        IOrderedQueryable<Ticket> ordered = field switch
        {
            "updatedat" => OrderBy(tickets, t => t.UpdatedAt, descending),
            "priority" => OrderBy(tickets, PriorityRank, descending),
            "status" => OrderBy(tickets, StatusRank, descending),
            "title" => OrderBy(tickets, t => t.Title, descending),
            _ => OrderBy(tickets, t => t.CreatedAt, descending),
        };

        // Tie-breaker so pages are stable when several tickets share the sort value.
        return descending ? ordered.ThenByDescending(t => t.Id) : ordered.ThenBy(t => t.Id);
    }

    private static IOrderedQueryable<Ticket> OrderBy<TKey>(
        IQueryable<Ticket> tickets, Expression<Func<Ticket, TKey>> key, bool descending) =>
        descending ? tickets.OrderByDescending(key) : tickets.OrderBy(key);

    private static readonly Expression<Func<Ticket, int>> PriorityRank = t =>
        t.Priority == TicketPriority.Critical ? 3 :
        t.Priority == TicketPriority.High ? 2 :
        t.Priority == TicketPriority.Medium ? 1 : 0;

    private static readonly Expression<Func<Ticket, int>> StatusRank = t =>
        t.Status == TicketStatus.Closed ? 4 :
        t.Status == TicketStatus.Resolved ? 3 :
        t.Status == TicketStatus.OnHold ? 2 :
        t.Status == TicketStatus.InProgress ? 1 : 0;
}
