import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Left-hand panel of the sign-in and register pages (hidden on small screens). */
@Component({
  selector: 'app-auth-hero',
  imports: [MatIconModule],
  template: `
    <div class="brand">
      <span class="logo" aria-hidden="true"><mat-icon>support_agent</mat-icon></span>
      TicketFlow
    </div>
    <h2 class="hero-title">
      IT support that <span class="gradient-text">keeps every branch running.</span>
    </h2>
    <p class="hero-lead">
      Report a problem in seconds, follow it until it is fixed, and give your IT team one place to
      prioritize, assign and resolve tickets.
    </p>
    <ul class="features">
      @for (feature of features; track feature.title; let i = $index) {
        <li class="stagger" [style.--i]="i + 2">
          <span class="feature-icon" aria-hidden="true">
            <mat-icon>{{ feature.icon }}</mat-icon>
          </span>
          <span>
            <strong>{{ feature.title }}</strong>
            {{ feature.text }}
          </span>
        </li>
      }
    </ul>
  `,
  styleUrl: './auth-hero.scss',
  host: { class: 'hero' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthHero {
  protected readonly features = [
    {
      icon: 'bolt',
      title: 'Clear workflow',
      text: 'Open, in progress, on hold, resolved and closed, with full history.',
    },
    {
      icon: 'groups',
      title: 'Built for teams',
      text: 'Requesters, technicians and admins each see exactly what they need.',
    },
    {
      icon: 'insights',
      title: 'Live dashboard',
      text: 'Workload by status, priority, branch and category at a glance.',
    },
  ];
}
