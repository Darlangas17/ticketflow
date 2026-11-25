using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Repositories;
using TicketFlow.Tests.Infrastructure;

namespace TicketFlow.Tests.Tickets;

/// <summary>
/// TicketService with the real EF Core repositories over in-memory SQLite, so the
/// rules are tested together with the SQL that enforces them (filters, projections, history).
/// </summary>
public sealed class TicketServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase _database = new();
    private readonly TicketFlowDbContext _db;
    private readonly TicketService _service;

    private readonly User _admin;
    private readonly User _tech;
    private readonly User _otherTech;
    private readonly User _inactiveTech;
    private readonly User _requester;
    private readonly User _otherRequester;
    private readonly Branch _branch;
    private readonly Branch _inactiveBranch;
    private readonly Category _network;
    private readonly Category _hardware;

    public TicketServiceTests()
    {
        using (var setup = _database.CreateContext())
        {
            _admin = NewUser("Admin User", UserRole.Admin);
            _tech = NewUser("Tech One", UserRole.Technician);
            _otherTech = NewUser("Tech Two", UserRole.Technician);
            _inactiveTech = NewUser("Tech Retired", UserRole.Technician, isActive: false);
            _requester = NewUser("Requester One", UserRole.Requester);
            _otherRequester = NewUser("Requester Two", UserRole.Requester);
            _branch = new Branch { Name = "Main", City = "Tegucigalpa" };
            _inactiveBranch = new Branch { Name = "Closed Office", City = "La Ceiba", IsActive = false };
            _network = new Category { Name = "Network" };
            _hardware = new Category { Name = "Hardware" };

            setup.AddRange(_admin, _tech, _otherTech, _inactiveTech, _requester, _otherRequester);
            setup.AddRange(_branch, _inactiveBranch, _network, _hardware);
            setup.SaveChanges();
        }

        _db = _database.CreateContext();
        _service = new TicketService(
            new TicketRepository(_db),
            new UserRepository(_db),
            new TicketListQueryValidator(),
            new CreateTicketRequestValidator(),
            new UpdateTicketRequestValidator(),
            new ChangeTicketStatusRequestValidator(),
            new AssignTicketRequestValidator(),
            new AddCommentRequestValidator(),
            new FixedTimeProvider(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static User NewUser(string name, UserRole role, bool isActive = true) => new()
    {
        FullName = name,
        Email = name.Replace(' ', '.').ToLowerInvariant() + "@ticketflow.demo",
        PasswordHash = "not-used",
        Role = role,
        IsActive = isActive,
        CreatedAt = Now.AddDays(-30),
    };

    /// <summary>Inserts a ticket directly (bypassing the service) in any state.</summary>
    private int AddTicket(
        User requester,
        User? assignee = null,
        TicketStatus status = TicketStatus.Open,
        TicketPriority priority = TicketPriority.Medium,
        string title = "Printer offline",
        string description = "The printer shows offline.",
        Category? category = null,
        DateTime? createdAt = null)
    {
        using var db = _database.CreateContext();
        var ticket = new Ticket
        {
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            CategoryId = (category ?? _network).Id,
            BranchId = _branch.Id,
            RequesterId = requester.Id,
            AssigneeId = assignee?.Id,
            CreatedAt = createdAt ?? Now.AddDays(-1),
            UpdatedAt = createdAt ?? Now.AddDays(-1),
        };
        db.Tickets.Add(ticket);
        db.SaveChanges();
        return ticket.Id;
    }

    private List<TicketHistory> HistoryOf(int ticketId)
    {
        using var db = _database.CreateContext();
        return db.TicketHistory.Where(h => h.TicketId == ticketId).OrderBy(h => h.Id).ToList();
    }

    // ----- Status workflow -----

    [Fact]
    public async Task Valid_status_change_updates_the_ticket_and_writes_history()
    {
        var id = AddTicket(_requester, _tech);

        var result = await _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest(TicketStatus.InProgress));

        result.Status.Should().Be(TicketStatus.InProgress);
        result.UpdatedAt.Should().Be(Now);
        result.AllowedNextStatuses.Should().BeEquivalentTo([TicketStatus.OnHold, TicketStatus.Resolved]);
        var history = HistoryOf(id).Should().ContainSingle().Subject;
        history.Field.Should().Be(TicketService.StatusField);
        history.OldValue.Should().Be("Open");
        history.NewValue.Should().Be("InProgress");
        history.ChangedById.Should().Be(_tech.Id);
        history.ChangedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Resolving_sets_resolved_at()
    {
        var id = AddTicket(_requester, _tech, TicketStatus.InProgress);

        var result = await _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest(TicketStatus.Resolved));

        result.ResolvedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Full_workflow_with_on_hold_records_every_step()
    {
        var id = AddTicket(_requester, _tech);
        TicketStatus[] steps =
        [
            TicketStatus.InProgress, TicketStatus.OnHold, TicketStatus.InProgress,
            TicketStatus.Resolved, TicketStatus.Closed,
        ];

        foreach (var step in steps)
        {
            await _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest(step));
        }

        var details = await _service.GetAsync(_admin.Id, id);
        details.Status.Should().Be(TicketStatus.Closed);
        details.AllowedNextStatuses.Should().BeEmpty();
        details.History.Select(h => h.NewValue).Should().Equal(steps.Select(s => s.ToString()));
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.Open, TicketStatus.OnHold)]
    [InlineData(TicketStatus.OnHold, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress, TicketStatus.InProgress)]
    public async Task Invalid_transition_is_rejected_and_nothing_changes(TicketStatus from, TicketStatus to)
    {
        var id = AddTicket(_requester, _tech, from);

        var change = () => _service.ChangeStatusAsync(_admin.Id, id, new ChangeTicketStatusRequest(to));

        await change.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage($"Invalid status transition from {from} to {to}*");
        HistoryOf(id).Should().BeEmpty();
        (await _service.GetAsync(_admin.Id, id)).Status.Should().Be(from);
    }

    [Fact]
    public async Task Requester_cannot_start_work_on_their_own_ticket()
    {
        var id = AddTicket(_requester);

        var change = () => _service.ChangeStatusAsync(_requester.Id, id, new ChangeTicketStatusRequest(TicketStatus.InProgress));

        await change.Should().ThrowAsync<ForbiddenException>();
        HistoryOf(id).Should().BeEmpty();
    }

    [Fact]
    public async Task Requester_can_close_their_own_resolved_ticket()
    {
        var id = AddTicket(_requester, _tech, TicketStatus.Resolved);

        var result = await _service.ChangeStatusAsync(_requester.Id, id, new ChangeTicketStatusRequest(TicketStatus.Closed));

        result.Status.Should().Be(TicketStatus.Closed);
        HistoryOf(id).Single().ChangedById.Should().Be(_requester.Id);
    }

    [Fact]
    public async Task Undefined_status_value_is_a_validation_error()
    {
        var id = AddTicket(_requester, _tech);

        var change = () => _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest((TicketStatus)99));

        await change.Should().ThrowAsync<ValidationException>();
    }

    // ----- Visibility -----

    [Fact]
    public async Task List_returns_only_the_tickets_each_role_may_see()
    {
        var ownUnassigned = AddTicket(_requester);
        var ownAssignedToTech = AddTicket(_requester, _tech);
        var otherAssignedToOtherTech = AddTicket(_otherRequester, _otherTech);
        var otherAssignedToTech = AddTicket(_otherRequester, _tech);

        async Task<IEnumerable<int>> VisibleIds(User user) =>
            (await _service.ListAsync(user.Id, new TicketListQuery())).Items.Select(t => t.Id);

        (await VisibleIds(_admin)).Should().BeEquivalentTo([ownUnassigned, ownAssignedToTech, otherAssignedToOtherTech, otherAssignedToTech]);
        (await VisibleIds(_tech)).Should().BeEquivalentTo([ownUnassigned, ownAssignedToTech, otherAssignedToTech]);
        (await VisibleIds(_otherTech)).Should().BeEquivalentTo([ownUnassigned, otherAssignedToOtherTech]);
        (await VisibleIds(_requester)).Should().BeEquivalentTo([ownUnassigned, ownAssignedToTech]);
        (await VisibleIds(_otherRequester)).Should().BeEquivalentTo([otherAssignedToOtherTech, otherAssignedToTech]);
    }

    [Fact]
    public async Task Ticket_of_another_requester_is_reported_as_not_found()
    {
        var id = AddTicket(_otherRequester);

        await FluentActions.Awaiting(() => _service.GetAsync(_requester.Id, id))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => _service.AddCommentAsync(_requester.Id, id, new AddCommentRequest("Hello")))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => _service.UpdateAsync(_requester.Id, id, new UpdateTicketRequest("x", "y", TicketPriority.Low, _network.Id)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Technician_cannot_touch_a_ticket_assigned_to_another_technician()
    {
        var id = AddTicket(_requester, _otherTech);

        var change = () => _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest(TicketStatus.InProgress));

        await change.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deactivated_user_cannot_use_an_existing_token()
    {
        var list = () => _service.ListAsync(_inactiveTech.Id, new TicketListQuery());

        await list.Should().ThrowAsync<UnauthorizedException>();
    }

    // ----- Create / update -----

    [Fact]
    public async Task Create_opens_a_ticket_on_behalf_of_the_caller()
    {
        var result = await _service.CreateAsync(
            _requester.Id, new CreateTicketRequest("  VPN down  ", "Cannot connect.", null, _network.Id, _branch.Id));

        result.Title.Should().Be("VPN down");
        result.Status.Should().Be(TicketStatus.Open);
        result.Priority.Should().Be(TicketPriority.Medium);
        result.Requester.Should().Be(new UserSummaryDto(_requester.Id, _requester.FullName));
        result.Assignee.Should().BeNull();
        result.Category.Should().Be(new CatalogItemDto(_network.Id, "Network"));
        result.CreatedAt.Should().Be(Now);
        result.AllowedNextStatuses.Should().Equal(TicketStatus.InProgress);
    }

    [Fact]
    public async Task Create_rejects_unknown_category_and_inactive_branch()
    {
        var unknownCategory = () => _service.CreateAsync(
            _requester.Id, new CreateTicketRequest("T", "D", null, 999, _branch.Id));
        var inactiveBranch = () => _service.CreateAsync(
            _requester.Id, new CreateTicketRequest("T", "D", null, _network.Id, _inactiveBranch.Id));

        await unknownCategory.Should().ThrowAsync<BusinessRuleException>().WithMessage("Category 999*");
        await inactiveBranch.Should().ThrowAsync<BusinessRuleException>().WithMessage("Branch*");
    }

    [Fact]
    public async Task Create_validates_required_fields()
    {
        var create = () => _service.CreateAsync(
            _requester.Id, new CreateTicketRequest("", "", null, 0, 0));

        var error = await create.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Select(e => e.PropertyName).Should()
            .Contain(["Title", "Description", "CategoryId", "BranchId"]);
    }

    [Fact]
    public async Task Update_records_priority_changes_but_not_text_edits()
    {
        var id = AddTicket(_requester, priority: TicketPriority.Low);

        var result = await _service.UpdateAsync(
            _requester.Id, id, new UpdateTicketRequest("New title", "New description", TicketPriority.High, _hardware.Id));

        result.Title.Should().Be("New title");
        result.Category.Name.Should().Be("Hardware");
        result.Priority.Should().Be(TicketPriority.High);
        var history = HistoryOf(id).Should().ContainSingle().Subject;
        history.Field.Should().Be(TicketService.PriorityField);
        history.OldValue.Should().Be("Low");
        history.NewValue.Should().Be("High");
    }

    [Fact]
    public async Task Closed_ticket_cannot_be_edited()
    {
        var id = AddTicket(_requester, _tech, TicketStatus.Closed);

        var update = () => _service.UpdateAsync(
            _requester.Id, id, new UpdateTicketRequest("x", "y", TicketPriority.Low, _network.Id));

        await update.Should().ThrowAsync<BusinessRuleException>();
    }

    // ----- Assignment -----

    [Fact]
    public async Task Assign_records_old_and_new_assignee_names()
    {
        var id = AddTicket(_requester, _tech);

        var result = await _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(_otherTech.Id));

        result.Assignee.Should().Be(new UserSummaryDto(_otherTech.Id, _otherTech.FullName));
        var history = HistoryOf(id).Should().ContainSingle().Subject;
        history.Field.Should().Be(TicketService.AssigneeField);
        history.OldValue.Should().Be(_tech.FullName);
        history.NewValue.Should().Be(_otherTech.FullName);
    }

    [Fact]
    public async Task Technician_can_take_an_unassigned_ticket_and_hand_it_over()
    {
        var id = AddTicket(_requester);

        var taken = await _service.AssignAsync(_tech.Id, id, new AssignTicketRequest(_tech.Id));
        var handedOver = await _service.AssignAsync(_tech.Id, id, new AssignTicketRequest(_otherTech.Id));

        taken.Assignee!.Id.Should().Be(_tech.Id);
        handedOver.Assignee!.Id.Should().Be(_otherTech.Id);
        HistoryOf(id).Select(h => (h.OldValue, h.NewValue)).Should().Equal(
            (null, _tech.FullName),
            (_tech.FullName, _otherTech.FullName));

        // After handing it over the ticket is no longer visible to the first technician.
        await FluentActions.Awaiting(() => _service.GetAsync(_tech.Id, id)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Assigning_the_same_user_again_does_not_write_history()
    {
        var id = AddTicket(_requester, _tech);

        await _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(_tech.Id));

        HistoryOf(id).Should().BeEmpty();
    }

    [Fact]
    public async Task Unassigning_records_a_null_new_value()
    {
        var id = AddTicket(_requester, _tech);

        var result = await _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(null));

        result.Assignee.Should().BeNull();
        HistoryOf(id).Single().NewValue.Should().BeNull();
    }

    [Fact]
    public async Task Tickets_can_only_be_assigned_to_active_staff()
    {
        var id = AddTicket(_requester);

        await FluentActions.Awaiting(() => _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(_requester.Id)))
            .Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(_inactiveTech.Id)))
            .Should().ThrowAsync<BusinessRuleException>();
        await FluentActions.Awaiting(() => _service.AssignAsync(_admin.Id, id, new AssignTicketRequest(12345)))
            .Should().ThrowAsync<BusinessRuleException>();
        HistoryOf(id).Should().BeEmpty();
    }

    [Fact]
    public async Task Requester_cannot_assign()
    {
        var id = AddTicket(_requester);

        var assign = () => _service.AssignAsync(_requester.Id, id, new AssignTicketRequest(_tech.Id));

        await assign.Should().ThrowAsync<ForbiddenException>();
    }

    // ----- Comments and delete -----

    [Fact]
    public async Task Comment_is_added_with_its_author_and_shows_in_details()
    {
        var id = AddTicket(_requester, _tech);

        var comment = await _service.AddCommentAsync(_tech.Id, id, new AddCommentRequest(" Checking it now. "));

        comment.Body.Should().Be("Checking it now.");
        comment.Author.Should().Be(new UserSummaryDto(_tech.Id, _tech.FullName));
        var details = await _service.GetAsync(_requester.Id, id);
        details.Comments.Should().ContainSingle().Which.Should().Be(comment);
    }

    [Fact]
    public async Task Only_admin_can_delete_and_delete_removes_comments_and_history()
    {
        var id = AddTicket(_requester, _tech);
        await _service.AddCommentAsync(_tech.Id, id, new AddCommentRequest("Note"));
        await _service.ChangeStatusAsync(_tech.Id, id, new ChangeTicketStatusRequest(TicketStatus.InProgress));

        await FluentActions.Awaiting(() => _service.DeleteAsync(_tech.Id, id)).Should().ThrowAsync<ForbiddenException>();

        await _service.DeleteAsync(_admin.Id, id);

        using var db = _database.CreateContext();
        (await db.Tickets.AnyAsync(t => t.Id == id)).Should().BeFalse();
        (await db.TicketComments.AnyAsync(c => c.TicketId == id)).Should().BeFalse();
        (await db.TicketHistory.AnyAsync(h => h.TicketId == id)).Should().BeFalse();
    }

    // ----- Filters, search, sort and pagination -----

    [Fact]
    public async Task List_filters_by_status_priority_category_and_assignee()
    {
        var match = AddTicket(_requester, _tech, TicketStatus.InProgress, TicketPriority.High, category: _hardware);
        AddTicket(_requester, _tech, TicketStatus.Open, TicketPriority.High, category: _hardware);
        AddTicket(_requester, _tech, TicketStatus.InProgress, TicketPriority.Low, category: _hardware);
        AddTicket(_requester, _tech, TicketStatus.InProgress, TicketPriority.High, category: _network);
        AddTicket(_requester, _otherTech, TicketStatus.InProgress, TicketPriority.High, category: _hardware);

        var result = await _service.ListAsync(_admin.Id, new TicketListQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CategoryId = _hardware.Id,
            AssigneeId = _tech.Id,
            BranchId = _branch.Id,
        });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(match);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_matches_title_description_or_ticket_number()
    {
        var byTitle = AddTicket(_requester, title: "VPN keeps dropping");
        var byDescription = AddTicket(_requester, title: "Remote work issue", description: "The VPN client crashes.");
        var byNumber = AddTicket(_requester, title: "Monitor flickers");

        async Task<IEnumerable<int>> Search(string text) =>
            (await _service.ListAsync(_admin.Id, new TicketListQuery { Search = text })).Items.Select(t => t.Id);

        (await Search("VPN")).Should().BeEquivalentTo([byTitle, byDescription]);
        (await Search($"#{byNumber}")).Should().Equal(byNumber);
    }

    [Fact]
    public async Task Priority_sort_uses_business_order_not_alphabetical()
    {
        AddTicket(_requester, priority: TicketPriority.Medium);
        AddTicket(_requester, priority: TicketPriority.Critical);
        AddTicket(_requester, priority: TicketPriority.Low);
        AddTicket(_requester, priority: TicketPriority.High);

        var result = await _service.ListAsync(_admin.Id, new TicketListQuery { Sort = "-priority" });

        result.Items.Select(t => t.Priority).Should().Equal(
            TicketPriority.Critical, TicketPriority.High, TicketPriority.Medium, TicketPriority.Low);
    }

    [Fact]
    public async Task Pagination_returns_the_requested_page_newest_first_by_default()
    {
        var ids = Enumerable.Range(1, 5)
            .Select(day => AddTicket(_requester, createdAt: Now.AddDays(-day)))
            .ToList(); // ids[0] is the newest

        var page2 = await _service.ListAsync(_admin.Id, new TicketListQuery { Page = 2, PageSize = 2 });

        page2.Items.Select(t => t.Id).Should().Equal(ids[2], ids[3]);
        page2.TotalCount.Should().Be(5);
        page2.TotalPages.Should().Be(3);
        page2.Page.Should().Be(2);
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, "colour")]
    [InlineData(1, 20, "--priority")]
    public async Task Invalid_list_parameters_are_rejected(int page, int pageSize, string? sort)
    {
        var list = () => _service.ListAsync(_admin.Id, new TicketListQuery { Page = page, PageSize = pageSize, Sort = sort });

        await list.Should().ThrowAsync<ValidationException>();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
