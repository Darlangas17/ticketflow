using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Application.Abstractions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaimNames.Subject, user.Id.ToString()),
                new Claim(JwtClaimNames.Email, user.Email),
                new Claim(JwtClaimNames.Name, user.FullName),
                new Claim(JwtClaimNames.Role, user.Role.ToString()),
                new Claim(JwtClaimNames.TokenId, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(_options.GetSecurityKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
