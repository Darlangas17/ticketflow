using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Tests.Infrastructure;

/// <summary>
/// In-memory SQLite database with the real TicketFlow schema. Unlike the EF InMemory
/// provider, SQLite enforces foreign keys and unique indexes. The database lives as
/// long as the connection stays open.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Each call returns a fresh context (empty change tracker) over the same database.</summary>
    public TicketFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TicketFlowDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
