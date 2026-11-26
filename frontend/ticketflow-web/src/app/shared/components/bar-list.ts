import { DecimalPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Params, RouterLink } from '@angular/router';

export interface BarItem {
  label: string;
  value: number;
  /** Query params for /tickets, so a bar opens the matching tickets. */
  queryParams?: Params;
}

/**
 * Horizontal bars in one hue (one series: magnitude per category). It is a real <table>, so
 * screen readers get the numbers as a table; every bar also shows its value as text, so
 * nothing depends on color or on hovering. Hover/focus shows the share of the total.
 */
@Component({
  selector: 'app-bar-list',
  imports: [RouterLink, MatTooltipModule, DecimalPipe, PercentPipe],
  template: `
    <table class="bars">
      <caption class="visually-hidden">
        {{
          caption()
        }}
      </caption>
      <thead class="visually-hidden">
        <tr>
          <th scope="col">{{ labelHeader() }}</th>
          <th scope="col">Share</th>
          <th scope="col">Tickets</th>
        </tr>
      </thead>
      <tbody>
        @for (row of rows(); track row.label; let i = $index) {
          <tr
            [matTooltip]="
              row.value + ' ' + unit(row.value) + ' · ' + (row.share | percent: '1.0-1')
            "
            matTooltipPosition="above"
          >
            <th scope="row" class="label">
              @if (row.queryParams) {
                <a routerLink="/tickets" [queryParams]="row.queryParams">{{ row.label }}</a>
              } @else {
                {{ row.label }}
              }
            </th>
            <td class="track-cell">
              <span class="visually-hidden">{{ row.share | percent: '1.0-1' }}</span>
              <div class="track" aria-hidden="true">
                <div class="bar" [style.width.%]="row.width" [style.--row]="i"></div>
              </div>
            </td>
            <td class="value">{{ row.value | number }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styleUrl: './bar-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BarList {
  readonly items = input.required<BarItem[]>();
  /** Accessible name of the table, e.g. "Tickets by status". */
  readonly caption = input.required<string>();
  readonly labelHeader = input('Category');

  protected readonly rows = computed(() => {
    const items = this.items();
    const total = items.reduce((sum, item) => sum + item.value, 0);
    const max = Math.max(...items.map((item) => item.value), 0);
    return items.map((item) => ({
      ...item,
      share: total === 0 ? 0 : item.value / total,
      // Scaled to the largest bar; zero stays empty, small values keep a visible sliver.
      width: max === 0 || item.value === 0 ? 0 : Math.max((item.value / max) * 100, 2),
    }));
  });

  protected unit(value: number): string {
    return value === 1 ? 'ticket' : 'tickets';
  }
}
