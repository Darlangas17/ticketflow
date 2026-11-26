import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { signInAs } from '../../../testing/ticket-fixtures';
import { DashboardSummary } from '../../core/models/dashboard.models';
import { Dashboard, formatDuration, toCharts } from './dashboard';

const SUMMARY: DashboardSummary = {
  totalTickets: 30,
  activeTickets: 18,
  unassignedTickets: 3,
  resolvedTickets: 12,
  averageResolutionHours: 37.25,
  byStatus: [
    { status: 'Open', count: 7 },
    { status: 'InProgress', count: 7 },
    { status: 'OnHold', count: 4 },
    { status: 'Resolved', count: 6 },
    { status: 'Closed', count: 6 },
  ],
  byPriority: [
    { priority: 'Low', count: 7 },
    { priority: 'Medium', count: 12 },
    { priority: 'High', count: 8 },
    { priority: 'Critical', count: 3 },
  ],
  byBranch: [
    { id: 3, name: 'La Ceiba Coast', count: 12 },
    { id: 2, name: 'San Pedro Sula North', count: 11 },
    { id: 1, name: 'Tegucigalpa Central', count: 7 },
  ],
  byCategory: [{ id: 2, name: 'Hardware', count: 30 }],
};

describe('formatDuration', () => {
  it.each([
    [null, '—'],
    [0.5, '0.5 h'],
    [37.25, '37.3 h'],
    [47.9, '47.9 h'],
    [60, '2.5 days'],
  ])('%s → %s', (hours, expected) => {
    expect(formatDuration(hours)).toBe(expected);
  });
});

describe('toCharts', () => {
  it('uses readable labels, puts the highest priority first and links each bar to its tickets', () => {
    const charts = toCharts(SUMMARY);

    expect(charts.status.map((s) => s.label)).toEqual([
      'Open',
      'In progress',
      'On hold',
      'Resolved',
      'Closed',
    ]);
    expect(charts.priority.map((p) => p.label)).toEqual(['Critical', 'High', 'Medium', 'Low']);
    expect(charts.branch[0]).toEqual({
      label: 'La Ceiba Coast',
      value: 12,
      queryParams: { branchId: 3 },
    });
    expect(charts.status[2].queryParams).toEqual({ status: 'OnHold' });
  });
});

describe('Dashboard', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    await signInAs('Admin');
    const fixture = TestBed.createComponent(Dashboard);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('shows the KPIs and four charts from the summary', async () => {
    const { fixture, element } = await render();
    http.expectOne('/api/dashboard/summary').flush(SUMMARY);
    await fixture.whenStable();

    const kpis = [...element.querySelectorAll('.kpi-value')].map((k) => k.textContent!.trim());
    expect(kpis).toEqual(['30', '18', '3', '37.3 h']);
    expect(element.querySelectorAll('table.bars')).toHaveLength(4);
    expect(element.querySelector('.scope')!.textContent).toContain('All tickets in the company');
  });

  it('explains when there is nothing to chart', async () => {
    const { fixture, element } = await render();
    http.expectOne('/api/dashboard/summary').flush({
      ...SUMMARY,
      totalTickets: 0,
      averageResolutionHours: null,
    });
    await fixture.whenStable();

    expect(element.querySelectorAll('table.bars')).toHaveLength(0);
    expect(element.textContent).toContain('There are no tickets yet');
  });

  it('shows the error and reloads on demand', async () => {
    const { fixture, element } = await render();
    http.expectOne('/api/dashboard/summary').flush(null, { status: 500, statusText: 'Error' });
    await fixture.whenStable();

    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    await fixture.whenStable();
    http.expectOne('/api/dashboard/summary').flush(SUMMARY);
    await fixture.whenStable();

    expect(element.querySelectorAll('.kpi')).toHaveLength(4);
  });
});
