using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Tests.Auth;

/// <summary>JWT settings for tests only. The key is not used anywhere else.</summary>
public static class TestJwt
{
    public const string SigningKey = "test-signing-key-for-unit-tests-only-0123456789";

    public static JwtOptions Options { get; } = new()
    {
        Issuer = "TicketFlow.Tests",
        Audience = "TicketFlow.Tests.Client",
        SigningKey = SigningKey,
        ExpirationMinutes = 15,
    };
}
