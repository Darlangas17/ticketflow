using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Tests.Infrastructure;

public class DemoDataSeederTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync()
    {
        await using var db = _database.CreateContext();
        await DemoDataSeeder.SeedAsync(db);
    }

    [Fact]
    public async Task Seed_creates_the_expected_amount_of_demo_data()
    {
        await SeedAsync();

        await using var db = _database.CreateContext();
        (await db.Branches.CountAsync()).Should().Be(3);
        (await db.Categories.CountAsync()).Should().Be(5);
        (await db.Tickets.CountAsync()).Should().Be(DemoDataSeeder.TicketCount);

        var usersByRole = await db.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count);

        usersByRole.Should().BeEquivalentTo(new Dictionary<UserRole, int>
        {
            [UserRole.Admin] = 1,
            [UserRole.Technician] = 2,
            [UserRole.Requester] = 3,
        });
    }

    [Fact]
    public async Task Seed_is_skipped_when_the_database_already_has_users()
    {
        await SeedAsync();

        await using var db = _database.CreateContext();
        var seededAgain = await DemoDataSeeder.SeedAsync(db);

        seededAgain.Should().BeFalse();
        (await db.Tickets.CountAsync()).Should().Be(DemoDataSeeder.TicketCount);
        (await db.Users.CountAsync()).Should().Be(6);
    }

    [Fact]
    public async Task Seed_covers_every_status_and_priority()
    {
        await SeedAsync();

        await using var db = _database.CreateContext();
        var statuses = await db.Tickets.Select(t => t.Status).Distinct().ToListAsync();
        var priorities = await db.Tickets.Select(t => t.Priority).Distinct().ToListAsync();

        statuses.Should().BeEquivalentTo(Enum.GetValues<TicketStatus>());
        priorities.Should().BeEquivalentTo(Enum.GetValues<TicketPriority>());
    }

    [Fact]
    public async Task Seeded_tickets_follow_the_business_rules()
    {
        await SeedAsync();

        await using var db = _database.CreateContext();
        var tickets = await db.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .Include(t => t.History)
            .AsSplitQuery()
            .ToListAsync();

        tickets.Should().AllSatisfy(t =>
        {
            t.Requester.Role.Should().Be(UserRole.Requester);
            t.UpdatedAt.Should().BeOnOrAfter(t.CreatedAt);
            t.UpdatedAt.Should().BeBefore(DateTime.UtcNow);

            if (t.Status != TicketStatus.Open)
            {
                t.Assignee.Should().NotBeNull("only open tickets may be unassigned");
            }

            if (t.Assignee is not null)
            {
                t.Assignee.Role.Should().Be(UserRole.Technician);
            }

            if (t.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                t.ResolvedAt.Should().NotBeNull().And.BeOnOrAfter(t.CreatedAt);
            }
            else
            {
                t.ResolvedAt.Should().BeNull();
            }

            // The last status change in the history must match the current status.
            var lastStatusChange = t.History
                .Where(h => h.Field == "Status")
                .OrderBy(h => h.ChangedAt)
                .LastOrDefault();
            (lastStatusChange?.NewValue ?? nameof(TicketStatus.Open)).Should().Be(t.Status.ToString());
        });

        tickets.Should().Contain(t => t.Assignee == null, "some open tickets are left unassigned");
    }

    [Fact]
    public async Task Seeded_passwords_are_stored_as_bcrypt_hashes()
    {
        await SeedAsync();

        await using var db = _database.CreateContext();
        var users = await db.Users.ToListAsync();

        users.Should().AllSatisfy(u =>
        {
            u.PasswordHash.Should().NotBe(DemoDataSeeder.DemoPassword);
            BCrypt.Net.BCrypt.Verify(DemoDataSeeder.DemoPassword, u.PasswordHash).Should().BeTrue();
        });
    }
}
