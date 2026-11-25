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
            .Matches("[0-9]").WithMessage("'Password' must contain at least one digit.");
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
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}
