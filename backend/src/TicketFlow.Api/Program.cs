using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using TicketFlow.Api.Auth;
using TicketFlow.Api.ErrorHandling;
using TicketFlow.Api.Security;
using TicketFlow.Api.Swagger;
using TicketFlow.Application;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Auth;
using TicketFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureTicketFlowKestrel();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ProblemDetailsResult.ForInvalidModelState);

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
builder.Services.AddTicketFlowSecurity(builder.Configuration);
builder.Services.AddTicketFlowSwagger();

// GET /health: "Healthy" when the API is up and the database answers (used by Docker and monitoring).
builder.Services.AddHealthChecks().AddDbContextCheck<TicketFlowDbContext>();

var app = builder.Build();

// First, so every response (errors included) gets the security headers and the real client IP is known.
app.UseTicketFlowSecurity();
app.UseExceptionHandler();
app.UseStatusCodePages(); // ProblemDetails body for empty 401/403/404 responses

// Swagger is on in Development, or where Swagger:Enabled is true (the docker compose demo).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseRateLimiter(); // after authentication, so limits can be per user
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Migrations + demo seed run automatically in Development, or anywhere the
// Database:MigrateAndSeedOnStartup setting is true (used later by Docker).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateAndSeedOnStartup"))
{
    await app.Services.InitializeDatabaseAsync();
}

app.Run();

// Makes the implicit Program class visible to WebApplicationFactory in the integration tests.
public partial class Program;
