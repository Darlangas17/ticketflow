using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Dashboard;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Repositories;

/// <summary>
/// Every number is computed by the database with GROUP BY / COUNT / AVG: only one row per
/// group comes back, never the tickets themselves. Branch and category names come from the
/// (small) catalog tables and are joined with the counts in memory, which also gives a zero
/// for catalogs that have no tickets.
/// </summary>
public sealed class DashboardRepository(TicketFlowDbContext db) : IDashboardRepository
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        Expression<Func<Ticket, bool>> visibility,
        CancellationToken cancellationToken = default)
    {
        var tickets = db.Tickets.AsNoTracking().Where(visibility);

        // Queries run one after another: a DbContext does not support parallel operations.
        var byStatus = await tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var byPriority = await tickets
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Priority, x => x.Count, cancellationToken);

        var byBranch = await tickets
            .GroupBy(t => t.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, cancellationToken);

        var byCategory = await tickets
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var unassigned = await tickets.CountAsync(
            t => t.AssigneeId == null && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed,
            cancellationToken);

        var resolvedTickets = tickets.Where(t => t.ResolvedAt != null);
        var resolvedCount = await resolvedTickets.CountAsync(cancellationToken);
        var averageMinutes = await AverageResolutionMinutesAsync(resolvedTickets, cancellationToken);

        var branches = await db.Branches.AsNoTracking()
            .Select(b => new { b.Id, b.Name, b.IsActive })
            .ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        int CountOf<TKey>(Dictionary<TKey, int> counts, TKey key) where TKey : notnull =>
            counts.TryGetValue(key, out var count) ? count : 0;

        var total = byStatus.Values.Sum();
        var finished = CountOf(byStatus, TicketStatus.Resolved) + CountOf(byStatus, TicketStatus.Closed);

        return new DashboardSummaryDto(
            TotalTickets: total,
            ActiveTickets: total - finished,
            UnassignedTickets: unassigned,
            ResolvedTickets: resolvedCount,
            AverageResolutionHours: averageMinutes is { } minutes ? Math.Round(minutes / 60, 1) : null,
            ByStatus: Enum.GetValues<TicketStatus>().Select(s => new StatusCountDto(s, CountOf(byStatus, s))).ToList(),
            ByPriority: Enum.GetValues<TicketPriority>().Select(p => new PriorityCountDto(p, CountOf(byPriority, p))).ToList(),
            ByBranch: branches
                .Where(b => b.IsActive || byBranch.ContainsKey(b.Id))
                .Select(b => new CatalogCountDto(b.Id, b.Name, CountOf(byBranch, b.Id)))
                .OrderByDescending(b => b.Count).ThenBy(b => b.Name)
                .ToList(),
            ByCategory: categories
                .Select(c => new CatalogCountDto(c.Id, c.Name, CountOf(byCategory, c.Id)))
                .OrderByDescending(c => c.Count).ThenBy(c => c.Name)
                .ToList());
    }

    private async Task<double?> AverageResolutionMinutesAsync(IQueryable<Ticket> resolved, CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlServer())
        {
            // AVG(CAST(DATEDIFF(minute, CreatedAt, ResolvedAt) AS float)) runs in SQL Server.
            return await resolved.AverageAsync(
                t => (double?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value),
                cancellationToken);
        }

        // Other providers (SQLite in the tests) have no DATEDIFF: read only the two dates and average here.
        var dates = await resolved
            .Select(t => new { t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value })
            .ToListAsync(cancellationToken);
        return dates.Count == 0 ? null : dates.Average(d => (d.ResolvedAt - d.CreatedAt).TotalMinutes);
    }
}
