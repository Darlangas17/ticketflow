import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

// Loading, error and empty states used by every screen that loads data.

@Component({
  selector: 'app-loading',
  imports: [MatProgressSpinnerModule],
  template: `
    <div class="state" role="status" aria-live="polite">
      <mat-spinner diameter="36" aria-hidden="true" />
      <span>{{ message() }}</span>
    </div>
  `,
  styleUrl: './load-states.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Loading {
  readonly message = input('Loading…');
}

@Component({
  selector: 'app-error-state',
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="state" role="alert">
      <mat-icon class="icon error" aria-hidden="true">error_outline</mat-icon>
      <p>{{ message() }}</p>
      @if (retryable()) {
        <button mat-stroked-button type="button" (click)="retry.emit()">
          <mat-icon>refresh</mat-icon>
          Try again
        </button>
      }
    </div>
  `,
  styleUrl: './load-states.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorState {
  readonly message = input.required<string>();
  readonly retryable = input(true);
  readonly retry = output();
}

@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="state">
      <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <p class="title">{{ title() }}</p>
      @if (hint()) {
        <p class="hint">{{ hint() }}</p>
      }
      <ng-content />
    </div>
  `,
  styleUrl: './load-states.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly hint = input<string>();
  readonly icon = input('inbox');
}
