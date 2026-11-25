import { HttpErrorResponse } from '@angular/common/http';

/** RFC 7807 body returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  /** Only on validation errors (400): field name → messages. */
  errors?: Record<string, string[]>;
}

/** A user-facing message plus the per-field validation errors, if any. */
export interface ApiError {
  status: number;
  message: string;
  fieldErrors: Record<string, string[]>;
}

const GENERIC_MESSAGES: Record<number, string> = {
  0: 'Cannot reach the server. Check your connection and try again.',
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to do this.',
  404: 'The requested item was not found.',
  500: 'Something went wrong on the server. Please try again later.',
};

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'detail' in value);
}

/** Turns any HTTP failure into something a screen can show. */
export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, message: 'An unexpected error occurred.', fieldErrors: {} };
  }

  const body: unknown = error.error;
  const problem = isProblemDetails(body) ? body : undefined;
  const fieldErrors = problem?.errors ?? {};
  const firstFieldError = Object.values(fieldErrors).flat()[0];

  const message =
    problem?.detail ??
    firstFieldError ??
    GENERIC_MESSAGES[error.status] ??
    problem?.title ??
    `Request failed (${error.status}).`;

  return { status: error.status, message, fieldErrors };
}
