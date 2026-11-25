using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Repositories;

public sealed class UserRepository(TicketFlowDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailTakenByOtherAsync(string email, int userId, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Email == email && u.Id != userId, cancellationToken);

    public async Task<PagedResult<UserAccountDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        var users = db.Users.AsNoTracking();

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        }

        var totalCount = await users.CountAsync(cancellationToken);

        // The password hash is never selected.
        var items = await users
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserAccountDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserAccountDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListAssigneesAsync(CancellationToken cancellationToken = default) =>
        await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Role != UserRole.Requester)
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummaryDto(u.Id, u.FullName))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
