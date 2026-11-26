import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';

import { DashboardApi } from '../../core/api/dashboard.api';
import { createLoader } from '../../core/api/loader';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardSummary } from '../../core/models/dashboard.models';
import { BarItem, BarList } from '../../shared/components/bar-list';
import { ErrorState, Loading } from '../../shared/components/load-states';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../shared/ticket-labels';

/** "37.3 h" below two days, "2.4 days" above; "—" when nothing was resolved. */
export function formatDuration(hours: number | null): string {
  if (hours === null) {
    return '—';
  }
  if (hours < 48) {
    return `${hours.toFixed(1)} h`;
  }
  return `${(hours / 24).toFixed(1)} days`;
}

/** The bar data for each breakdown; each bar links to the matching tickets. */
export function toCharts(summary: DashboardSummary) {
  return {
    status: summary.byStatus.map<BarItem>((s) => ({
      label: STATUS_LABELS[s.status],
      value: s.count,
      queryParams: { status: s.status },
    })),
    priority: [...summary.byPriority].reverse().map<BarItem>((p) => ({
      label: PRIORITY_LABELS[p.priority],
      value: p.count,
      queryParams: { priority: p.priority },
    })),
    branch: summary.byBranch.map<BarItem>((b) => ({
      label: b.name,
      value: b.count,
      queryParams: { branchId: b.id },
    })),
    category: summary.byCategory.map<BarItem>((c) => ({
      label: c.name,
      value: c.count,
      queryParams: { categoryId: c.id },
    })),
  };
}

@Component({
  selector: 'app-dashboard',
  imports: [
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    BarList,
    Loading,
    ErrorState,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard {
  private readonly auth = inject(AuthService);
  private readonly api = inject(DashboardApi);

  protected readonly user = this.auth.currentUser;
  protected readonly summary = createLoader(signal(null), () => this.api.summary());
  protected readonly charts = computed(() => {
    const data = this.summary.data();
    return data ? toCharts(data) : null;
  });
  protected readonly averageResolution = computed(() =>
    formatDuration(this.summary.data()?.averageResolutionHours ?? null),
  );

  /** Admins see company-wide numbers; everybody else, the tickets they can see. */
  protected readonly scope = computed(() => {
    switch (this.auth.role()) {
      case 'Admin':
        return 'All tickets in the company.';
      case 'Technician':
        return 'Tickets assigned to you and unassigned tickets.';
      default:
        return 'Your tickets.';
    }
  });
}
