import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { BarItem, BarList } from './bar-list';

describe('BarList', () => {
  async function render(items: BarItem[]) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(BarList);
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('caption', 'Tickets by status');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders an accessible table with the value as text on every row', async () => {
    const element = await render([
      { label: 'Open', value: 6 },
      { label: 'Closed', value: 2 },
    ]);

    expect(element.querySelector('caption')!.textContent!.trim()).toBe('Tickets by status');
    const rows = [...element.querySelectorAll('tbody tr')];
    expect(rows.map((r) => r.querySelector('th')!.textContent!.trim())).toEqual(['Open', 'Closed']);
    expect(rows.map((r) => r.querySelector('.value')!.textContent!.trim())).toEqual(['6', '2']);
    // Share of the total is available to screen readers, not only on hover.
    expect(rows[0].querySelector('.visually-hidden')!.textContent!.trim()).toBe('75%');
  });

  it('scales bars to the largest value and leaves zero empty', async () => {
    const element = await render([
      { label: 'A', value: 10 },
      { label: 'B', value: 5 },
      { label: 'C', value: 0 },
    ]);

    const widths = [...element.querySelectorAll<HTMLElement>('.bar')].map((b) => b.style.width);
    expect(widths).toEqual(['100%', '50%', '0%']);
  });

  it('links rows to the filtered ticket list', async () => {
    const element = await render([
      { label: 'On hold', value: 4, queryParams: { status: 'OnHold' } },
    ]);

    expect(element.querySelector('a')!.getAttribute('href')).toBe('/tickets?status=OnHold');
  });
});
