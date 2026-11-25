using TicketFlow.Application.Catalogs;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public interface IBranchRepository
{
    /// <summary>Branches ordered by name.</summary>
    Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>Tracked branch for changes saved with <see cref="SaveChangesAsync"/>.</summary>
    Task<Branch?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive; <paramref name="excludeId"/> skips the branch being edited.</summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> HasTicketsAsync(int id, CancellationToken cancellationToken = default);

    void Add(Branch branch);

    void Remove(Branch branch);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICategoryRepository
{
    /// <summary>Categories ordered by name.</summary>
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked category for changes saved with <see cref="SaveChangesAsync"/>.</summary>
    Task<Category?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive; <paramref name="excludeId"/> skips the category being edited.</summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> HasTicketsAsync(int id, CancellationToken cancellationToken = default);

    void Add(Category category);

    void Remove(Category category);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
