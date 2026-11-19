using FluentValidation;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    TimeProvider timeProvider) : IAuthService
{
    // Same message for unknown e-mail and wrong password, so the API does not reveal which e-mails exist.
    public const string InvalidCredentialsMessage = "Invalid email or password.";
    public const string DisabledAccountMessage = "This account is disabled. Contact an administrator.";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Requester, // self-registration never grants elevated roles
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        await users.AddAsync(user, cancellationToken);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        // Always verify (even for unknown e-mails) so both failures take the same time.
        var passwordIsValid = passwordHasher.Verify(request.Password, user?.PasswordHash);
        if (user is null || !passwordIsValid)
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        // Only reported after the password is verified, so it doesn't leak account state.
        if (!user.IsActive)
        {
            throw new ForbiddenException(DisabledAccountMessage);
        }

        return CreateResponse(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Read from the database (not only the token) so deactivations and role changes apply immediately.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("The user no longer exists or is disabled.");
        }

        return UserDto.FromEntity(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokenService.CreateAccessToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, UserDto.FromEntity(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
