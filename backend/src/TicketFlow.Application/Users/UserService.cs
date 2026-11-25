using FluentValidation;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Common;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Users;

/// <summary>User administration (admin only; enforced by the API policies).</summary>
public interface IUserService
{
    Task<PagedResult<UserAccountDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default);

    Task<UserAccountDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<UserAccountDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <param name="adminId">The administrator making the change.</param>
    Task<UserAccountDto> UpdateAsync(int adminId, int id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSummaryDto>> ListAssigneesAsync(CancellationToken cancellationToken = default);
}

public sealed class UserService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IValidator<UserListQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    TimeProvider timeProvider) : IUserService
{
    public const string SelfLockoutMessage = "You cannot deactivate your own account or remove your own Admin role.";

    public async Task<PagedResult<UserAccountDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        return await users.ListAsync(query, cancellationToken);
    }

    public async Task<UserAccountDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw UserNotFound(id);
        return ToDto(user);
    }

    public async Task<UserAccountDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = AccountRules.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        await users.AddAsync(user, cancellationToken);
        return ToDto(user);
    }

    public async Task<UserAccountDto> UpdateAsync(int adminId, int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var user = await users.GetForUpdateAsync(id, cancellationToken) ?? throw UserNotFound(id);

        // Prevents an admin from locking themselves out. Since no admin can demote or disable
        // themselves, there is always at least one active admin (the one making the change).
        if (id == adminId && (!request.IsActive || request.Role != UserRole.Admin))
        {
            throw new BusinessRuleException(SelfLockoutMessage);
        }

        var email = AccountRules.NormalizeEmail(request.Email);
        if (email != user.Email && await users.EmailTakenByOtherAsync(email, id, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (request.Password is not null)
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
        }

        await users.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public Task<IReadOnlyList<UserSummaryDto>> ListAssigneesAsync(CancellationToken cancellationToken = default) =>
        users.ListAssigneesAsync(cancellationToken);

    private static UserAccountDto ToDto(User user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.CreatedAt);

    private static NotFoundException UserNotFound(int id) => new($"User {id} was not found.");
}
