using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Common.Exceptions;

namespace TicketFlow.Api.ErrorHandling;

/// <summary>
/// Turns exceptions into RFC 7807 ProblemDetails responses. Known application
/// exceptions map to 4xx; anything else is logged and returned as a generic 500
/// without internal details.
/// In .NET 8 the ExceptionHandlerMiddleware also logs every exception (even expected 4xx ones)
/// as an error with its stack trace; that category is turned off in appsettings.json, so
/// this class is the single place that logs real failures.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            BusinessRuleException => Problem(StatusCodes.Status400BadRequest, "Business rule violation", exception.Message),
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.");
        }

        // Same correlation id the framework adds to its own errors, so a client report can be matched to the logs.
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        };
    }
}
