using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Auth;
using TicketFlow.Infrastructure.Persistence.Seed;

namespace TicketFlow.Tests.Api;

/// <summary>Security controls of the HTTP pipeline (OWASP ASVS V4, V8, V14).</summary>
public class SecurityTests(TicketFlowApiFactory factory) : IClassFixture<TicketFlowApiFactory>
{
    [Fact]
    public async Task Api_responses_carry_security_headers_and_are_not_cached()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Request_bodies_over_one_megabyte_are_rejected()
    {
        var huge = new string('x', 1024 * 1024 + 10);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(huge, "x"));

        // Kestrel's limit returns 413; TestServer does not enforce it, so the model limits answer 400.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Repeated_sign_in_attempts_are_rate_limited()
    {
        using var baseFactory = new TicketFlowApiFactory();
        using var limited = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3"));
        var client = limited.CreateClient();
        var wrong = new LoginRequest("admin@ticketflow.demo", "WrongPassword9");

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/login", wrong)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", wrong);

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        blocked.Headers.Contains("Retry-After").Should().BeTrue();
        blocked.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await blocked.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("Too many attempts");

        // Even the right password is refused until the window passes.
        var right = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin@ticketflow.demo", DemoDataSeeder.DemoPassword));
        right.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Rate_limit_does_not_apply_to_normal_endpoints_at_the_auth_threshold()
    {
        using var baseFactory = new TicketFlowApiFactory();
        using var limited = baseFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:AuthPermitLimit", "1"));
        var client = limited.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
