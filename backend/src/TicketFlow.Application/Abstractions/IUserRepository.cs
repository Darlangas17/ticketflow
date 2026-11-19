using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <param name="email">Normalized (trimmed, lower-case) e-mail.</param>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <param name="email">Normalized (trimmed, lower-case) e-mail.</param>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
