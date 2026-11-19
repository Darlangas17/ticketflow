using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence;

public class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketFlowDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored in UTC. SQL Server's datetime2 has no time zone,
        // so values come back as DateTimeKind.Unspecified; this converter marks them as UTC
        // (it also applies to nullable DateTime properties).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDatabase => toDatabase.Kind == DateTimeKind.Local ? toDatabase.ToUniversalTime() : toDatabase,
        fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
}
