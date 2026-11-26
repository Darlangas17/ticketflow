import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';

import { applyServerErrors, matchesField, passwordStrength, showApiError } from './validators';

describe('passwordStrength', () => {
  const check = (value: string) => passwordStrength(new FormControl(value));

  it('accepts a password with 8+ characters, a letter and a digit', () => {
    expect(check('Secret123')).toBeNull();
  });

  it('leaves empty values to the required validator', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    ['short1', 'minLength'],
    ['onlyletters', 'digit'],
    ['12345678', 'letter'],
  ])('%s fails with %s', (value, rule) => {
    expect(check(value)?.['passwordStrength']).toHaveProperty(rule, true);
  });

  it('rejects passwords over 72 bytes (BCrypt limit), counting multi-byte characters', () => {
    expect(check('a1' + 'ñ'.repeat(36))?.['passwordStrength']).toHaveProperty('maxBytes', true); // 2 + 72 bytes
    expect(check('a1' + 'x'.repeat(70))).toBeNull(); // exactly 72 bytes
  });
});

describe('matchesField', () => {
  function form(password: string, confirm: string) {
    const group = new FormGroup({
      password: new FormControl(password),
      confirm: new FormControl(confirm, matchesField('password')),
    });
    // The validator first ran before the control had a parent; in a form it re-runs on input.
    group.controls.confirm.updateValueAndValidity();
    return group;
  }

  it('passes when both values are equal', () => {
    expect(form('Secret123', 'Secret123').controls.confirm.errors).toBeNull();
  });

  it('fails when they differ', () => {
    expect(form('Secret123', 'Other123').controls.confirm.errors).toEqual({ mismatch: true });
  });
});

describe('applyServerErrors', () => {
  it('maps API field names to controls and returns the rest', () => {
    const form = new FormGroup({ email: new FormControl(''), fullName: new FormControl('') });

    const unmatched = applyServerErrors(form, {
      Email: ['E-mail is taken.'],
      FullName: ['Too long.'],
      Unknown: ['Something else.'],
    });

    expect(form.controls.email.getError('server')).toBe('E-mail is taken.');
    expect(form.controls.fullName.getError('server')).toBe('Too long.');
    expect(form.controls.email.touched).toBe(true);
    expect(unmatched).toEqual(['Something else.']);
  });
});

describe('showApiError', () => {
  const form = () => new FormGroup({ name: new FormControl(''), detail: new FormControl('') });
  const httpError = (status: number, error: object) => new HttpErrorResponse({ status, error });

  it('puts field errors on the form and returns null when nothing else needs showing', () => {
    const f = form();
    const message = showApiError(f, httpError(400, { errors: { Name: ['Name is taken.'] } }));

    expect(message).toBeNull();
    expect(f.controls.name.getError('server')).toBe('Name is taken.');
  });

  it('renames API fields with the map', () => {
    const f = form();
    showApiError(f, httpError(400, { errors: { City: ['Too long.'] } }), { City: 'Detail' });

    expect(f.controls.detail.getError('server')).toBe('Too long.');
  });

  it('returns the general message for errors without fields (e.g. 409)', () => {
    expect(showApiError(form(), httpError(409, { detail: 'Already exists.' }))).toBe(
      'Already exists.',
    );
  });
});
