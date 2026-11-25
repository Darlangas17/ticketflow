import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';

import { AuthService } from '../../core/auth/auth.service';

/**
 * Temporary page for sections built in later phases. `title`, `icon` and `description`
 * come from the route's `data` (bound to inputs by withComponentInputBinding).
 */
@Component({
  selector: 'app-placeholder-page',
  imports: [MatCardModule, MatIconModule],
  template: `
    <section class="page">
      <h1 class="page-title">{{ title() }}</h1>
      <mat-card appearance="outlined">
        <mat-card-content class="body">
          <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
          <div>
            @if (user(); as user) {
              <p class="greeting">Signed in as {{ user.fullName }} ({{ user.role }}).</p>
            }
            <p>{{ description() }}</p>
          </div>
        </mat-card-content>
      </mat-card>
    </section>
  `,
  styles: `
    .body {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      padding-top: 16px;
    }
    .icon {
      flex-shrink: 0;
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--mat-sys-primary);
    }
    .greeting {
      font: var(--mat-sys-title-medium);
      margin-top: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlaceholderPage {
  readonly title = input('');
  readonly icon = input('construction');
  readonly description = input('This section is under construction.');

  protected readonly user = inject(AuthService).currentUser;
}
