using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Catalogs;
using TicketFlow.Application.Dashboard;
using TicketFlow.Application.Tickets;
using TicketFlow.Application.Users;

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
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}
