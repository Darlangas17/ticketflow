using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using TicketFlow.Api.Auth;
using TicketFlow.Application.Auth;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Auth;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Tests.Api;

public class AuthEndpointsTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    private const string AdminEmail = "admin@ticketflow.demo";
    private const string RequesterEmail = "maria.lopez@ticketflow.demo";

    // Same settings as the API: enums travel as strings.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);

    private async Task<AuthResponse> LoginOkAsync(string email)
    {
        var response = await LoginAsync(email, DemoDataSeeder.DemoPassword);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    private async Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token_that_works_on_me()
    {
        var auth = await LoginOkAsync(AdminEmail);

        auth.User.Email.Should().Be(AdminEmail);
        auth.User.Role.Should().Be(UserRole.Admin);

        var me = await GetMeAsync(auth.AccessToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await me.Content.ReadFromJsonAsync<UserDto>(Json);
        user.Should().Be(auth.User);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_problem_details()
    {
        var response = await LoginAsync(AdminEmail, "WrongPassword1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        problem!.Detail.Should().Be(AuthService.InvalidCredentialsMessage);
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task Validation_messages_are_in_english_regardless_of_server_culture()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("Name", "not-an-email", "Password123"), Json);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        problem!.Errors["Email"].Should().Equal("'Email' is not a valid email address.");
    }

    [Fact]
    public async Task Login_of_inactive_user_returns_403()
    {
        var response = await LoginAsync(TicketFlowApiFactory.InactiveUserEmail, DemoDataSeeder.DemoPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        problem!.Detail.Should().Be(AuthService.DisabledAccountMessage);
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400_with_field_errors()
    {
        var response = await LoginAsync("", "");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        problem!.Errors.Keys.Should().Contain(["Email", "Password"]);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        var response = await GetMeAsync(token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_an_invalid_token_returns_401()
    {
        var response = await GetMeAsync("not.a.valid.token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_creates_a_requester_who_can_log_in()
    {
        var request = new RegisterRequest("Nuevo Usuario", "nuevo.usuario@ticketflow.demo", "Password123");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request, Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().Be("/api/auth/me");
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        auth!.User.Role.Should().Be(UserRole.Requester);

        var login = await LoginAsync(request.Email, request.Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_cannot_choose_a_role()
    {
        // Extra "role" field in the body is ignored: self-registration is always Requester.
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky User",
            email = "sneaky@ticketflow.demo",
            password = "Password123",
            role = "Admin",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        auth!.User.Role.Should().Be(UserRole.Requester);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("Copy", AdminEmail.ToUpperInvariant(), "Password123"), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(AdminEmail, true, true)]
    [InlineData("carlos.rivera@ticketflow.demo", false, true)]
    [InlineData(RequesterEmail, false, false)]
    public async Task Role_policies_follow_the_role_in_the_token(string email, bool isAdmin, bool isStaff)
    {
        var auth = await LoginOkAsync(email);
        var principal = await ValidateAsync(auth.AccessToken);
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(principal, AuthorizationPolicies.AdminOnly)).Succeeded.Should().Be(isAdmin);
        (await authorization.AuthorizeAsync(principal, AuthorizationPolicies.StaffOnly)).Succeeded.Should().Be(isStaff);
    }

    private async Task<ClaimsPrincipal> ValidateAsync(string token)
    {
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, jwt.CreateValidationParameters());
        result.IsValid.Should().BeTrue();
        return new ClaimsPrincipal(result.ClaimsIdentity);
    }
}
