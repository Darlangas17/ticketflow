using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using TicketFlow.Api.Auth;
using TicketFlow.Api.ErrorHandling;
using TicketFlow.Api.Swagger;
using TicketFlow.Application;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured through options so it reads the validated JwtOptions lazily (works with test overrides too).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false; // keep "sub", "role"... as written in the token
        bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters();
    });

builder.Services.AddTicketFlowAuthorization();
builder.Services.AddTicketFlowSwagger();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // ProblemDetails body for empty 401/403/404 responses

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Migrations + demo seed run automatically in Development, or anywhere the
// Database:MigrateAndSeedOnStartup setting is true (used later by Docker).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.InitializeDatabaseAsync();
}

app.Run();

// Makes the implicit Program class visible to WebApplicationFactory in the integration tests.
public partial class Program;
