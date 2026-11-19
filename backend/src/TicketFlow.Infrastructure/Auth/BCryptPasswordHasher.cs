using TicketFlow.Application.Abstractions;

namespace TicketFlow.Infrastructure.Auth;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // 2^11 rounds: about 100-250 ms per hash on current hardware. Slow on purpose to resist brute force.
    private const int WorkFactor = 11;

    // Hash used when the user does not exist, so Verify takes the same time either way.
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword("timing-attack-dummy", WorkFactor));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? passwordHash)
    {
        if (passwordHash is null)
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash.Value);
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Stored value is not a valid BCrypt hash: treat as a failed login, not a server error.
            return false;
        }
    }
}
