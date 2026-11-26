import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { navigationFor } from './navigation';

/** Layout of the signed-in area: toolbar, role-based side navigation and the page content. */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule,
    MatTooltipModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.currentUser;

  /** Phones and small tablets: the menu slides over the content instead of staying open. */
  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe([Breakpoints.XSmall, Breakpoints.Small])
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly sections = computed(() => {
    const role = this.auth.role();
    return role ? navigationFor(role) : [];
  });

  protected readonly initials = computed(() => {
    const name = this.user()?.fullName ?? '';
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part.charAt(0).toUpperCase())
      .join('');
  });

  private readonly mainContent = viewChild.required<ElementRef<HTMLElement>>('mainContent');

  /** Skip link (WCAG 2.4.1): moves keyboard focus past the navigation to the page content. */
  protected skipToContent(event: Event): void {
    event.preventDefault();
    this.mainContent().nativeElement.focus();
  }

  protected logout(): void {
    this.auth.logout();
  }
}
