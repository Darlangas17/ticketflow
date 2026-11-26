import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { BRANCHES, CATEGORIES, details, signInAs } from '../../../../testing/ticket-fixtures';
import { TicketForm } from './ticket-form';

describe('TicketForm', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let element: HTMLElement;

  beforeEach(async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(
          [
            { path: 'tickets/new', component: TicketForm },
            { path: 'tickets/:id/edit', component: TicketForm },
            { path: 'tickets/:id', children: [] },
          ],
          withComponentInputBinding(),
        ),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    await signInAs('Requester');
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function open(url: string): Promise<void> {
    await harness.navigateByUrl(url, TicketForm);
    await harness.fixture.whenStable();
    http.expectOne('/api/branches').flush(BRANCHES);
    http.expectOne('/api/categories').flush(CATEGORIES);
  }

  function type(selector: string, value: string): void {
    const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
    field.value = value;
    field.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
  }

  function errors(): string[] {
    return [...element.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
  }

  it('validates the required fields before calling the API', async () => {
    await open('/tickets/new');
    await harness.fixture.whenStable();
    element = harness.routeNativeElement!;

    await submit();

    expect(errors()).toEqual([
      'Title is required.',
      'Description is required.',
      'Choose a category.',
      'Choose the branch where the problem is.',
    ]);
  });

  it('creates the ticket with trimmed values and opens it', async () => {
    await open('/tickets/new');
    await harness.fixture.whenStable();
    element = harness.routeNativeElement!;
    const form = harness.routeDebugElement!.componentInstance as TicketForm;
    // mat-select is driven through the form model here; the browser checks cover the dropdown UI.
    (form as unknown as { form: { patchValue(v: object): void } }).form.patchValue({
      categoryId: 2,
      branchId: 1,
    });
    type('input[formControlName="title"]', '  Laptop does not power on  ');
    type('textarea[formControlName="description"]', 'No lights at all.');

    await submit();
    const request = http.expectOne('/api/tickets');
    expect(request.request.body).toEqual({
      title: 'Laptop does not power on',
      description: 'No lights at all.',
      priority: 'Medium',
      categoryId: 2,
      branchId: 1,
    });
    request.flush(details({ id: 42 }));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/tickets/42');
  });

  it('pre-fills the form when editing and does not allow changing the branch', async () => {
    await harness.navigateByUrl('/tickets/7/edit', TicketForm);
    await harness.fixture.whenStable();
    http.expectOne('/api/branches').flush(BRANCHES);
    http.expectOne('/api/categories').flush(CATEGORIES);
    http.expectOne('/api/tickets/7').flush(details({ title: 'Printer offline' }));
    await harness.fixture.whenStable();
    element = harness.routeNativeElement!;

    expect(element.querySelector<HTMLInputElement>('input[formControlName="title"]')!.value).toBe(
      'Printer offline',
    );
    expect(element.querySelector('mat-select[formControlName="branchId"]')).toBeNull();
    expect(element.textContent).toContain('cannot be changed after the ticket is created');

    type('input[formControlName="title"]', 'Printer offline (warehouse)');
    await submit();
    const request = http.expectOne('/api/tickets/7');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).not.toHaveProperty('branchId');
    request.flush(details());
  });

  it('reports unsaved changes until the ticket is saved', async () => {
    await open('/tickets/new');
    await harness.fixture.whenStable();
    element = harness.routeNativeElement!;
    const form = harness.routeDebugElement!.componentInstance as TicketForm;

    expect(form.hasUnsavedChanges()).toBe(false);
    type('input[formControlName="title"]', 'Something');
    expect(form.hasUnsavedChanges()).toBe(true);
  });

  it('shows a message instead of the form for closed tickets', async () => {
    await harness.navigateByUrl('/tickets/7/edit', TicketForm);
    await harness.fixture.whenStable();
    http.expectOne('/api/branches').flush(BRANCHES);
    http.expectOne('/api/categories').flush(CATEGORIES);
    http.expectOne('/api/tickets/7').flush(details({ status: 'Closed' }));
    await harness.fixture.whenStable();
    element = harness.routeNativeElement!;

    expect(element.querySelector('form')).toBeNull();
    expect(element.textContent).toContain('closed and can no longer be edited');
  });
});
