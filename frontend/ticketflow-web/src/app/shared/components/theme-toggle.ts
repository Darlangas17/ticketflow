import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ThemeService } from '../../core/theme/theme.service';

/** Cycles system → light → dark. The icon spins in when it changes. */
@Component({
  selector: 'app-theme-toggle',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <button
      mat-icon-button
      type="button"
      [attr.aria-label]="theme.label()"
      [matTooltip]="theme.label()"
      (click)="theme.cycle()"
    >
      @for (icon of [theme.icon()]; track icon) {
        <mat-icon class="icon">{{ icon }}</mat-icon>
      }
    </button>
  `,
  styles: `
    .icon {
      animation: spin-in 360ms var(--tf-ease-spring) both;
    }
    @keyframes spin-in {
      from {
        opacity: 0;
        transform: rotate(-90deg) scale(0.6);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
}
