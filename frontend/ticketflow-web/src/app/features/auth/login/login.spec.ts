import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import type { MockInstance } from 'vitest';

import { authResponse } from '../../../../testing/auth-fixtures';
import { AuthService } from '../../../core/auth/auth.service';
import { DEMO_PASSWORD, Login } from './login';

describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let element: HTMLElement;
  let http: HttpTestingController;
  let navigateByUrl: MockInstance<Router['navigateByUrl']>;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Login);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function input(name: string): HTMLInputElement {
    return element.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
  }

  function type(name: string, value: string): void {
    const field = input(name);
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  function submitButton(): HTMLButtonElement {
    return element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  function errors(): string[] {
    return [...element.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
  }

  it('shows required errors and does not call the API when the form is empty', async () => {
    await submit();

    expect(errors()).toEqual(['E-mail is required.', 'Password is required.']);
    http.expectNone('/api/auth/login');
  });

  it('rejects an invalid e-mail', async () => {
    type('email', 'not-an-email');
    type('password', 'whatever');

    await submit();

    expect(errors()).toEqual(['Enter a valid e-mail address.']);
    http.expectNone('/api/auth/login');
  });

  it('signs in and goes to the dashboard', async () => {
    type('email', 'admin@ticketflow.demo');
    type('password', DEMO_PASSWORD);

    await submit();
    expect(submitButton().disabled).toBe(true); // loading while the request is in flight

    const request = http.expectOne('/api/auth/login');
    expect(request.request.body).toEqual({
      email: 'admin@ticketflow.demo',
      password: DEMO_PASSWORD,
    });
    request.flush(authResponse('Admin'));
    await fixture.whenStable();

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
    expect(navigateByUrl).toHaveBeenCalledWith('/dashboard');
    expect(submitButton().disabled).toBe(false);
  });

  it('returns to the requested page after signing in', async () => {
    fixture.componentRef.setInput('returnUrl', '/tickets/12');
    type('email', 'admin@ticketflow.demo');
    type('password', DEMO_PASSWORD);

    await submit();
    http.expectOne('/api/auth/login').flush(authResponse('Admin'));
    await fixture.whenStable();

    expect(navigateByUrl).toHaveBeenCalledWith('/tickets/12');
  });

  it('ignores a returnUrl that points to another site', async () => {
    fixture.componentRef.setInput('returnUrl', '//evil.example');
    type('email', 'admin@ticketflow.demo');
    type('password', DEMO_PASSWORD);

    await submit();
    http.expectOne('/api/auth/login').flush(authResponse('Admin'));
    await fixture.whenStable();

    expect(navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it("shows the API's message when the credentials are wrong", async () => {
    type('email', 'admin@ticketflow.demo');
    type('password', 'WrongPassword1');

    await submit();
    http
      .expectOne('/api/auth/login')
      .flush(
        { status: 401, title: 'Unauthorized', detail: 'Invalid email or password.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')!.textContent!.trim()).toBe(
      'Invalid email or password.',
    );
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(submitButton().disabled).toBe(false);
  });

  it('shows a clear message when the server cannot be reached', async () => {
    type('email', 'admin@ticketflow.demo');
    type('password', DEMO_PASSWORD);

    await submit();
    http.expectOne('/api/auth/login').error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(
      'Cannot reach the server',
    );
  });

  it('fills the form with a demo account', async () => {
    const technician = [...element.querySelectorAll<HTMLButtonElement>('.demo-list button')].find(
      (b) => b.textContent!.includes('Technician'),
    )!;

    technician.click();
    await fixture.whenStable();

    expect(input('email').value).toBe('carlos.rivera@ticketflow.demo');
    expect(input('password').value).toBe(DEMO_PASSWORD);
  });

  it('explains why the user was signed out', async () => {
    fixture.componentRef.setInput('reason', 'expired');
    await fixture.whenStable();

    expect(element.querySelector('[role="status"]')!.textContent).toContain(
      'Your session has expired',
    );
  });

  it('toggles password visibility', async () => {
    const toggle = element.querySelector<HTMLButtonElement>('button[aria-label="Show password"]')!;
    expect(input('password').type).toBe('password');

    toggle.click();
    await fixture.whenStable();

    expect(input('password').type).toBe('text');
    expect(toggle.getAttribute('aria-label')).toBe('Hide password');
  });
});
