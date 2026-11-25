using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Tickets;

namespace TicketFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Always English messages: by default FluentValidation follows the server's OS culture.
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITicketService, TicketService>();
        return services;
    }
}
