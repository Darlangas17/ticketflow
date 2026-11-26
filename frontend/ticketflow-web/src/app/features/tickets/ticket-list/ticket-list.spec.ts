import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { USERS } from '../../../../testing/auth-fixtures';
import {
  BRANCHES,
  CATEGORIES,
  listItem,
  page,
  signInAs,
} from '../../../../testing/ticket-fixtures';
import { UserRole } from '../../../core/models/auth.models';
import { TicketList } from './ticket-list';

describe('TicketList', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'tickets', component: TicketList }], withComponentInputBinding()),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function open(role: UserRole, url = '/tickets'): Promise<HTMLElement> {
    await signInAs(role);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, TicketList);
    await harness.fixture.whenStable();
    http.expectOne('/api/branches').flush(BRANCHES);
    http.expectOne('/api/categories').flush(CATEGORIES);
    if (role !== 'Requester') {
      http
        .expectOne('/api/users/assignees')
        .flush([{ id: USERS.Technician.id, fullName: USERS.Technician.fullName }]);
    }
    return harness.routeNativeElement!;
  }

  function listRequest() {
    return http.expectOne((r) => r.url === '/api/tickets');
  }

  it('sends the URL filters to the API and shows the tickets', async () => {
    const element = await open('Technician', '/tickets?status=Open&unassigned=true&page=2');

    const request = listRequest();
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('unassigned')).toBe('true');
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([listItem(4, { title: 'VPN down' }), listItem(9)], 22, 2));
    await harness.fixture.whenStable();

    expect(element.querySelectorAll('tr[mat-row]')).toHaveLength(2);
    expect(element.textContent).toContain('VPN down');
    expect(element.querySelector('.summary')!.textContent).toContain('22 tickets');
    expect(element.querySelector('th.mat-column-requester')).not.toBeNull();
  });

  it('calls it "My tickets" for requesters and hides the requester column and assignee filter', async () => {
    const element = await open('Requester');
    listRequest().flush(page([listItem(1)]));
    await harness.fixture.whenStable();

    expect(element.querySelector('h1')!.textContent).toContain('My tickets');
    expect(element.querySelector('th.mat-column-requester')).toBeNull();
    const filterLabels = [...element.querySelectorAll('form mat-label')].map((l) => l.textContent);
    expect(filterLabels).not.toContain('Assignee');
  });

  it('puts the search text in the URL after a pause', async () => {
    const element = await open('Admin');
    listRequest().flush(page([listItem(1)]));
    await harness.fixture.whenStable();

    const search = element.querySelector<HTMLInputElement>('input[formControlName="search"]')!;
    search.value = '  printer ';
    search.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 450));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/tickets?search=printer');
    expect(listRequest().request.params.get('search')).toBe('printer');
  });

  it('shows an empty state with a way to clear filters', async () => {
    const element = await open('Admin', '/tickets?priority=Critical');
    listRequest().flush(page([]));
    await harness.fixture.whenStable();

    expect(element.textContent).toContain('No tickets match these filters');
    const clear = [...element.querySelectorAll('button')].find((b) =>
      b.textContent!.includes('Clear filters'),
    )!;
    clear.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/tickets');
    listRequest().flush(page([listItem(1)]));
  });

  it('shows the error and retries on demand', async () => {
    const element = await open('Admin');
    listRequest().flush(null, { status: 500, statusText: 'Server Error' });
    await harness.fixture.whenStable();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Something went wrong');
    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    await harness.fixture.whenStable();

    listRequest().flush(page([listItem(1)]));
    await harness.fixture.whenStable();
    expect(element.querySelectorAll('tr[mat-row]')).toHaveLength(1);
  });
});
