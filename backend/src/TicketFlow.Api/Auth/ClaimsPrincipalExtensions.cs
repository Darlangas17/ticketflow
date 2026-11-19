using System.Security.Claims;
using TicketFlow.Application.Common.Exceptions;
using TicketFlow.Infrastructure.Auth;

namespace TicketFlow.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id of the authenticated user, taken from the token's "sub" claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtClaimNames.Subject);
        return int.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedException("The access token does not identify a user.");
    }
}
