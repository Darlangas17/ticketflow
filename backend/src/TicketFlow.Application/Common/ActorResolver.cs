using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Application.Tickets;

namespace TicketFlow.Application.Common;

internal static class ActorResolver
{
    /// <summary>
    /// Loads the caller from the database (not only from the token), so a deactivation or a
    /// role change applies immediately to tokens that were issued before it.
    /// </summary>
    public static async Task<Actor> GetActorAsync(this IUserRepository users, int userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("The user no longer exists or is disabled.");
        }

        return new Actor(user.Id, user.FullName, user.Role);
    }
}
