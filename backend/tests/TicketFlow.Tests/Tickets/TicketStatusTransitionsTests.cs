using FluentAssertions;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Rules;

namespace TicketFlow.Tests.Tickets;

public class TicketStatusTransitionsTests
{
    // The only valid moves of the workflow; everything else must be rejected.
    private static readonly HashSet<(TicketStatus From, TicketStatus To)> ValidTransitions =
    [
        (TicketStatus.Open, TicketStatus.InProgress),
        (TicketStatus.InProgress, TicketStatus.OnHold),
        (TicketStatus.OnHold, TicketStatus.InProgress),
        (TicketStatus.InProgress, TicketStatus.Resolved),
        (TicketStatus.Resolved, TicketStatus.Closed),
    ];

    /// <summary>Every (from, to) combination of statuses: 25 cases.</summary>
    public static TheoryData<TicketStatus, TicketStatus, bool> AllPairs()
    {
        var data = new TheoryData<TicketStatus, TicketStatus, bool>();
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                data.Add(from, to, ValidTransitions.Contains((from, to)));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Only_workflow_transitions_are_allowed(TicketStatus from, TicketStatus to, bool expected)
    {
        TicketStatusTransitions.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void Staying_in_the_same_status_is_not_a_transition()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            TicketStatusTransitions.CanTransition(status, status).Should().BeFalse();
        }
    }

    [Fact]
    public void Closed_is_final()
    {
        TicketStatusTransitions.NextStatuses(TicketStatus.Closed).Should().BeEmpty();
    }

    [Fact]
    public void Next_statuses_agree_with_can_transition()
    {
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            var expected = Enum.GetValues<TicketStatus>().Where(to => TicketStatusTransitions.CanTransition(from, to));
            TicketStatusTransitions.NextStatuses(from).Should().BeEquivalentTo(expected);
        }
    }
}
