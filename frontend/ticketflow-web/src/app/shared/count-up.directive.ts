import { DestroyRef, Directive, ElementRef, effect, inject, input } from '@angular/core';

const DURATION_MS = 800;
const format = new Intl.NumberFormat('en-US');

/**
 * Counts a number up from its previous value (0 at first) with an ease-out curve. Shows the final
 * value right away when the user prefers reduced motion or the environment has no animation
 * support, so the number is never wrong or missing.
 */
@Directive({ selector: '[appCountUp]' })
export class CountUp {
  readonly appCountUp = input.required<number>();

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private frame: number | undefined;
  private shown = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.cancel());
    effect(() => this.animateTo(this.appCountUp()));
  }

  private animateTo(target: number): void {
    this.cancel();
    const canAnimate =
      typeof window.matchMedia === 'function' &&
      typeof window.requestAnimationFrame === 'function' &&
      !window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!canAnimate || target === this.shown) {
      this.render(target);
      return;
    }

    const from = this.shown;
    const start = performance.now();
    const step = (now: number) => {
      const progress = Math.min((now - start) / DURATION_MS, 1);
      const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic
      this.render(Math.round(from + (target - from) * eased));
      if (progress < 1) {
        this.frame = requestAnimationFrame(step);
      }
    };
    this.frame = requestAnimationFrame(step);
  }

  private render(value: number): void {
    this.shown = value;
    this.element.textContent = format.format(value);
  }

  private cancel(): void {
    if (this.frame !== undefined) {
      cancelAnimationFrame(this.frame);
      this.frame = undefined;
    }
  }
}
