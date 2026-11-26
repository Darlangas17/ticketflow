import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';

import { toApiError } from '../../core/api/api-error';

// Validators shared by the forms of the app.

/**
 * Same rules as the API (AccountRules.ValidPassword): 8+ characters, at most 72 bytes
 * (BCrypt's limit), at least one letter and one digit. The API remains the source of truth.
 */
export const passwordStrength: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const value: unknown = control.value;
  if (typeof value !== 'string' || value === '') {
    return null; // `required` reports empty values
  }

  const errors: ValidationErrors = {};
  if (value.length < 8) {
    errors['minLength'] = true;
  }
  if (new TextEncoder().encode(value).length > 72) {
    errors['maxBytes'] = true;
  }
  if (!/[A-Za-z]/.test(value)) {
    errors['letter'] = true;
  }
  if (!/[0-9]/.test(value)) {
    errors['digit'] = true;
  }
  return Object.keys(errors).length > 0 ? { passwordStrength: errors } : null;
};

/**
 * The control must equal its sibling `otherName` (e.g. "confirm password"). Re-run it with
 * updateValueAndValidity() when the sibling changes.
 */
export function matchesField(otherName: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const other = control.parent?.get(otherName);
    if (!other || !control.value) {
      return null;
    }
    return control.value === other.value ? null : { mismatch: true };
  };
}

/**
 * Shows the API's validation errors (ProblemDetails `errors`, keyed "Email", "Password"...)
 * on the matching form controls ("email", "password"...). Returns the messages that did not
 * match any control, so the page can show them as a general error.
 */
export function applyServerErrors(
  form: FormGroup,
  fieldErrors: Record<string, string[]>,
): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(fieldErrors)) {
    const controlName = field.charAt(0).toLowerCase() + field.slice(1);
    const control = form.get(controlName);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

/**
 * Shows an API failure on a form: validation errors under their fields, and returns the message to
 * show above the form (null when every error is already shown on a field). `fieldMap` renames API
 * fields to form controls, e.g. { City: 'Detail' }.
 */
export function showApiError(
  form: FormGroup,
  error: unknown,
  fieldMap: Record<string, string> = {},
): string | null {
  const apiError = toApiError(error);
  const fieldErrors = Object.fromEntries(
    Object.entries(apiError.fieldErrors).map(([field, messages]) => [
      fieldMap[field] ?? field,
      messages,
    ]),
  );
  const unmatched = applyServerErrors(form, fieldErrors);
  const allShownOnFields = Object.keys(fieldErrors).length > 0 && unmatched.length === 0;
  return allShownOnFields ? null : (unmatched[0] ?? apiError.message);
}
