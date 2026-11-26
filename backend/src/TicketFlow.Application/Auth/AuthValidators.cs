using System.Text;
using FluentValidation;

namespace TicketFlow.Application.Auth;

/// <summary>Account rules shared by self-registration and user administration.</summary>
public static class AccountRules
{
    // BCrypt only uses the first 72 bytes of the password; longer ones would be silently truncated.
    private const int MaxPasswordBytes = 72;

    /// <summary>E-mails are stored trimmed and lower-case, so lookups and uniqueness ignore case.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(120);

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(160).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(8)
            .Must(p => Encoding.UTF8.GetByteCount(p) <= MaxPasswordBytes)
                .WithMessage($"'Password' must be at most {MaxPasswordBytes} bytes long.")
            .Matches("[A-Za-z]").WithMessage("'Password' must contain at least one letter.")
            .Matches("[0-9]").WithMessage("'Password' must contain at least one digit.")
            .Must(p => !CommonPasswords.Contains(p))
                .WithMessage("'Password' is too common. Choose a less predictable one.");

    /// <summary>
    /// Passwords that meet the length/letter/digit rules but appear at the top of every leaked
    /// password list. NIST SP 800-63B asks to reject commonly used passwords; this short list covers
    /// the most frequent ones (compared case-insensitively).
    /// </summary>
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1", "password12", "password123", "password1234", "passw0rd", "p4ssw0rd",
        "12345678a", "123456789a", "a12345678", "abc12345", "abcd1234", "abc123456",
        "qwerty12", "qwerty123", "qwerty1234", "1q2w3e4r", "1q2w3e4r5t", "zaq12wsx",
        "iloveyou1", "welcome1", "welcome123", "letmein1", "admin123", "admin1234",
        "changeme1", "test1234", "trustno1", "football1", "baseball1", "superman1",
        "monkey123", "dragon123", "sunshine1", "princess1", "master123", "michael1",
        "contraseña1", "contrasena1", "contraseña123", "contrasena123",
    };
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).ValidPassword();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Upper bounds too: oversized values are rejected before they reach the database or BCrypt.
        RuleFor(r => r.Email).NotEmpty().MaximumLength(160);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(256);
    }
}
