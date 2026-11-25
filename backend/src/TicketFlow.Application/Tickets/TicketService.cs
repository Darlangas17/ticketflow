using FluentValidation;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Rules;

namespace TicketFlow.Application.Tickets;

public interface ITicketService
{
    Task<PagedResult<TicketListItemDto>> ListAsync(int userId, TicketListQuery query, CancellationToken cancellationToken = default);

    Task<TicketDetailsDto> GetAsync(int userId, int ticketId, CancellationToken cancellationToken = default);

    Task<TicketDetailsDto> CreateAsync(int userId, CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailsDto> UpdateAsync(int userId, int ticketId, UpdateTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailsDto> ChangeStatusAsync(int userId, int ticketId, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailsDto> AssignAsync(int userId, int ticketId, AssignTicketRequest request, CancellationToken cancellationToken = default);

    Task<TicketCommentDto> AddCommentAsync(int userId, int ticketId, AddCommentRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int userId, int ticketId, CancellationToken cancellationToken = default);
}

public sealed class TicketService(
    ITicketRepository tickets,
    IUserRepository users,
    IValidator<TicketListQuery> listValidator,
    IValidator<CreateTicketRequest> createValidator,
    IValidator<UpdateTicketRequest> updateValidator,
    IValidator<ChangeTicketStatusRequest> statusValidator,
    IValidator<AssignTicketRequest> assignValidator,
    IValidator<AddCommentRequest> commentValidator,
    TimeProvider timeProvider) : ITicketService
{
    // Names stored in TicketHistory.Field.
    public const string StatusField = "Status";
    public const string PriorityField = "Priority";
    public const string AssigneeField = "Assignee";

    public async Task<PagedResult<TicketListItemDto>> ListAsync(int userId, TicketListQuery query, CancellationToken cancellationToken = default)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);
        return await tickets.ListAsync(query, TicketAccess.VisibleTo(actor), cancellationToken);
    }

    public async Task<TicketDetailsDto> GetAsync(int userId, int ticketId, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(userId, cancellationToken);
        return await GetDetailsAsync(actor, ticketId, cancellationToken);
    }

    public async Task<TicketDetailsDto> CreateAsync(int userId, CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        if (!await tickets.ActiveBranchExistsAsync(request.BranchId, cancellationToken))
        {
            throw new BusinessRuleException($"Branch {request.BranchId} does not exist or is inactive.");
        }

        var now = UtcNow();
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = request.Priority ?? TicketPriority.Medium,
            CategoryId = request.CategoryId,
            BranchId = request.BranchId,
            RequesterId = actor.Id, // tickets are always created on behalf of the caller
            CreatedAt = now,
            UpdatedAt = now,
        };

        tickets.Add(ticket);
        await tickets.SaveChangesAsync(cancellationToken);

        return await GetDetailsAsync(actor, ticket.Id, cancellationToken);
    }

