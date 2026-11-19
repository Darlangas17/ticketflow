using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(int Id, string FullName, string Email, UserRole Role)
{
    public static UserDto FromEntity(User user) => new(user.Id, user.FullName, user.Email, user.Role);
}

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
