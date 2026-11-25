using FluentValidation;
using TicketFlow.Application.Auth;

namespace TicketFlow.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).ValidPassword();
        RuleFor(r => r.Role).IsInEnum();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Role).IsInEnum();
        RuleFor(r => r.Password!).ValidPassword().When(r => r.Password is not null);
    }
}

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, UserListQuery.MaxPageSize);
        RuleFor(q => q.Role).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(100);
    }
}
