using TicketFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default"));

var app = builder.Build();

// Migrations + demo seed run automatically in Development, or anywhere the
// Database:MigrateAndSeedOnStartup setting is true (used later by Docker).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.InitializeDatabaseAsync();
}

app.Run();
