using System.Text;
using FluentValidation;

namespace TicketFlow.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    // BCrypt only uses the first 72 bytes of the password; longer ones would be silently truncated.
    private const int MaxPasswordBytes = 72;

    public RegisterRequestValidator()
    {
        RuleFor(r => r.FullName)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(r => r.Email)
            .NotEmpty()
            .MaximumLength(160)
            .EmailAddress();

        RuleFor(r => r.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Must(p => Encoding.UTF8.GetByteCount(p) <= MaxPasswordBytes)
                .WithMessage($"'Password' must be at most {MaxPasswordBytes} bytes long.")
            .Matches("[A-Za-z]").WithMessage("'Password' must contain at least one letter.")
            .Matches("[0-9]").WithMessage("'Password' must contain at least one digit.");
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
