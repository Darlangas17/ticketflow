import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** Full-page message used for 403 and 404. Texts come from the route's `data`. */
@Component({
  selector: 'app-message-page',
  imports: [MatIconModule, MatButtonModule, RouterLink],
  template: `
    <section class="message" role="alert">
      <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
      <h1>{{ title() }}</h1>
      <p>{{ description() }}</p>
      <a mat-flat-button routerLink="/dashboard">Go to the dashboard</a>
    </section>
  `,
  styles: `
    .message {
      min-height: 60vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      text-align: center;
      padding: 24px;
    }
    .icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      color: var(--mat-sys-on-surface-variant);
    }
    h1 {
      font: var(--mat-sys-headline-medium);
      margin: 16px 0 8px;
    }
    p {
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 24px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MessagePage {
  readonly title = input('');
  readonly description = input('');
  readonly icon = input('info');
}
