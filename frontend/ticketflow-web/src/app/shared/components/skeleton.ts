import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type SkeletonVariant = 'table' | 'cards' | 'dashboard' | 'detail';

/**
 * Placeholder that previews the shape of the content while it loads (feels faster than a
 * spinner). Screen readers hear the message through a live region; the shapes are hidden from them.
 */
@Component({
  selector: 'app-skeleton',
  template: `
    <div class="skeleton" role="status" aria-live="polite" [attr.aria-busy]="true">
      <span class="visually-hidden">{{ message() }}</span>
      <div aria-hidden="true">
        @switch (variant()) {
          @case ('dashboard') {
            <div class="kpis">
              @for (i of tiles; track i) {
                <div class="tile">
                  <span class="line short"></span>
                  <span class="line big"></span>
                  <span class="line"></span>
                </div>
              }
            </div>
            <div class="charts">
              @for (i of tiles; track i) {
                <div class="tile chart">
                  <span class="line short"></span>
                  @for (bar of bars; track bar) {
                    <span class="bar" [style.width.%]="bar"></span>
                  }
                </div>
              }
            </div>
          }
          @case ('detail') {
            <span class="line short"></span>
            <span class="line title"></span>
            <div class="detail">
              <div class="tile tall"></div>
              <div class="tile tall"></div>
            </div>
          }
          @case ('cards') {
            @for (row of rowList(); track row) {
              <div class="tile card">
                <span class="line short"></span>
                <span class="line"></span>
                <span class="line medium"></span>
              </div>
            }
          }
          @default {
            @for (row of rowList(); track row) {
              <div class="row">
                <span class="cell narrow"></span>
                <span class="cell wide"></span>
                <span class="cell"></span>
                <span class="cell"></span>
                <span class="cell"></span>
              </div>
            }
          }
        }
      </div>
    </div>
  `,
  styles: `
    .skeleton {
      padding: 16px;
    }
    .line,
    .cell,
    .bar,
    .tile {
      display: block;
      border-radius: 8px;
      background: linear-gradient(
        90deg,
        var(--mat-sys-surface-container-high) 25%,
        var(--mat-sys-surface-container-highest) 37%,
        var(--mat-sys-surface-container-high) 63%
      );
      background-size: 200% 100%;
      animation: tf-shimmer 1.4s ease-in-out infinite;
    }
    .line {
      height: 12px;
      margin: 10px 0;
      width: 70%;
    }
    .line.short {
      width: 35%;
    }
    .line.medium {
      width: 55%;
    }
    .line.big {
      height: 36px;
      width: 45%;
    }
    .line.title {
      height: 28px;
      width: 60%;
      margin-bottom: 20px;
    }
    .row {
      display: grid;
      grid-template-columns: 0.4fr 2.5fr 1fr 1fr 1fr;
      gap: 16px;
      padding: 14px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .cell {
      height: 14px;
    }
    .tile {
      background: var(--mat-sys-surface-container-low);
      animation: none;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--tf-radius);
      padding: 16px;
    }
    .tile .line,
    .tile .bar {
      animation: tf-shimmer 1.4s ease-in-out infinite;
    }
    .kpis,
    .charts {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 16px;
    }
    .charts {
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    }
    .bar {
      height: 16px;
      margin: 12px 0;
    }
    .card {
      margin-bottom: 8px;
    }
    .detail {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 16px;
    }
    .tall {
      height: 260px;
    }
    @media (max-width: 599.98px) {
      .row {
        grid-template-columns: 1fr 3fr;
      }
      .row .cell:nth-child(n + 3) {
        display: none;
      }
      .detail {
        grid-template-columns: 1fr;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Skeleton {
  readonly variant = input<SkeletonVariant>('table');
  readonly rows = input(6);
  readonly message = input('Loading…');

  protected readonly tiles = [1, 2, 3, 4];
  protected readonly bars = [92, 70, 55, 80];
  protected readonly rowList = computed(() => Array.from({ length: this.rows() }, (_, i) => i));
}
