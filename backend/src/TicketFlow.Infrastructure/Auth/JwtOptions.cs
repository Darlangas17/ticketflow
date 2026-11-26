using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TicketFlow.Infrastructure.Auth;

/// <summary>JWT settings, bound from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 secret. Must come from user secrets or an environment variable, never from the repo.</summary>
    [Required(ErrorMessage = "Jwt:SigningKey is not configured. Set it with 'dotnet user-secrets' or the Jwt__SigningKey environment variable (see README).")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters (256 bits) long.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Local demo only (docker compose): when no SigningKey is configured, generate a random one at
    /// startup. Tokens then stop working whenever the API restarts. Never enable it in production.
    /// </summary>
    public bool GenerateKeyIfMissing { get; init; }

    [Range(1, 24 * 60)]
    public int ExpirationMinutes { get; init; } = 60;

    public SymmetricSecurityKey GetSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    /// <summary>Rules used by the API to accept a token. Kept here so issuing and validating stay in sync.</summary>
    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = GetSecurityKey(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtClaimNames.Name,
        RoleClaimType = JwtClaimNames.Role,
    };
}

/// <summary>Claim names written into the token (short JWT names, no inbound mapping).</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
