using FluentAssertions;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Tests.Tickets;

public class TicketAccessTests
{
    private static readonly Actor Admin = new(1, "Admin", UserRole.Admin);
    private static readonly Actor Tech = new(2, "Tech", UserRole.Technician);
    private static readonly Actor OtherTech = new(3, "Other Tech", UserRole.Technician);
    private static readonly Actor Requester = new(4, "Requester", UserRole.Requester);
    private static readonly Actor OtherRequester = new(5, "Other Requester", UserRole.Requester);

    // Ticket ids describe the case: requester 4 or 5, assigned to tech 2, tech 3 or nobody.
    private static readonly List<Ticket> Tickets =
    [
        Ticket(10, requesterId: 4, assigneeId: null),
        Ticket(11, requesterId: 4, assigneeId: 2),
        Ticket(12, requesterId: 4, assigneeId: 3),
        Ticket(13, requesterId: 5, assigneeId: null),
        Ticket(14, requesterId: 5, assigneeId: 2),
        Ticket(15, requesterId: 5, assigneeId: 3),
    ];

    private static Ticket Ticket(int id, int requesterId, int? assigneeId, TicketStatus status = TicketStatus.Open) =>
        new() { Id = id, RequesterId = requesterId, AssigneeId = assigneeId, Status = status };

    public static TheoryData<string, int[]> VisibilityCases() => new()
    {
        { "admin", [10, 11, 12, 13, 14, 15] },
        { "tech", [10, 11, 13, 14] },          // assigned to them + unassigned
        { "otherTech", [10, 12, 13, 15] },
        { "requester", [10, 11, 12] },         // only their own
        { "otherRequester", [13, 14, 15] },
    };

    private static Actor ActorNamed(string name) => name switch
    {
        "admin" => Admin,
        "tech" => Tech,
        "otherTech" => OtherTech,
        "requester" => Requester,
        _ => OtherRequester,
    };

    [Theory]
    [MemberData(nameof(VisibilityCases))]
    public void Query_filter_returns_only_the_tickets_the_role_may_see(string actorName, int[] expectedIds)
    {
        var visible = Tickets.AsQueryable().Where(TicketAccess.VisibleTo(ActorNamed(actorName)));

        visible.Select(t => t.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Theory]
    [MemberData(nameof(VisibilityCases))]
    public void In_memory_check_agrees_with_the_query_filter(string actorName, int[] expectedIds)
    {
        var actor = ActorNamed(actorName);

        Tickets.Where(t => TicketAccess.CanView(actor, t)).Select(t => t.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.OnHold)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    public void Staff_can_make_any_status_change(TicketStatus from, TicketStatus to)
    {
        var ticket = Ticket(1, requesterId: 4, assigneeId: 2, from);

        TicketAccess.CanChangeStatus(Tech, ticket, to).Should().BeTrue();
        TicketAccess.CanChangeStatus(Admin, ticket, to).Should().BeTrue();
    }

    [Fact]
    public void Requester_can_close_their_own_resolved_ticket()
    {
        var ticket = Ticket(1, requesterId: Requester.Id, assigneeId: 2, TicketStatus.Resolved);

        TicketAccess.CanChangeStatus(Requester, ticket, TicketStatus.Closed).Should().BeTrue();
        TicketAccess.CanChangeStatus(OtherRequester, ticket, TicketStatus.Closed).Should().BeFalse();
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.OnHold)]
    public void Requester_cannot_work_on_their_own_ticket(TicketStatus from, TicketStatus to)
    {
        var ticket = Ticket(1, requesterId: Requester.Id, assigneeId: 2, from);

        TicketAccess.CanChangeStatus(Requester, ticket, to).Should().BeFalse();
    }

    [Fact]
    public void Only_staff_can_assign_and_only_admins_can_delete()
    {
        TicketAccess.CanAssign(Admin).Should().BeTrue();
        TicketAccess.CanAssign(Tech).Should().BeTrue();
        TicketAccess.CanAssign(Requester).Should().BeFalse();

        TicketAccess.CanDelete(Admin).Should().BeTrue();
        TicketAccess.CanDelete(Tech).Should().BeFalse();
        TicketAccess.CanDelete(Requester).Should().BeFalse();
    }
}
