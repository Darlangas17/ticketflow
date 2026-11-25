import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { authResponse } from '../../../testing/auth-fixtures';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    // logout() navigates to /login, which this test router does not define.
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => controller.verify());

  async function signIn(): Promise<string> {
    const response = authResponse('Technician');
    const login = firstValueFrom(auth.login({ email: 'a@b.c', password: 'x' }));
    controller.expectOne('/api/auth/login').flush(response);
    await login;
    return response.accessToken;
  }

  it('adds the bearer token to API requests', async () => {
    const token = await signIn();

    void firstValueFrom(http.get('/api/tickets'));

    const request = controller.expectOne('/api/tickets');
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    request.flush({});
  });

  it('never sends the token to other hosts', async () => {
    await signIn();

    void firstValueFrom(http.get('https://example.com/api/tickets'));

    const request = controller.expectOne('https://example.com/api/tickets');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('sends API requests without a header when signed out', () => {
    void firstValueFrom(http.get('/api/branches')).catch(() => undefined);

    const request = controller.expectOne('/api/branches');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('signs the user out when the API answers 401', async () => {
    await signIn();
    const logout = vi.spyOn(auth, 'logout');

    const call = firstValueFrom(http.get('/api/tickets'));
    controller.expectOne('/api/tickets').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toMatchObject({ status: 401 });
    expect(logout).toHaveBeenCalledWith('expired', expect.any(String));
  });

  it('does not sign out on 403 (signed in, but not allowed)', async () => {
    await signIn();
    const logout = vi.spyOn(auth, 'logout');

    const call = firstValueFrom(http.delete('/api/tickets/1'));
    controller.expectOne('/api/tickets/1').flush(null, { status: 403, statusText: 'Forbidden' });

    await expect(call).rejects.toMatchObject({ status: 403 });
    expect(logout).not.toHaveBeenCalled();
  });

  it('treats 401 on login as wrong credentials, not as an expired session', async () => {
    const logout = vi.spyOn(auth, 'logout');

    const login = firstValueFrom(auth.login({ email: 'a@b.c', password: 'wrong' }));
    controller
      .expectOne('/api/auth/login')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(login).rejects.toMatchObject({ status: 401 });
    expect(logout).not.toHaveBeenCalled();
  });
});
