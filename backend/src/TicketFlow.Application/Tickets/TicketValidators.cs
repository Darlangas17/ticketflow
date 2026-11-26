using FluentValidation;

namespace TicketFlow.Application.Tickets;

// Title and comment lengths match the database columns (see the EF Core configurations).
// Description is nvarchar(max) in the database; the API caps it to keep requests reasonable.
internal static class TicketLimits
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 4000;
    public const int CommentMaxLength = 2000;
    public const int SearchMaxLength = 100;
}

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(TicketLimits.TitleMaxLength);
        RuleFor(r => r.Description).NotEmpty().MaximumLength(TicketLimits.DescriptionMaxLength);
        RuleFor(r => r.Priority).IsInEnum();
        RuleFor(r => r.CategoryId).GreaterThan(0);
        RuleFor(r => r.BranchId).GreaterThan(0);
    }
}

public sealed class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(TicketLimits.TitleMaxLength);
        RuleFor(r => r.Description).NotEmpty().MaximumLength(TicketLimits.DescriptionMaxLength);
        RuleFor(r => r.Priority).IsInEnum();
        RuleFor(r => r.CategoryId).GreaterThan(0);
    }
}

public sealed class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
    }
}

public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(r => r.AssigneeId).GreaterThan(0).When(r => r.AssigneeId is not null);
    }
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(r => r.Body).NotEmpty().MaximumLength(TicketLimits.CommentMaxLength);
    }
}

public sealed class TicketListQueryValidator : AbstractValidator<TicketListQuery>
{
    public TicketListQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, TicketListQuery.MaxPageSize);
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.Priority).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(TicketLimits.SearchMaxLength);
        RuleFor(q => q.AssigneeId)
            .Null()
            .When(q => q.Unassigned == true)
            .WithMessage("'Assignee Id' cannot be combined with 'Unassigned'.");
        RuleFor(q => q.Sort)
            .Must(TicketSort.IsValid)
            .WithMessage($"'Sort' must be one of: {string.Join(", ", TicketSort.Allowed)}.");
    }
}
