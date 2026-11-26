import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { USERS } from '../../../../testing/auth-fixtures';
import { details, signInAs } from '../../../../testing/ticket-fixtures';
import { UserRole } from '../../../core/models/auth.models';
import { TicketDetails } from '../../../core/models/ticket.models';
import { TicketDetail } from './ticket-detail';

describe('TicketDetail', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(
          [
            { path: 'tickets/:id', component: TicketDetail },
            { path: 'tickets', children: [] },
          ],
          withComponentInputBinding(),
        ),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function open(role: UserRole, ticket: TicketDetails | 'not-found'): Promise<HTMLElement> {
    await signInAs(role);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/tickets/7', TicketDetail);
    await harness.fixture.whenStable();
    if (role !== 'Requester') {
      http
        .expectOne('/api/users/assignees')
        .flush([{ id: USERS.Technician.id, fullName: USERS.Technician.fullName }]);
    }
    const request = http.expectOne('/api/tickets/7');
    if (ticket === 'not-found') {
      request.flush(null, { status: 404, statusText: 'Not Found' });
    } else {
      request.flush(ticket);
    }
    await harness.fixture.whenStable();
    return harness.routeNativeElement!;
  }

  const buttons = (element: HTMLElement) =>
    [...element.querySelectorAll('button, a')].map((b) => b.textContent!.trim());

  it('offers technicians the next workflow steps and assignment', async () => {
    const element = await open('Technician', details({ status: 'InProgress' }));

    const labels = buttons(element);
    expect(labels).toEqual(
      expect.arrayContaining([
        expect.stringContaining('Put on hold'),
        expect.stringContaining('Mark as resolved'),
        expect.stringContaining('Assign to me'),
      ]),
    );
    expect(labels.some((l) => l.includes('Delete'))).toBe(false);
  });

  it('a requester only sees "Close ticket" once their ticket is resolved', async () => {
    const element = await open('Requester', details({ status: 'Resolved' }));

    const labels = buttons(element);
    expect(labels.some((l) => l.includes('Close ticket'))).toBe(true);
    expect(labels.some((l) => l.includes('Assign to me'))).toBe(false);
    expect(element.querySelector('mat-select')).toBeNull();
  });

  it('changes the status and shows the updated ticket', async () => {
    const element = await open('Technician', details({ status: 'Open' }));

    [...element.querySelectorAll<HTMLButtonElement>('button')]
      .find((b) => b.textContent!.includes('Start work'))!
      .click();
    const request = http.expectOne('/api/tickets/7/status');
    expect(request.request.body).toEqual({ status: 'InProgress' });
    request.flush(details({ status: 'InProgress' }));
    await harness.fixture.whenStable();

    expect(element.querySelector('app-status-badge')!.textContent).toContain('In progress');
  });

  it('adds a comment to the list', async () => {
    const element = await open('Requester', details());

    const textarea = element.querySelector<HTMLTextAreaElement>('textarea')!;
    textarea.value = '  It happens again  ';
    textarea.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit'));

    const request = http.expectOne('/api/tickets/7/comments');
    expect(request.request.body).toEqual({ body: 'It happens again' });
    request.flush({
      id: 1,
      author: { id: USERS.Requester.id, fullName: USERS.Requester.fullName },
      body: 'It happens again',
      createdAt: '2026-03-02T09:00:00Z',
    });
    await harness.fixture.whenStable();

    expect(element.querySelectorAll('.comment')).toHaveLength(1);
    expect(textarea.value).toBe('');
  });

  it('hides editing and commenting on closed tickets', async () => {
    const element = await open('Admin', details({ status: 'Closed' }));

    expect(buttons(element).some((l) => l === 'editEdit' || l.endsWith('Edit'))).toBe(false);
    expect(element.querySelector('textarea')).toBeNull();
    expect(element.textContent).toContain('Closed tickets cannot receive new comments.');
    expect(buttons(element).some((l) => l.includes('Delete'))).toBe(true);
  });

  it('explains when the ticket does not exist or is not visible', async () => {
    const element = await open('Requester', 'not-found');

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(
      'does not exist or you do not have access',
    );
  });
});
