using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketFlow.Api.Auth;
using TicketFlow.Api.Security;
using TicketFlow.Application.Auth;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates a new account with the Requester role and returns an access token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(SecuritySetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Me), response);
    }

    /// <summary>Validates the credentials and returns a JWT access token with the user's data.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecuritySetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken) =>
        authService.LoginAsync(request, cancellationToken);

    /// <summary>Returns the user that owns the access token.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<UserDto> Me(CancellationToken cancellationToken) =>
        authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);
}
