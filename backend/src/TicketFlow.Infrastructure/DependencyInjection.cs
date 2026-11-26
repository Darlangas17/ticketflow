using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions;
using TicketFlow.Infrastructure.Auth;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Seed;
using TicketFlow.Infrastructure.Repositories;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string may be missing at design time (dotnet ef migrations add);
        // it is validated when the database is actually used.
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<TicketFlowDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Fails at startup (not on the first login) if the JWT settings are missing or too weak.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure<ILoggerFactory>((jwt, loggers) =>
            {
                if (string.IsNullOrWhiteSpace(jwt.SigningKey) && jwt.GenerateKeyIfMissing)
                {
                    // 512 random bits from a CSPRNG; kept in memory only.
                    jwt.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                    loggers.CreateLogger<JwtOptions>().LogWarning(
                        "Jwt:SigningKey is not configured; using a random key generated at startup (local demo mode). " +
                        "Set Jwt__SigningKey for any real deployment.");
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        return services;
    }

    /// <summary>Applies pending migrations and inserts demo data if the database is empty.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DependencyInjection));

        if (string.IsNullOrWhiteSpace(db.Database.GetConnectionString()))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:Default' is not configured. " +
                "Set it with 'dotnet user-secrets' or the ConnectionStrings__Default environment variable (see README).");
        }

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);

        var seeded = await DemoDataSeeder.SeedAsync(db, passwordHasher, cancellationToken: cancellationToken);
        logger.LogInformation(seeded ? "Demo data inserted." : "Database already has data; seed skipped.");
    }
}
