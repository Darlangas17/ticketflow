using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Users;

/// <summary>User as seen by an administrator.</summary>
public sealed record UserAccountDto(
    int Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);

/// <param name="Password">Optional: sets a new password when present.</param>
public sealed record UpdateUserRequest(
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    string? Password = null);

/// <summary>Query-string parameters of GET /api/users. Every filter is optional.</summary>
public sealed record UserListQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
    public UserRole? Role { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Text to find in the name or e-mail.</summary>
    public string? Search { get; init; }
}
