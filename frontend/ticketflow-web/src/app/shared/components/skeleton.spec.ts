import { TestBed } from '@angular/core/testing';

import { Skeleton } from './skeleton';

describe('Skeleton', () => {
  it('announces loading to screen readers and hides the placeholder shapes from them', async () => {
    const fixture = TestBed.createComponent(Skeleton);
    fixture.componentRef.setInput('message', 'Loading tickets…');
    fixture.componentRef.setInput('rows', 3);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    const status = element.querySelector('[role="status"]')!;
    expect(status.getAttribute('aria-busy')).toBe('true');
    expect(status.textContent).toContain('Loading tickets…');
    expect(element.querySelector('[aria-hidden="true"]')!.querySelectorAll('.row')).toHaveLength(3);
  });

  it('renders the dashboard shape', async () => {
    const fixture = TestBed.createComponent(Skeleton);
    fixture.componentRef.setInput('variant', 'dashboard');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.kpis .tile')).toHaveLength(4);
  });
});
