using FluentAssertions;
using FluentValidation;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Application.Users;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Auth;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Repositories;
using TicketFlow.Tests.Infrastructure;

namespace TicketFlow.Tests.Users;

/// <summary>User administration with the real repository over in-memory SQLite.</summary>
public sealed class UserServiceTests : IDisposable
{
    private const string Password = "Secret123";

    private readonly SqliteTestDatabase _database = new();
    private readonly TicketFlowDbContext _db;
    private readonly BCryptPasswordHasher _hasher = new();
    private readonly UserService _service;

    private readonly User _admin;
    private readonly User _tech;
    private readonly User _inactiveTech;
    private readonly User _requester;

    public UserServiceTests()
    {
        using (var setup = _database.CreateContext())
        {
            _admin = NewUser("Laura Admin", "laura@ticketflow.demo", UserRole.Admin);
            _tech = NewUser("Carlos Tech", "carlos@ticketflow.demo", UserRole.Technician);
            _inactiveTech = NewUser("Bruno Retired", "bruno@ticketflow.demo", UserRole.Technician, isActive: false);
            _requester = NewUser("Maria Requester", "maria@ticketflow.demo", UserRole.Requester);
            setup.Users.AddRange(_admin, _tech, _inactiveTech, _requester);
            setup.SaveChanges();
        }

        _db = _database.CreateContext();
        _service = new UserService(
            new UserRepository(_db),
            _hasher,
            new UserListQueryValidator(),
            new CreateUserRequestValidator(),
            new UpdateUserRequestValidator(),
            TimeProvider.System);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static User NewUser(string name, string email, UserRole role, bool isActive = true) => new()
    {
        FullName = name, Email = email, PasswordHash = "not-used", Role = role,
        IsActive = isActive, CreatedAt = DateTime.UtcNow,
    };

    private User Stored(int id)
    {
        using var db = _database.CreateContext();
        return db.Users.Single(u => u.Id == id);
    }

    private static UpdateUserRequest UpdateOf(User user, UserRole? role = null, bool? isActive = null, string? email = null, string? password = null) =>
        new(user.FullName, email ?? user.Email, role ?? user.Role, isActive ?? user.IsActive, password);

    [Fact]
    public async Task Admin_can_create_a_user_with_any_role_and_a_hashed_password()
    {
        var created = await _service.CreateAsync(new CreateUserRequest(" Ana Torres ", "Ana.Torres@TicketFlow.demo", Password, UserRole.Technician));

        created.FullName.Should().Be("Ana Torres");
        created.Email.Should().Be("ana.torres@ticketflow.demo");
        created.Role.Should().Be(UserRole.Technician);
        created.IsActive.Should().BeTrue();
        var stored = Stored(created.Id);
        stored.PasswordHash.Should().NotBe(Password);
        _hasher.Verify(Password, stored.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Create_with_an_existing_email_is_a_conflict()
    {
        var create = () => _service.CreateAsync(new CreateUserRequest("Copy", "CARLOS@ticketflow.demo", Password, UserRole.Requester));

        await create.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_applies_the_same_password_policy_as_registration()
    {
        var create = () => _service.CreateAsync(new CreateUserRequest("Weak", "weak@ticketflow.demo", "password", UserRole.Requester));

        var error = await create.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Update_changes_role_status_and_email()
    {
        var updated = await _service.UpdateAsync(_admin.Id, _requester.Id,
            UpdateOf(_requester, role: UserRole.Technician, isActive: false, email: " Maria.New@ticketflow.demo "));

        updated.Role.Should().Be(UserRole.Technician);
        updated.IsActive.Should().BeFalse();
        updated.Email.Should().Be("maria.new@ticketflow.demo");
        Stored(_requester.Id).PasswordHash.Should().Be("not-used", "the password only changes when one is sent");
    }

    [Fact]
    public async Task Update_with_a_password_resets_it()
    {
        await _service.UpdateAsync(_admin.Id, _tech.Id, UpdateOf(_tech, password: "NewSecret456"));

        _hasher.Verify("NewSecret456", Stored(_tech.Id).PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Update_rejects_a_weak_new_password()
    {
        var update = () => _service.UpdateAsync(_admin.Id, _tech.Id, UpdateOf(_tech, password: "short"));

        await update.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_to_an_email_of_another_user_is_a_conflict()
    {
        var update = () => _service.UpdateAsync(_admin.Id, _tech.Id, UpdateOf(_tech, email: "MARIA@ticketflow.demo"));

        await update.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData(UserRole.Admin, false)]
    [InlineData(UserRole.Technician, true)]
    [InlineData(UserRole.Requester, true)]
    public async Task Admin_cannot_lock_themselves_out(UserRole newRole, bool isActive)
    {
        var update = () => _service.UpdateAsync(_admin.Id, _admin.Id, UpdateOf(_admin, role: newRole, isActive: isActive));

        await update.Should().ThrowAsync<BusinessRuleException>().WithMessage(UserService.SelfLockoutMessage);
        Stored(_admin.Id).Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Admin_can_edit_their_own_name()
    {
        var updated = await _service.UpdateAsync(_admin.Id, _admin.Id,
            new UpdateUserRequest("Laura M. Admin", _admin.Email, UserRole.Admin, true));

        updated.FullName.Should().Be("Laura M. Admin");
    }

    [Fact]
    public async Task List_filters_by_role_status_and_search()
    {
        (await _service.ListAsync(new UserListQuery { Role = UserRole.Technician })).Items
            .Select(u => u.FullName).Should().Equal("Bruno Retired", "Carlos Tech");
        (await _service.ListAsync(new UserListQuery { Role = UserRole.Technician, IsActive = true })).Items
            .Select(u => u.FullName).Should().Equal("Carlos Tech");
        (await _service.ListAsync(new UserListQuery { Search = "maria@" })).Items
            .Select(u => u.FullName).Should().Equal("Maria Requester");
    }

    [Fact]
    public async Task List_is_paginated_and_ordered_by_name()
    {
        var page = await _service.ListAsync(new UserListQuery { Page = 2, PageSize = 3 });

        page.TotalCount.Should().Be(4);
        page.TotalPages.Should().Be(2);
        page.Items.Select(u => u.FullName).Should().Equal("Maria Requester");
    }

    [Fact]
    public async Task List_rejects_page_size_over_the_limit()
    {
        var list = () => _service.ListAsync(new UserListQuery { PageSize = 101 });

        await list.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Assignees_are_only_active_technicians_and_admins()
    {
        var assignees = await _service.ListAssigneesAsync();

        assignees.Select(u => u.Id).Should().Equal(_tech.Id, _admin.Id); // ordered by name
    }

    [Fact]
    public async Task Unknown_user_is_not_found()
    {
        await FluentActions.Awaiting(() => _service.GetAsync(999)).Should().ThrowAsync<NotFoundException>();
    }
}
