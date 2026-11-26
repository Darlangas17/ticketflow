import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { UserAccount } from '../../../core/models/user.models';
import { UserDialog, UserDialogData } from './user-dialog';

const ANA: UserAccount = {
  id: 3,
  fullName: 'Ana Torres',
  email: 'ana.torres@ticketflow.demo',
  role: 'Technician',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
};

describe('UserDialog', () => {
  let http: HttpTestingController;
  let close: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<UserDialog>;
  let element: HTMLElement;

  async function open(data: UserDialogData): Promise<void> {
    close = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UserDialog);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  function type(name: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(`input[formControlName="${name}"]`)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('requires a strong password when creating', async () => {
    await open({ isSelf: false });
    type('fullName', 'Diego Flores');
    type('email', 'diego.flores@ticketflow.demo');
    type('password', 'weak');

    await submit();

    expect(element.textContent).toContain('Use at least 8 characters');
    http.expectNone('/api/users');
  });

  it('creates the user and closes with the result', async () => {
    await open({ isSelf: false });
    type('fullName', ' Diego Flores ');
    type('email', 'diego.flores@ticketflow.demo');
    type('password', 'Password123');

    await submit();
    const request = http.expectOne('/api/users');
    expect(request.request.body).toEqual({
      fullName: 'Diego Flores',
      email: 'diego.flores@ticketflow.demo',
      password: 'Password123',
      role: 'Requester',
    });
    request.flush({ ...ANA, id: 9, fullName: 'Diego Flores' });
    await fixture.whenStable();

    expect(close).toHaveBeenCalledWith(expect.objectContaining({ id: 9 }));
  });

  it('only sends a password when editing if one was typed', async () => {
    await open({ user: ANA, isSelf: false });

    await submit();
    const request = http.expectOne('/api/users/3');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      fullName: 'Ana Torres',
      email: 'ana.torres@ticketflow.demo',
      role: 'Technician',
      isActive: true,
    });
    request.flush(ANA);
  });

  it('locks role and status when admins edit themselves', async () => {
    await open({ user: { ...ANA, role: 'Admin' }, isSelf: true });

    expect(element.querySelector('mat-select')!.getAttribute('aria-disabled')).toBe('true');
    expect(element.textContent).toContain('cannot change your own role');
  });

  it("shows the API's conflict message and stays open", async () => {
    await open({ isSelf: false });
    type('fullName', 'Copy');
    type('email', 'admin@ticketflow.demo');
    type('password', 'Password123');

    await submit();
    http
      .expectOne('/api/users')
      .flush(
        { status: 409, title: 'Conflict', detail: 'An account with this email already exists.' },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('already exists');
    expect(close).not.toHaveBeenCalled();
  });
});
