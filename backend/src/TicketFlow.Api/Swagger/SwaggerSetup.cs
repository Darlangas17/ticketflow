using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TicketFlow.Api.Swagger;

public static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddTicketFlowSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TicketFlow API",
                Version = "v1",
                Description = "IT support ticket system. Log in with POST /api/auth/login, " +
                              "then click **Authorize** and paste the accessToken.",
            });

            // Enables the "Authorize" button: Swagger UI sends "Authorization: Bearer <token>".
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken returned by /api/auth/login (without the 'Bearer ' prefix).",
            });
            options.OperationFilter<AuthorizeOperationFilter>();

            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile);
            }
        });

        return services;
    }

    /// <summary>
    /// Marks every endpoint without [AllowAnonymous] as requiring the bearer token
    /// (padlock icon in Swagger UI) and documents the 401 response.
    /// </summary>
    private sealed class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowsAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
                .OfType<IAllowAnonymous>()
                .Any();
            if (allowsAnonymous)
            {
                return;
            }

            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = BearerScheme },
                }] = Array.Empty<string>(),
            });
        }
    }
}
