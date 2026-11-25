using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace TicketFlow.Api.ErrorHandling;

/// <summary>
/// Writes a ProblemDetails body through <see cref="IProblemDetailsService"/>, the same way the
/// global exception handler does. Unlike an ObjectResult, the content type is not overridden by
/// [Produces("application/json")], so every error is sent as application/problem+json.
/// </summary>
public sealed class ProblemDetailsResult(ProblemDetails problem) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status400BadRequest;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    /// <summary>
    /// Replaces the default response for model-binding errors (e.g. "status=Sleeping" in the query string
    /// or malformed JSON), which [ApiController] returns before the action runs.
    /// </summary>
    public static IActionResult ForInvalidModelState(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
        return new ProblemDetailsResult(problem);
    }
}
