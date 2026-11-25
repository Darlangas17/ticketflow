import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import type { MockInstance } from 'vitest';

import { authResponse } from '../../../../testing/auth-fixtures';
import { Register } from './register';

describe('Register', () => {
  let fixture: ComponentFixture<Register>;
  let element: HTMLElement;
  let http: HttpTestingController;
  let navigateByUrl: MockInstance<Router['navigateByUrl']>;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Register);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function type(name: string, value: string): void {
    const field = element.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  function fill(
    values: Partial<Record<'fullName' | 'email' | 'password' | 'confirmPassword', string>>,
  ): void {
    const defaults = {
      fullName: 'Nuevo Usuario',
      email: 'nuevo.usuario@ticketflow.demo',
      password: 'Secret123',
      confirmPassword: 'Secret123',
    };
    for (const [name, value] of Object.entries({ ...defaults, ...values })) {
      type(name, value);
    }
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  function errors(): string[] {
    return [...element.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
  }

  it('applies the same password rules as the API', async () => {
    fill({ password: 'onlyletters', confirmPassword: 'onlyletters' });

    await submit();

    expect(errors()).toEqual(['Use at least 8 characters, with a letter and a digit.']);
    http.expectNone('/api/auth/register');
  });

  it('requires matching passwords', async () => {
    fill({ confirmPassword: 'Different123' });

    await submit();

    expect(errors()).toEqual(['The passwords do not match.']);
  });

  it('re-checks the confirmation when the password changes', async () => {
    fill({});
    type('password', 'Changed123');

    await submit();

    expect(errors()).toEqual(['The passwords do not match.']);
  });

  it('creates the account without sending the confirmation and goes to the dashboard', async () => {
    fill({});

    await submit();
    const request = http.expectOne('/api/auth/register');
    expect(request.request.body).toEqual({
      fullName: 'Nuevo Usuario',
      email: 'nuevo.usuario@ticketflow.demo',
      password: 'Secret123',
    });
    request.flush(authResponse('Requester'));
    await fixture.whenStable();

    expect(navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it("shows the API's field errors under the matching inputs", async () => {
    fill({});

    await submit();
    http
      .expectOne('/api/auth/register')
      .flush(
        {
          status: 400,
          title: 'One or more validation errors occurred.',
          errors: { Email: ["'Email' is not a valid email address."] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    expect(errors()).toEqual(["'Email' is not a valid email address."]);
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('shows a taken e-mail as a general message', async () => {
    fill({});

    await submit();
    http
      .expectOne('/api/auth/register')
      .flush(
        { status: 409, title: 'Conflict', detail: 'An account with this email already exists.' },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')!.textContent!.trim()).toBe(
      'An account with this email already exists.',
    );
  });
});
