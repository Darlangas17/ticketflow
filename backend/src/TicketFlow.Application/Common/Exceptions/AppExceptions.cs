namespace TicketFlow.Application.Common.Exceptions;

// Application errors that the API translates into HTTP ProblemDetails responses
// (see the global exception handler in TicketFlow.Api). Validation errors use
// FluentValidation.ValidationException.

/// <summary>Credentials are missing or invalid (HTTP 401).</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>The user is authenticated but not allowed to perform the action (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>The requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with the current state, e.g. a duplicate e-mail (HTTP 409).</summary>
public sealed class ConflictException(string message) : Exception(message);
