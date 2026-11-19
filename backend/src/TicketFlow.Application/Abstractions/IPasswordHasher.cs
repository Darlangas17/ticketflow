namespace TicketFlow.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a password against a stored hash. When <paramref name="passwordHash"/> is null
    /// (unknown user) it still does the same amount of work and returns false, so response
    /// times do not reveal which e-mails are registered.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}
