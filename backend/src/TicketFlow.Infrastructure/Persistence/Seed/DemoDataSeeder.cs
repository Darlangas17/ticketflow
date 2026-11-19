using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Infrastructure.Persistence.Seed;

/// <summary>
/// Inserts fictional demo data: 3 branches, 5 categories, 6 users (1 admin,
/// 2 technicians, 3 requesters) and 30 tickets with comments and history.
/// Runs only when the database has no users, so it is safe to call on every startup.
/// All names and e-mails are made up; the .demo domain cannot receive mail.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Password shared by every demo account. Documented in the README.</summary>
    public const string DemoPassword = "Demo123!";

    public const int TicketCount = 30;

    // Fixed seed: every run produces the same tickets (only the dates move with "now").
    private const int RandomSeed = 20241;

    public static async Task<bool> SeedAsync(
        TicketFlowDbContext db,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return false;
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var random = new Random(RandomSeed);

        var branches = CreateBranches();
        var categories = CreateCategories();
        var users = CreateUsers(now);
        var tickets = CreateTickets(random, now, branches, categories, users);

        db.Branches.AddRange(branches);
        db.Categories.AddRange(categories);
        db.Users.AddRange(users.All);
        db.Tickets.AddRange(tickets);

        // A single SaveChanges runs inside one transaction: all or nothing.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static List<Branch> CreateBranches() =>
    [
        new() { Name = "Tegucigalpa Central", City = "Tegucigalpa" },
        new() { Name = "San Pedro Sula North", City = "San Pedro Sula" },
        new() { Name = "La Ceiba Coast", City = "La Ceiba" },
    ];

    private static List<Category> CreateCategories() =>
    [
        new() { Name = "Network", Description = "Internet, Wi-Fi, VPN and shared drive connectivity" },
        new() { Name = "Hardware", Description = "Computers, monitors, peripherals and POS terminals" },
        new() { Name = "Software", Description = "Business applications, e-mail and installations" },
        new() { Name = "Accounts & Access", Description = "Passwords, locked accounts and permissions" },
        new() { Name = "Printers", Description = "Printers, scanners and multifunction devices" },
    ];

    private sealed record DemoUsers(User Admin, List<User> Technicians, List<User> Requesters)
    {
        public IEnumerable<User> All => Technicians.Concat(Requesters).Prepend(Admin);
    }

    private static DemoUsers CreateUsers(DateTime now)
    {
        // Same password for every demo account, so hash once (BCrypt is slow on purpose).
        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        var createdAt = now.AddDays(-90);

        User NewUser(string name, string email, UserRole role) => new()
        {
            FullName = name,
            Email = email,
            PasswordHash = hash,
            Role = role,
            CreatedAt = createdAt,
        };

        return new DemoUsers(
            Admin: NewUser("Laura Mejia", "admin@ticketflow.demo", UserRole.Admin),
            Technicians:
            [
                NewUser("Carlos Rivera", "carlos.rivera@ticketflow.demo", UserRole.Technician),
                NewUser("Ana Torres", "ana.torres@ticketflow.demo", UserRole.Technician),
            ],
            Requesters:
            [
                NewUser("Jorge Castillo", "jorge.castillo@ticketflow.demo", UserRole.Requester),
                NewUser("Maria Lopez", "maria.lopez@ticketflow.demo", UserRole.Requester),
                NewUser("Pedro Sanchez", "pedro.sanchez@ticketflow.demo", UserRole.Requester),
            ]);
    }

    private sealed record TicketTemplate(string Category, string Title, string Description);

    private static readonly TicketTemplate[] Templates =
    [
        new("Network", "VPN disconnects every few minutes", "The VPN connection drops every 5-10 minutes while working from home, so I lose access to the ERP."),
        new("Network", "No internet on second floor", "None of the computers on the second floor can browse the internet since this morning. Phones on Wi-Fi work."),
        new("Network", "Slow Wi-Fi in meeting room", "Video calls freeze in the main meeting room. Speed tests show less than 2 Mbps."),
        new("Network", "Cannot reach shared drive", "The S: drive shows as disconnected and asks for credentials that are not accepted."),
        new("Hardware", "Laptop does not power on", "The laptop shows no lights when connected to the charger. It worked fine yesterday."),
        new("Hardware", "Monitor flickering", "The external monitor flickers and goes black for a few seconds several times per hour."),
        new("Hardware", "Keyboard keys not responding", "Several keys (E, R, T) do not respond on the desktop keyboard at the front desk."),
        new("Hardware", "POS terminal frozen", "The point-of-sale terminal at register 2 freezes when printing the receipt."),
        new("Software", "ERP crashes when exporting report", "The ERP closes without an error message when exporting the monthly sales report to Excel."),
        new("Software", "Outlook not syncing", "Outlook has not received new e-mails since yesterday afternoon. Webmail shows them correctly."),
        new("Software", "Install PDF editor", "I need a PDF editor installed to sign and edit supplier contracts."),
        new("Software", "Antivirus update failed", "The antivirus shows a red warning: definitions are 14 days old and the update fails."),
        new("Accounts & Access", "Account for new employee", "A new cashier starts on Monday and needs a domain account and access to the POS system."),
        new("Accounts & Access", "Locked out of e-mail account", "My e-mail account was locked after several failed attempts. I need it unlocked."),
        new("Accounts & Access", "Access to accounting folder", "I need read access to the Accounting/2024 shared folder to prepare the audit."),
        new("Printers", "Printer paper jam", "The main office printer reports a paper jam but there is no paper stuck inside."),
        new("Printers", "Printer shows offline", "The warehouse printer appears as offline on every computer, although it is turned on."),
        new("Printers", "Scanner not sending to e-mail", "The multifunction scanner shows 'SMTP error' when sending scans to e-mail."),
    ];

    // Fixed mix so the dashboard always shows every status.
    private static readonly TicketStatus[] StatusMix =
    [
        .. Enumerable.Repeat(TicketStatus.Open, 7),
        .. Enumerable.Repeat(TicketStatus.InProgress, 7),
        .. Enumerable.Repeat(TicketStatus.OnHold, 4),
        .. Enumerable.Repeat(TicketStatus.Resolved, 6),
        .. Enumerable.Repeat(TicketStatus.Closed, 6),
    ];

    private static readonly string[] TechnicianComments =
    [
        "I'm looking into this now.",
        "Could you confirm whether this happens on other computers too?",
        "I checked remotely and found the cause. Applying a fix.",
        "Waiting for a replacement part from the supplier.",
    ];

    private static readonly string[] RequesterComments =
    [
        "Thanks, it happens on other computers as well.",
        "It's still happening, please check again.",
        "Confirmed, it's working now. Thank you!",
    ];

    private static List<Ticket> CreateTickets(
        Random random,
        DateTime now,
        List<Branch> branches,
        List<Category> categories,
        DemoUsers users)
    {
        var statuses = StatusMix.ToArray();
        random.Shuffle(statuses);

        var tickets = new List<Ticket>(TicketCount);
        for (var i = 0; i < TicketCount; i++)
        {
            var template = Templates[i % Templates.Length];
            var requester = users.Requesters[random.Next(users.Requesters.Count)];
            var createdAt = now
                .AddDays(-random.Next(4, 60))
                .AddHours(-random.Next(0, 24))
                .AddMinutes(-random.Next(0, 60));

            var ticket = new Ticket
            {
                Title = template.Title,
                Description = template.Description,
                Status = TicketStatus.Open,
                Priority = PickPriority(random),
                Category = categories.Single(c => c.Name == template.Category),
                Branch = branches[random.Next(branches.Count)],
                Requester = requester,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };

            ApplyLifecycle(ticket, statuses[i], random, users, i);
            tickets.Add(ticket);
        }

        return tickets;
    }

    private static TicketPriority PickPriority(Random random) => random.Next(100) switch
    {
        < 25 => TicketPriority.Low,
        < 65 => TicketPriority.Medium,
        < 90 => TicketPriority.High,
        _ => TicketPriority.Critical,
    };

    /// <summary>
    /// Walks the ticket through the valid state machine up to <paramref name="target"/>,
    /// writing a history entry (and some comments) for each step so the data is consistent.
    /// </summary>
    private static void ApplyLifecycle(Ticket ticket, TicketStatus target, Random random, DemoUsers users, int index)
    {
        // Some open tickets are still unassigned, the rest are assigned to a technician.
        var leaveUnassigned = target == TicketStatus.Open && index % 2 == 0;
        if (leaveUnassigned)
        {
            return;
        }

        var technician = users.Technicians[random.Next(users.Technicians.Count)];
        var clock = ticket.CreatedAt;

        DateTime Step() => clock = clock.AddHours(random.Next(1, 18)).AddMinutes(random.Next(0, 60));

        ticket.Assignee = technician;
        ticket.History.Add(History("Assignee", null, technician.FullName, users.Admin, Step()));

        if (target == TicketStatus.Open)
        {
            ticket.UpdatedAt = clock;
            return;
        }

        ChangeStatus(TicketStatus.InProgress, technician, Step());
        ticket.Comments.Add(Comment(technician, TechnicianComments[random.Next(3)], Step()));

        if (target == TicketStatus.OnHold)
        {
            ChangeStatus(TicketStatus.OnHold, technician, Step());
            ticket.Comments.Add(Comment(technician, TechnicianComments[3], clock));
        }

        if (target is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ChangeStatus(TicketStatus.Resolved, technician, Step());
            ticket.ResolvedAt = clock;
        }

        if (target == TicketStatus.Closed)
        {
            ticket.Comments.Add(Comment(ticket.Requester, RequesterComments[2], Step()));
            ChangeStatus(TicketStatus.Closed, ticket.Requester, clock);
        }
        else if (target == TicketStatus.InProgress && random.Next(2) == 0)
        {
            ticket.Comments.Add(Comment(ticket.Requester, RequesterComments[random.Next(2)], Step()));
        }

        ticket.UpdatedAt = clock;

        void ChangeStatus(TicketStatus newStatus, User changedBy, DateTime at)
        {
            ticket.History.Add(History("Status", ticket.Status.ToString(), newStatus.ToString(), changedBy, at));
            ticket.Status = newStatus;
        }
    }

    private static TicketHistory History(string field, string? oldValue, string? newValue, User changedBy, DateTime at) =>
        new() { Field = field, OldValue = oldValue, NewValue = newValue, ChangedBy = changedBy, ChangedAt = at };

    private static TicketComment Comment(User author, string body, DateTime at) =>
        new() { Author = author, Body = body, CreatedAt = at };
}
