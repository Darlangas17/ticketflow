import { HttpErrorResponse } from '@angular/common/http';

import { toApiError } from './api-error';

describe('toApiError', () => {
  const httpError = (status: number, error: unknown = null) =>
    new HttpErrorResponse({ status, error, url: '/api/tickets' });

  it("uses the ProblemDetails 'detail' when present", () => {
    const error = httpError(400, {
      title: 'Business rule violation',
      detail: 'Invalid status transition from Open to Closed. Allowed: InProgress.',
    });

    expect(toApiError(error).message).toBe(
      'Invalid status transition from Open to Closed. Allowed: InProgress.',
    );
  });

  it('uses the first field error for validation problems and keeps all of them', () => {
    const error = httpError(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        Title: ["'Title' must not be empty."],
        BranchId: ["'Branch Id' must be greater than '0'."],
      },
    });

    const apiError = toApiError(error);

    expect(apiError.message).toBe("'Title' must not be empty.");
    expect(Object.keys(apiError.fieldErrors)).toEqual(['Title', 'BranchId']);
  });

  it.each([
    [0, 'Cannot reach the server'],
    [403, 'You do not have permission'],
    [404, 'not found'],
    [500, 'Something went wrong'],
  ])('gives a friendly message for status %i without a body', (status, text) => {
    expect(toApiError(httpError(status)).message).toContain(text);
  });

  it('handles non-HTTP errors', () => {
    expect(toApiError(new Error('boom')).message).toBe('An unexpected error occurred.');
  });
});
