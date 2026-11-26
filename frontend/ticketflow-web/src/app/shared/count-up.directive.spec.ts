import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { CountUp } from './count-up.directive';

@Component({
  imports: [CountUp],
  template: `<span [appCountUp]="value()"></span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly value = signal(1234);
}

describe('CountUp', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return fixture;
  }

  it('shows the final value at once when animations are not available', async () => {
    // jsdom has no matchMedia: the number must still be correct, never stuck at 0.
    const fixture = await render();

    expect(fixture.nativeElement.textContent.trim()).toBe('1,234');
  });

  it('respects the reduced-motion preference', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query.includes('reduce') }));
    const fixture = await render();

    expect(fixture.nativeElement.textContent.trim()).toBe('1,234');
  });

  it('animates towards the value when motion is allowed', async () => {
    const frames: FrameRequestCallback[] = [];
    vi.stubGlobal('matchMedia', () => ({ matches: false }));
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => frames.push(cb));
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
    vi.spyOn(performance, 'now').mockReturnValue(1000); // fixed clock: start = 1000 ms
    const start = 1000;
    const fixture = await render();

    // Angular's zoneless scheduler also queues frames, so run every pending one at each time.
    const flushFrames = (time: number) => frames.splice(0).forEach((frame) => frame(time));

    flushFrames(start + 400); // halfway through the 800 ms animation
    const middle = Number(fixture.nativeElement.textContent.replace(/,/g, ''));
    flushFrames(start + 2000); // past the end

    expect(middle).toBeGreaterThan(0);
    expect(middle).toBeLessThan(1234);
    expect(fixture.nativeElement.textContent.trim()).toBe('1,234');
  });
});
