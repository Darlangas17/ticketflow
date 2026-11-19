using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Tests.Auth;

public class AuthServiceTests
{
    private const string Password = "Secret123";

    private readonly FakeUserRepository _users = new();
    private readonly BCryptPasswordHasher _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var tokenService = new JwtTokenService(Options.Create(TestJwt.Options), TimeProvider.System);
        _service = new AuthService(
            _users,
            _hasher,
            tokenService,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            TimeProvider.System);
    }

    private User AddUser(string email, bool isActive = true, UserRole role = UserRole.Technician)
    {
        var user = new User
        {
            FullName = "Test User",
            Email = email,
            PasswordHash = _hasher.Hash(Password),
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        _users.Add(user);
        return user;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_user()
    {
        var user = AddUser("tech@ticketflow.demo");

        var response = await _service.LoginAsync(new LoginRequest("tech@ticketflow.demo", Password));

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        response.User.Should().Be(new UserDto(user.Id, user.FullName, user.Email, UserRole.Technician));
    }

    [Fact]
    public async Task Login_ignores_email_case_and_surrounding_spaces()
    {
        AddUser("tech@ticketflow.demo");

        var response = await _service.LoginAsync(new LoginRequest("  TECH@TicketFlow.demo ", Password));

        response.User.Email.Should().Be("tech@ticketflow.demo");
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        AddUser("tech@ticketflow.demo");

        var login = () => _service.LoginAsync(new LoginRequest("tech@ticketflow.demo", "WrongPass1"));

        await login.Should().ThrowAsync<UnauthorizedException>().WithMessage(AuthService.InvalidCredentialsMessage);
    }

    [Fact]
    public async Task Login_with_unknown_email_gives_the_same_error_as_a_wrong_password()
    {
        var login = () => _service.LoginAsync(new LoginRequest("nobody@ticketflow.demo", Password));

        await login.Should().ThrowAsync<UnauthorizedException>().WithMessage(AuthService.InvalidCredentialsMessage);
    }

    [Fact]
    public async Task Login_of_inactive_user_is_forbidden()
    {
        AddUser("inactive@ticketflow.demo", isActive: false);

        var login = () => _service.LoginAsync(new LoginRequest("inactive@ticketflow.demo", Password));

        await login.Should().ThrowAsync<ForbiddenException>().WithMessage(AuthService.DisabledAccountMessage);
    }

    [Fact]
    public async Task Inactive_user_with_wrong_password_does_not_learn_the_account_is_disabled()
    {
        AddUser("inactive@ticketflow.demo", isActive: false);

        var login = () => _service.LoginAsync(new LoginRequest("inactive@ticketflow.demo", "WrongPass1"));

        await login.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Register_creates_an_active_requester_with_a_hashed_password()
    {
        var response = await _service.RegisterAsync(new RegisterRequest(" New User ", "New@TicketFlow.demo", Password));

        var stored = _users.Single();
        stored.Role.Should().Be(UserRole.Requester);
        stored.IsActive.Should().BeTrue();
        stored.FullName.Should().Be("New User");
        stored.Email.Should().Be("new@ticketflow.demo");
        stored.PasswordHash.Should().NotBe(Password);
        _hasher.Verify(Password, stored.PasswordHash).Should().BeTrue();
        response.User.Role.Should().Be(UserRole.Requester);
    }

    [Fact]
    public async Task Register_with_an_existing_email_is_a_conflict()
    {
        AddUser("taken@ticketflow.demo");

        var register = () => _service.RegisterAsync(new RegisterRequest("Someone", "TAKEN@ticketflow.demo", Password));

        await register.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("", "valid@ticketflow.demo", "Secret123", "FullName")]
    [InlineData("Name", "not-an-email", "Secret123", "Email")]
    [InlineData("Name", "valid@ticketflow.demo", "short1", "Password")]
    [InlineData("Name", "valid@ticketflow.demo", "onlyletters", "Password")]
    [InlineData("Name", "valid@ticketflow.demo", "12345678", "Password")]
    public async Task Register_rejects_invalid_input(string fullName, string email, string password, string invalidField)
    {
        var register = () => _service.RegisterAsync(new RegisterRequest(fullName, email, password));

        var error = await register.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Should().Contain(e => e.PropertyName == invalidField);
        _users.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_rejects_passwords_longer_than_bcrypt_supports()
    {
        var tooLong = new string('a', 72) + "1";

        var register = () => _service.RegisterAsync(new RegisterRequest("Name", "long@ticketflow.demo", tooLong));

        await register.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Current_user_of_a_deactivated_account_is_rejected()
    {
        var user = AddUser("tech@ticketflow.demo");
        user.IsActive = false;

        var me = () => _service.GetCurrentUserAsync(user.Id);

        await me.Should().ThrowAsync<UnauthorizedException>();
    }

    private sealed class FakeUserRepository : List<User>, IUserRepository
    {
        public new void Add(User user)
        {
            user.Id = Count + 1;
            base.Add(user);
        }

        public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.FirstOrDefault(u => u.Email == email));

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(this.Any(u => u.Email == email));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Add(user);
            return Task.CompletedTask;
        }
    }
}
