using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TicketFlow.Application.Abstractions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Seed;
using TicketFlow.Tests.Auth;

namespace TicketFlow.Tests.Api;

/// <summary>
/// Runs the real API in memory, replacing SQL Server with an in-memory SQLite database
/// that holds the demo seed plus one inactive user.
/// </summary>
public sealed class TicketFlowApiFactory : WebApplicationFactory<Program>
{
    public const string InactiveUserEmail = "inactive@ticketflow.demo";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" is not Development, so the API does not try to migrate SQL Server on startup.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", TestJwt.SigningKey);
        // Integration tests sign in many times per minute; SecurityTests checks the real limits.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<TicketFlowDbContext>>();
            _connection.Open();
            services.AddDbContext<TicketFlowDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Database.EnsureCreated();
        DemoDataSeeder.SeedAsync(db, hasher).GetAwaiter().GetResult();

        db.Users.Add(new User
        {
            FullName = "Inactive User",
            Email = InactiveUserEmail,
            PasswordHash = hasher.Hash(DemoDataSeeder.DemoPassword),
            Role = UserRole.Requester,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
