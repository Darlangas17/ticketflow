using TicketFlow.Domain.Enums;

namespace TicketFlow.Api.Auth;

/// <summary>
/// Role-based policies. Use them with [Authorize(Policy = AuthorizationPolicies.AdminOnly)].
/// Every endpoint requires an authenticated user by default (fallback policy);
/// public endpoints opt out with [AllowAnonymous].
/// </summary>
public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string StaffOnly = nameof(StaffOnly);

    public static IServiceCollection AddTicketFlowAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(StaffOnly, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Technician)));

        return services;
    }
}
