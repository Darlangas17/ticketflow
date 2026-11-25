using FluentAssertions;
using TicketFlow.Application.Dashboard;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Repositories;
using TicketFlow.Tests.Infrastructure;

namespace TicketFlow.Tests.Dashboard;

/// <summary>Dashboard aggregates over a small, known set of tickets in in-memory SQLite.</summary>
public sealed class DashboardServiceTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase _database = new();
    private readonly TicketFlowDbContext _db;
    private readonly DashboardService _service;

    private readonly User _admin = NewUser("Admin", UserRole.Admin);
    private readonly User _tech = NewUser("Tech", UserRole.Technician);
    private readonly User _requester = NewUser("Requester", UserRole.Requester);
    private readonly User _otherRequester = NewUser("Other Requester", UserRole.Requester);

    private readonly Branch _central = new() { Name = "Central", City = "Tegucigalpa" };
    private readonly Branch _north = new() { Name = "North", City = "San Pedro Sula" };
    private readonly Branch _closedWithTickets = new() { Name = "Old Office", City = "Choluteca", IsActive = false };
    private readonly Branch _closedEmpty = new() { Name = "Never Used", City = "Danli", IsActive = false };

    private readonly Category _network = new() { Name = "Network" };
    private readonly Category _hardware = new() { Name = "Hardware" };
    private readonly Category _printers = new() { Name = "Printers" };

    public DashboardServiceTests()
    {
        using (var setup = _database.CreateContext())
        {
            setup.AddRange(_admin, _tech, _requester, _otherRequester);
            setup.AddRange(_central, _north, _closedWithTickets, _closedEmpty, _network, _hardware, _printers);

            // Requester: 4 tickets. Other requester: 2 tickets. Resolution times: 2 h and 6 h.
            setup.Tickets.AddRange(
                NewTicket(_requester, TicketStatus.Open, TicketPriority.High, _central, _network, assignee: null),
                NewTicket(_requester, TicketStatus.InProgress, TicketPriority.Critical, _central, _network, _tech),
                NewTicket(_requester, TicketStatus.Resolved, TicketPriority.Low, _north, _hardware, _tech, resolvedAfterHours: 2),
                NewTicket(_requester, TicketStatus.Closed, TicketPriority.High, _closedWithTickets, _hardware, _tech, resolvedAfterHours: 6),
                NewTicket(_otherRequester, TicketStatus.OnHold, TicketPriority.Medium, _north, _network, _tech),
                NewTicket(_otherRequester, TicketStatus.Open, TicketPriority.High, _north, _network, assignee: null));
            setup.SaveChanges();
        }

        _db = _database.CreateContext();
        _service = new DashboardService(new DashboardRepository(_db), new UserRepository(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static User NewUser(string name, UserRole role) => new()
    {
        FullName = name, Email = name.Replace(' ', '.').ToLowerInvariant() + "@ticketflow.demo",
        PasswordHash = "x", Role = role, CreatedAt = Start,
    };

    private static Ticket NewTicket(
        User requester, TicketStatus status, TicketPriority priority, Branch branch, Category category,
        User? assignee, int? resolvedAfterHours = null) => new()
    {
        Title = "Ticket", Description = "Description", Status = status, Priority = priority,
        Branch = branch, Category = category, Requester = requester, Assignee = assignee,
        CreatedAt = Start, UpdatedAt = Start,
        ResolvedAt = resolvedAfterHours is { } hours ? Start.AddHours(hours) : null,
    };

    [Fact]
    public async Task Admin_summary_counts_every_ticket()
    {
        var summary = await _service.GetSummaryAsync(_admin.Id);

        summary.TotalTickets.Should().Be(6);
        summary.ActiveTickets.Should().Be(4);       // Open, InProgress, OnHold, Open
        summary.UnassignedTickets.Should().Be(2);   // the two Open tickets without assignee
        summary.ResolvedTickets.Should().Be(2);
        summary.AverageResolutionHours.Should().Be(4); // (2 h + 6 h) / 2
    }

    [Fact]
    public async Task By_status_and_priority_list_every_value_in_order_including_zeros()
    {
        var summary = await _service.GetSummaryAsync(_admin.Id);

        summary.ByStatus.Should().Equal(
            new StatusCountDto(TicketStatus.Open, 2),
            new StatusCountDto(TicketStatus.InProgress, 1),
            new StatusCountDto(TicketStatus.OnHold, 1),
            new StatusCountDto(TicketStatus.Resolved, 1),
            new StatusCountDto(TicketStatus.Closed, 1));
        summary.ByPriority.Should().Equal(
            new PriorityCountDto(TicketPriority.Low, 1),
            new PriorityCountDto(TicketPriority.Medium, 1),
            new PriorityCountDto(TicketPriority.High, 3),
            new PriorityCountDto(TicketPriority.Critical, 1));
    }

    [Fact]
    public async Task By_branch_includes_inactive_branches_only_when_they_have_tickets()
    {
        var summary = await _service.GetSummaryAsync(_admin.Id);

        summary.ByBranch.Select(b => (b.Name, b.Count)).Should().Equal(
            ("North", 3), ("Central", 2), ("Old Office", 1));
    }

    [Fact]
    public async Task By_category_includes_categories_without_tickets()
    {
        var summary = await _service.GetSummaryAsync(_admin.Id);

        summary.ByCategory.Select(c => (c.Name, c.Count)).Should().Equal(
            ("Network", 4), ("Hardware", 2), ("Printers", 0));
    }

    [Fact]
    public async Task Requester_summary_only_counts_their_own_tickets()
    {
        var summary = await _service.GetSummaryAsync(_requester.Id);

        summary.TotalTickets.Should().Be(4);
        summary.UnassignedTickets.Should().Be(1);
        summary.AverageResolutionHours.Should().Be(4);
        summary.ByStatus.Single(s => s.Status == TicketStatus.OnHold).Count.Should().Be(0);
    }

    [Fact]
    public async Task Technician_summary_counts_assigned_and_unassigned_tickets()
    {
        var summary = await _service.GetSummaryAsync(_tech.Id);

        summary.TotalTickets.Should().Be(6); // all six are either assigned to them or unassigned
    }

    [Fact]
    public async Task Average_resolution_is_null_when_nothing_was_resolved()
    {
        var summary = await _service.GetSummaryAsync(_otherRequester.Id);

        summary.TotalTickets.Should().Be(2);
        summary.ResolvedTickets.Should().Be(0);
        summary.AverageResolutionHours.Should().BeNull();
    }
}
