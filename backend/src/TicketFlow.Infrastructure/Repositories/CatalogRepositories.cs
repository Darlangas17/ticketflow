using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Catalogs;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Repositories;

// Name checks compare in lower case: SQL Server's default collation already ignores case
// (so the unique index would reject "network" next to "Network"), and this keeps the check
// consistent on other providers too. Catalog tables are small, so the non-indexed comparison is fine.

public sealed class BranchRepository(TicketFlowDbContext db) : IBranchRepository
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await db.Branches
            .AsNoTracking()
            .Where(b => includeInactive || b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.City, b.IsActive))
            .ToListAsync(cancellationToken);

    public Task<Branch?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.ToLower();
        return db.Branches.AnyAsync(b => b.Name.ToLower() == lower && b.Id != excludeId, cancellationToken);
    }

    public Task<bool> HasTicketsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Tickets.AnyAsync(t => t.BranchId == id, cancellationToken);

    public void Add(Branch branch) => db.Branches.Add(branch);

    public void Remove(Branch branch) => db.Branches.Remove(branch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}

public sealed class CategoryRepository(TicketFlowDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync(cancellationToken);

    public Task<Category?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = name.ToLower();
        return db.Categories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != excludeId, cancellationToken);
    }

    public Task<bool> HasTicketsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Tickets.AnyAsync(t => t.CategoryId == id, cancellationToken);

    public void Add(Category category) => db.Categories.Add(category);

    public void Remove(Category category) => db.Categories.Remove(category);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
