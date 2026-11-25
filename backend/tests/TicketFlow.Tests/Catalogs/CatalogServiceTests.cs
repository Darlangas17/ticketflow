using FluentAssertions;
using FluentValidation;
using TicketFlow.Application.Catalogs;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Repositories;
using TicketFlow.Tests.Infrastructure;

namespace TicketFlow.Tests.Catalogs;

/// <summary>Branch and category services with the real repositories over in-memory SQLite.</summary>
public sealed class CatalogServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly TicketFlowDbContext _db;
    private readonly BranchService _branches;
    private readonly CategoryService _categories;

    private readonly Branch _usedBranch;
    private readonly Category _usedCategory;

    public CatalogServiceTests()
    {
        using (var setup = _database.CreateContext())
        {
            // One branch and one category referenced by a ticket, so they cannot be deleted.
            var requester = new User
            {
                FullName = "Requester", Email = "requester@ticketflow.demo", PasswordHash = "x",
                Role = UserRole.Requester, CreatedAt = DateTime.UtcNow,
            };
            _usedBranch = new Branch { Name = "Tegucigalpa Central", City = "Tegucigalpa" };
            _usedCategory = new Category { Name = "Network", Description = "Connectivity" };
            setup.Tickets.Add(new Ticket
            {
                Title = "VPN down", Description = "No VPN.", Requester = requester,
                Branch = _usedBranch, Category = _usedCategory,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            setup.SaveChanges();
        }

        _db = _database.CreateContext();
        _branches = new BranchService(new BranchRepository(_db), new CreateBranchRequestValidator(), new UpdateBranchRequestValidator());
        _categories = new CategoryService(new CategoryRepository(_db), new SaveCategoryRequestValidator());
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    // ----- Branches -----

    [Fact]
    public async Task Create_branch_trims_and_returns_an_active_branch()
    {
        var branch = await _branches.CreateAsync(new CreateBranchRequest("  San Pedro Sula North ", " San Pedro Sula "));

        branch.Should().Be(new BranchDto(branch.Id, "San Pedro Sula North", "San Pedro Sula", true));
        (await _branches.GetAsync(branch.Id)).Should().Be(branch);
    }

    [Fact]
    public async Task Branch_names_are_unique_ignoring_case()
    {
        var create = () => _branches.CreateAsync(new CreateBranchRequest("TEGUCIGALPA central", "Tegucigalpa"));

        await create.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Renaming_a_branch_to_another_branch_name_is_a_conflict_but_keeping_its_own_is_not()
    {
        var other = await _branches.CreateAsync(new CreateBranchRequest("La Ceiba Coast", "La Ceiba"));

        var takeExisting = () => _branches.UpdateAsync(other.Id, new UpdateBranchRequest("Tegucigalpa Central", "La Ceiba", true));
        await takeExisting.Should().ThrowAsync<ConflictException>();

        var keepOwn = await _branches.UpdateAsync(other.Id, new UpdateBranchRequest("La Ceiba Coast", "La Ceiba City", true));
        keepOwn.City.Should().Be("La Ceiba City");
    }

    [Fact]
    public async Task Inactive_branches_are_hidden_unless_requested()
    {
        var closed = await _branches.CreateAsync(new CreateBranchRequest("Old Office", "Choluteca"));
        await _branches.UpdateAsync(closed.Id, new UpdateBranchRequest("Old Office", "Choluteca", IsActive: false));

        (await _branches.ListAsync(includeInactive: false)).Select(b => b.Name).Should().Equal("Tegucigalpa Central");
        (await _branches.ListAsync(includeInactive: true)).Select(b => b.Name).Should().Equal("Old Office", "Tegucigalpa Central");
    }

    [Fact]
    public async Task Branch_with_tickets_cannot_be_deleted_but_an_unused_one_can()
    {
        var unused = await _branches.CreateAsync(new CreateBranchRequest("Temporary", "Comayagua"));

        var deleteUsed = () => _branches.DeleteAsync(_usedBranch.Id);
        await deleteUsed.Should().ThrowAsync<ConflictException>().WithMessage("*Deactivate it instead*");

        await _branches.DeleteAsync(unused.Id);
        await FluentActions.Awaiting(() => _branches.GetAsync(unused.Id)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Branch_requires_name_and_city()
    {
        var create = () => _branches.CreateAsync(new CreateBranchRequest("", ""));

        var error = await create.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Select(e => e.PropertyName).Should().Contain(["Name", "City"]);
    }

    [Fact]
    public async Task Unknown_branch_is_not_found()
    {
        await FluentActions.Awaiting(() => _branches.UpdateAsync(999, new UpdateBranchRequest("X", "Y", true)))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => _branches.DeleteAsync(999)).Should().ThrowAsync<NotFoundException>();
    }

    // ----- Categories -----

    [Fact]
    public async Task Create_category_stores_blank_description_as_null()
    {
        var category = await _categories.CreateAsync(new SaveCategoryRequest(" Printers ", "   "));

        category.Should().Be(new CategoryDto(category.Id, "Printers", null));
    }

    [Fact]
    public async Task Category_names_are_unique_ignoring_case()
    {
        var create = () => _categories.CreateAsync(new SaveCategoryRequest("network", null));

        await create.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_category_changes_name_and_description()
    {
        var updated = await _categories.UpdateAsync(_usedCategory.Id, new SaveCategoryRequest("Network & VPN", "Internet and VPN"));

        updated.Should().Be(new CategoryDto(_usedCategory.Id, "Network & VPN", "Internet and VPN"));
        (await _categories.ListAsync()).Should().ContainSingle().Which.Should().Be(updated);
    }

    [Fact]
    public async Task Category_with_tickets_cannot_be_deleted_but_an_unused_one_can()
    {
        var unused = await _categories.CreateAsync(new SaveCategoryRequest("Phones", null));

        await FluentActions.Awaiting(() => _categories.DeleteAsync(_usedCategory.Id)).Should().ThrowAsync<ConflictException>();

        await _categories.DeleteAsync(unused.Id);
        (await _categories.ListAsync()).Select(c => c.Name).Should().Equal("Network");
    }

    [Fact]
    public async Task Category_description_is_limited_to_500_characters()
    {
        var create = () => _categories.CreateAsync(new SaveCategoryRequest("Long", new string('x', 501)));

        await create.Should().ThrowAsync<ValidationException>();
    }
}
