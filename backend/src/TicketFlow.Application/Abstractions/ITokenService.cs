using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
