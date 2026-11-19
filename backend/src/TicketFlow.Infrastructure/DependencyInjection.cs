using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        // The connection string may be null at design time (dotnet ef migrations add);
        // it is validated when the database is actually used.
        services.AddDbContext<TicketFlowDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        return services;
    }

    /// <summary>Applies pending migrations and inserts demo data if the database is empty.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DependencyInjection));

        if (string.IsNullOrWhiteSpace(db.Database.GetConnectionString()))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:Default' is not configured. " +
                "Set it with 'dotnet user-secrets' or the ConnectionStrings__Default environment variable (see README).");
        }

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        var seeded = await DemoDataSeeder.SeedAsync(db, cancellationToken: cancellationToken);
        logger.LogInformation(seeded ? "Demo data inserted." : "Database already has data; seed skipped.");
    }
}
