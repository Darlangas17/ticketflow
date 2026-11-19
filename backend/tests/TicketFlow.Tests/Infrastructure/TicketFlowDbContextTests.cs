using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Tests.Infrastructure;

public class TicketFlowDbContextTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static User NewUser(string email, bool isActive = true) => new()
    {
        FullName = "Test User",
        Email = email,
        PasswordHash = "not-a-real-hash",
        Role = UserRole.Requester,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Email_must_be_unique()
    {
        await using var db = _database.CreateContext();
        db.Users.AddRange(NewUser("same@ticketflow.demo"), NewUser("same@ticketflow.demo"));

        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Inactive_user_is_saved_as_inactive_despite_the_database_default()
    {
        await using (var db = _database.CreateContext())
        {
            db.Users.Add(NewUser("inactive@ticketflow.demo", isActive: false));
            await db.SaveChangesAsync();
        }

        await using var readDb = _database.CreateContext();
        var user = await readDb.Users.SingleAsync();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Enums_are_stored_as_text()
    {
        await using (var db = _database.CreateContext())
        {
            db.Users.Add(NewUser("enum@ticketflow.demo"));
            await db.SaveChangesAsync();
        }

        await using var readDb = _database.CreateContext();
        var role = await readDb.Database
            .SqlQuery<string>($"SELECT Role AS Value FROM Users")
            .SingleAsync();
        role.Should().Be("Requester");
    }

    [Fact]
    public async Task Dates_are_read_back_as_utc()
    {
        await using (var db = _database.CreateContext())
        {
            db.Users.Add(NewUser("utc@ticketflow.demo"));
            await db.SaveChangesAsync();
        }

        await using var readDb = _database.CreateContext();
        var user = await readDb.Users.SingleAsync();
        user.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task User_with_tickets_cannot_be_deleted()
    {
        await using var db = _database.CreateContext();
        var requester = NewUser("requester@ticketflow.demo");
        db.Tickets.Add(new Ticket
        {
            Title = "Test",
            Description = "Test",
            Category = new Category { Name = "Test" },
            Branch = new Branch { Name = "Test", City = "Test" },
            Requester = requester,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await using var deleteDb = _database.CreateContext();
        var delete = () => deleteDb.Users.Where(u => u.Id == requester.Id).ExecuteDeleteAsync();

        await delete.Should().ThrowAsync<Exception>("FKs from Tickets to Users are Restrict");
    }
}
