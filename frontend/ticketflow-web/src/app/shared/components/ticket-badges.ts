import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { TicketPriority, TicketStatus } from '../../core/models/ticket.models';
import { PRIORITY_LABELS, STATUS_ICONS, STATUS_LABELS } from '../ticket-labels';

// Badges always show text (and an icon for status), so the meaning never depends on color alone
// (WCAG 1.4.1). Colors are pairs of Material system tokens with sufficient contrast.

@Component({
  selector: 'app-status-badge',
  imports: [MatIconModule],
  template: `
    <span class="badge" [attr.data-status]="status()">
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>{{ label() }}
    </span>
  `,
  styleUrl: './ticket-badges.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<TicketStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly icon = computed(() => STATUS_ICONS[this.status()]);
}

@Component({
  selector: 'app-priority-badge',
  template: `<span class="badge" [attr.data-priority]="priority()">{{ label() }}</span>`,
  styleUrl: './ticket-badges.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriorityBadge {
  readonly priority = input.required<TicketPriority>();
  protected readonly label = computed(() => PRIORITY_LABELS[this.priority()]);
}
