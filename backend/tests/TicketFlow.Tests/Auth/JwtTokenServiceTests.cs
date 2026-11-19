using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Tests.Auth;

public class JwtTokenServiceTests
{
    private readonly JsonWebTokenHandler _handler = new();

    private static User Admin => new()
    {
        Id = 7,
        FullName = "Laura Mejia",
        Email = "admin@ticketflow.demo",
        Role = UserRole.Admin,
    };

    private static string CreateToken(User user, JwtOptions? options = null) =>
        new JwtTokenService(Options.Create(options ?? TestJwt.Options), TimeProvider.System)
            .CreateAccessToken(user).Token;

    [Fact]
    public async Task Token_contains_the_user_identity_and_role()
    {
        var result = await _handler.ValidateTokenAsync(CreateToken(Admin), TestJwt.Options.CreateValidationParameters());

        result.IsValid.Should().BeTrue();
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        principal.FindFirstValue(JwtClaimNames.Subject).Should().Be("7");
        principal.FindFirstValue(JwtClaimNames.Email).Should().Be("admin@ticketflow.demo");
        principal.Identity!.Name.Should().Be("Laura Mejia");
        principal.IsInRole("Admin").Should().BeTrue("[Authorize(Roles = ...)] relies on the role claim");
        principal.IsInRole("Requester").Should().BeFalse();
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var forged = CreateToken(Admin, new JwtOptions
        {
            Issuer = TestJwt.Options.Issuer,
            Audience = TestJwt.Options.Audience,
            SigningKey = "a-completely-different-signing-key-0123456789",
        });

        var result = await _handler.ValidateTokenAsync(forged, TestJwt.Options.CreateValidationParameters());

        result.IsValid.Should().BeFalse();
        result.Exception.Should().BeAssignableTo<SecurityTokenInvalidSignatureException>();
    }

    [Fact]
    public async Task Token_with_a_modified_role_is_rejected()
    {
        var requester = Admin;
        requester.Role = UserRole.Requester;
        var parts = CreateToken(requester).Split('.');

        // Rewrite the payload to claim the Admin role while keeping the original signature.
        var payload = Base64UrlEncoder.Decode(parts[1]).Replace("\"Requester\"", "\"Admin\"");
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        var result = await _handler.ValidateTokenAsync(tampered, TestJwt.Options.CreateValidationParameters());

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_for_another_audience_is_rejected()
    {
        var token = CreateToken(Admin);
        var parameters = TestJwt.Options.CreateValidationParameters();
        parameters.ValidAudience = "some-other-app";

        var result = await _handler.ValidateTokenAsync(token, parameters);

        result.IsValid.Should().BeFalse();
    }
}
