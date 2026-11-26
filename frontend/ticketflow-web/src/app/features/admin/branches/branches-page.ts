import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, filter } from 'rxjs';

import { AdminApi } from '../../../core/api/admin.api';
import { toApiError } from '../../../core/api/api-error';
import { CatalogsApi } from '../../../core/api/catalogs.api';
import { createLoader } from '../../../core/api/loader';
import { Branch } from '../../../core/models/catalog.models';
import { confirm } from '../../../shared/components/confirm-dialog';
import { EmptyState, ErrorState } from '../../../shared/components/load-states';
import { CatalogDialog, CatalogDialogData, CatalogFormValue } from '../catalog-dialog';
import { Skeleton } from '../../../shared/components/skeleton';

@Component({
  selector: 'app-branches-page',
  imports: [
    Skeleton,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    ErrorState,
    EmptyState,
  ],
  template: `
    <section class="page">
      <header class="header animate-in">
        <h1 class="page-title">Branches</h1>
        <button mat-flat-button type="button" (click)="create()">
          <mat-icon>add_business</mat-icon>
          New branch
        </button>
      </header>

      <mat-card appearance="outlined" class="results animate-in">
        @if (busy() || (branches.loading() && branches.hasData())) {
          <mat-progress-bar mode="indeterminate" aria-label="Saving" />
        }
        @if (branches.error(); as error) {
          <app-error-state [message]="error" (retry)="branches.reload()" />
        } @else if (branches.data(); as list) {
          @if (list.length === 0) {
            <app-empty-state icon="store" title="No branches yet" />
          } @else {
            <div class="table-wrapper">
              <table mat-table [dataSource]="list" aria-label="Branches">
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Branch</th>
                  <td mat-cell *matCellDef="let branch">
                    <span class="primary-text">{{ branch.name }}</span>
                    <span class="secondary-text">{{ branch.city }}</span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="status">
                  <th mat-header-cell *matHeaderCellDef>Status</th>
                  <td mat-cell *matCellDef="let branch">
                    <span class="chip" [class.inactive]="!branch.isActive">
                      {{ branch.isActive ? 'Active' : 'Inactive' }}
                    </span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef>
                    <span class="visually-hidden">Actions</span>
                  </th>
                  <td mat-cell *matCellDef="let branch" class="actions-cell">
                    <button
                      mat-icon-button
                      type="button"
                      [disabled]="busy()"
                      [attr.aria-label]="
                        (branch.isActive ? 'Deactivate ' : 'Activate ') + branch.name
                      "
                      [matTooltip]="branch.isActive ? 'Deactivate' : 'Activate'"
                      (click)="toggleActive(branch)"
                    >
                      <mat-icon>{{ branch.isActive ? 'toggle_on' : 'toggle_off' }}</mat-icon>
                    </button>
                    <button
                      mat-icon-button
                      type="button"
                      [disabled]="busy()"
                      [attr.aria-label]="'Edit ' + branch.name"
                      matTooltip="Edit"
                      (click)="edit(branch)"
                    >
                      <mat-icon>edit</mat-icon>
                    </button>
                    <button
                      mat-icon-button
                      type="button"
                      [disabled]="busy()"
                      [attr.aria-label]="'Delete ' + branch.name"
                      matTooltip="Delete"
                      (click)="remove(branch)"
                    >
                      <mat-icon>delete</mat-icon>
                    </button>
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr
                  mat-row
                  *matRowDef="let row; columns: columns; let i = index"
                  class="stagger"
                  [style.--i]="i"
                  [class.dimmed]="!row.isActive"
                ></tr>
              </table>
            </div>
          }
        } @else {
          <app-skeleton [rows]="3" message="Loading branches…" />
        }
      </mat-card>
      <p class="footnote">
        Branches with tickets cannot be deleted; deactivate them instead so their history is kept.
      </p>
    </section>
  `,
  styleUrl: '../admin-page.scss',
  styles: `
    .footnote {
      margin: 12px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BranchesPage {
  private readonly adminApi = inject(AdminApi);
  private readonly catalogsApi = inject(CatalogsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns = ['name', 'status', 'actions'];
  protected readonly busy = signal(false);
  protected readonly branches = createLoader(signal(null), () => this.catalogsApi.branches(true));

  protected create(): void {
    this.open('New branch', { name: '', detail: '', isActive: true }, false, (value) =>
      this.adminApi.createBranch(value.name, value.detail),
    );
  }

  protected edit(branch: Branch): void {
    this.open(
      'Edit branch',
      { name: branch.name, detail: branch.city, isActive: branch.isActive },
      true,
      (value) =>
        this.adminApi.updateBranch(branch.id, {
          name: value.name,
          city: value.detail,
          isActive: value.isActive,
        }),
    );
  }

  protected toggleActive(branch: Branch): void {
    this.run(
      this.adminApi.updateBranch(branch.id, { ...branch, isActive: !branch.isActive }),
      `${branch.name} ${branch.isActive ? 'deactivated' : 'activated'}`,
    );
  }

  protected remove(branch: Branch): void {
    confirm(this.dialog, {
      title: `Delete ${branch.name}?`,
      message: 'This cannot be undone. Branches that have tickets cannot be deleted.',
      confirmText: 'Delete',
      destructive: true,
    })
      .pipe(filter(Boolean), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.run(this.adminApi.deleteBranch(branch.id), `${branch.name} deleted`));
  }

  private open(
    title: string,
    value: CatalogFormValue,
    showActive: boolean,
    save: (value: CatalogFormValue) => Observable<Branch>,
  ): void {
    this.dialog
      .open<CatalogDialog<Branch>, CatalogDialogData<Branch>, Branch>(CatalogDialog, {
        data: {
          title,
          value,
          detailLabel: 'City',
          detailRequired: true,
          detailMaxLength: 100,
          detailField: 'City',
          showActive,
          save,
        },
        width: '440px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .pipe(
        filter((saved): saved is Branch => typeof saved === 'object' && saved !== null),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((saved) => {
        this.snackBar.open(`${saved.name} saved`, 'Dismiss', { duration: 4000 });
        this.branches.reload();
      });
  }

  private run(action: Observable<unknown>, success: string): void {
    this.busy.set(true);
    action.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.busy.set(false);
        this.snackBar.open(success, 'Dismiss', { duration: 4000 });
        this.branches.reload();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.snackBar.open(toApiError(error).message, 'Dismiss', { duration: 6000 });
      },
    });
  }
}
