using TicketFlow.Application.Common;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Read-only (not tracked) user.</summary>
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked user for changes saved with <see cref="SaveChangesAsync"/>.</summary>
    Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <param name="email">Normalized (trimmed, lower-case) e-mail.</param>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <param name="email">Normalized (trimmed, lower-case) e-mail.</param>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>True if a user other than <paramref name="userId"/> already has the (normalized) e-mail.</summary>
    Task<bool> EmailTakenByOtherAsync(string email, int userId, CancellationToken cancellationToken = default);

    /// <summary>Filtered and paginated list ordered by name.</summary>
    Task<PagedResult<UserAccountDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Active technicians and admins, i.e. the users a ticket can be assigned to.</summary>
    Task<IReadOnlyList<UserSummaryDto>> ListAssigneesAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds the user and saves immediately.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