    public async Task<TicketDetailsDto> UpdateAsync(int userId, int ticketId, UpdateTicketRequest request, CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);
        var ticket = await GetVisibleForUpdateAsync(actor, ticketId, cancellationToken);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("Closed tickets cannot be edited.");
        }

        if (ticket.CategoryId != request.CategoryId)
        {
            await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        }

        var now = UtcNow();
        if (ticket.Priority != request.Priority)
        {
            AddHistory(ticket, actor, PriorityField, ticket.Priority.ToString(), request.Priority.ToString(), now);
            ticket.Priority = request.Priority;
        }

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.CategoryId = request.CategoryId;
        ticket.UpdatedAt = now;

        await tickets.SaveChangesAsync(cancellationToken);
        return await GetDetailsAsync(actor, ticket.Id, cancellationToken);
    }

    public async Task<TicketDetailsDto> ChangeStatusAsync(int userId, int ticketId, ChangeTicketStatusRequest request, CancellationToken cancellationToken = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);
        var ticket = await GetVisibleForUpdateAsync(actor, ticketId, cancellationToken);

        if (!TicketStatusTransitions.CanTransition(ticket.Status, request.Status))
        {
            var allowed = TicketStatusTransitions.NextStatuses(ticket.Status);
            var hint = allowed.Count == 0
                ? $"{ticket.Status} is a final status."
                : $"Allowed: {string.Join(", ", allowed)}.";
            throw new BusinessRuleException($"Invalid status transition from {ticket.Status} to {request.Status}. {hint}");
        }

        if (!TicketAccess.CanChangeStatus(actor, ticket, request.Status))
        {
            throw new ForbiddenException("Requesters can only close their own tickets once they are resolved.");
        }

        var now = UtcNow();
        AddHistory(ticket, actor, StatusField, ticket.Status.ToString(), request.Status.ToString(), now);
        ticket.Status = request.Status;
        ticket.UpdatedAt = now;
        if (request.Status == TicketStatus.Resolved)
        {
            ticket.ResolvedAt = now;
        }

        await tickets.SaveChangesAsync(cancellationToken);
        return await GetDetailsAsync(actor, ticket.Id, cancellationToken);
    }

    public async Task<TicketDetailsDto> AssignAsync(int userId, int ticketId, AssignTicketRequest request, CancellationToken cancellationToken = default)
    {
        await assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);
        if (!TicketAccess.CanAssign(actor))
        {
            throw new ForbiddenException("Only technicians and admins can assign tickets.");
        }

        var ticket = await GetVisibleForUpdateAsync(actor, ticketId, cancellationToken);
        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("Closed tickets cannot be reassigned.");
        }

        if (ticket.AssigneeId == request.AssigneeId)
        {
            return await GetDetailsAsync(actor, ticket.Id, cancellationToken); // nothing changes, no history
        }

        User? newAssignee = null;
        if (request.AssigneeId is { } assigneeId)
        {
            newAssignee = await users.GetByIdAsync(assigneeId, cancellationToken);
            if (newAssignee is null || !newAssignee.IsActive || newAssignee.Role == UserRole.Requester)
            {
                throw new BusinessRuleException($"User {assigneeId} is not an active technician or admin.");
            }
        }

        var oldAssignee = ticket.AssigneeId is { } oldId ? await users.GetByIdAsync(oldId, cancellationToken) : null;

        var now = UtcNow();
        AddHistory(ticket, actor, AssigneeField, oldAssignee?.FullName, newAssignee?.FullName, now);
        ticket.AssigneeId = newAssignee?.Id;
        ticket.UpdatedAt = now;

        await tickets.SaveChangesAsync(cancellationToken);

        // A technician who hands the ticket to someone else stops seeing it; the response of the
        // action they just performed is still returned, so it is read without the visibility filter.
        var details = await tickets.GetDetailsAsync(ticket.Id, _ => true, cancellationToken);
        return WithNextStatuses(details!);
    }

    public async Task<TicketCommentDto> AddCommentAsync(int userId, int ticketId, AddCommentRequest request, CancellationToken cancellationToken = default)
    {
        await commentValidator.ValidateAndThrowAsync(request, cancellationToken);
        var actor = await GetActorAsync(userId, cancellationToken);
        var ticket = await GetVisibleForUpdateAsync(actor, ticketId, cancellationToken);

        if (ticket.Status == TicketStatus.Closed)
        {
            throw new BusinessRuleException("Closed tickets cannot receive new comments.");
        }

        var comment = new TicketComment
        {
            AuthorId = actor.Id,
            Body = request.Body.Trim(),
            CreatedAt = UtcNow(),
        };
        ticket.Comments.Add(comment);

        await tickets.SaveChangesAsync(cancellationToken);
        return new TicketCommentDto(comment.Id, new UserSummaryDto(actor.Id, actor.FullName), comment.Body, comment.CreatedAt);
    }

    public async Task DeleteAsync(int userId, int ticketId, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(userId, cancellationToken);
        if (!TicketAccess.CanDelete(actor))
        {
            throw new ForbiddenException("Only admins can delete tickets.");
        }

        var ticket = await tickets.GetForUpdateAsync(ticketId, cancellationToken) ?? throw TicketNotFound(ticketId);
        tickets.Remove(ticket); // comments and history are removed by cascade
        await tickets.SaveChangesAsync(cancellationToken);
    }

    private async Task<Actor> GetActorAsync(int userId, CancellationToken cancellationToken)
    {
        // Loaded from the database so a deactivation or role change applies to existing tokens too.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("The user no longer exists or is disabled.");
        }

        return new Actor(user.Id, user.FullName, user.Role);
    }

    private async Task<TicketDetailsDto> GetDetailsAsync(Actor actor, int ticketId, CancellationToken cancellationToken)
    {
        var details = await tickets.GetDetailsAsync(ticketId, TicketAccess.VisibleTo(actor), cancellationToken)
                      ?? throw TicketNotFound(ticketId);
        return WithNextStatuses(details);
    }

    /// <summary>
    /// Tickets the user is not allowed to see are reported as not found (404, not 403)
    /// so the API does not reveal which ticket numbers exist.
    /// </summary>
    private async Task<Ticket> GetVisibleForUpdateAsync(Actor actor, int ticketId, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetForUpdateAsync(ticketId, cancellationToken);
        if (ticket is null || !TicketAccess.CanView(actor, ticket))
        {
            throw TicketNotFound(ticketId);
        }

        return ticket;
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await tickets.CategoryExistsAsync(categoryId, cancellationToken))
        {
            throw new BusinessRuleException($"Category {categoryId} does not exist.");
        }
    }

    private static void AddHistory(Ticket ticket, Actor actor, string field, string? oldValue, string? newValue, DateTime at) =>
        ticket.History.Add(new TicketHistory
        {
            ChangedById = actor.Id,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = at,
        });

    private static TicketDetailsDto WithNextStatuses(TicketDetailsDto details) =>
        details with { AllowedNextStatuses = TicketStatusTransitions.NextStatuses(details.Status) };

    private static NotFoundException TicketNotFound(int ticketId) => new($"Ticket {ticketId} was not found.");

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
