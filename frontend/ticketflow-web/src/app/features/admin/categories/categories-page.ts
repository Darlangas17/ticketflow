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
import { Category } from '../../../core/models/catalog.models';
import { confirm } from '../../../shared/components/confirm-dialog';
import { EmptyState, ErrorState } from '../../../shared/components/load-states';
import { CatalogDialog, CatalogDialogData, CatalogFormValue } from '../catalog-dialog';
import { Skeleton } from '../../../shared/components/skeleton';

@Component({
  selector: 'app-categories-page',
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
        <h1 class="page-title">Categories</h1>
        <button mat-flat-button type="button" (click)="create()">
          <mat-icon>add</mat-icon>
          New category
        </button>
      </header>

      <mat-card appearance="outlined" class="results animate-in">
        @if (busy() || (categories.loading() && categories.hasData())) {
          <mat-progress-bar mode="indeterminate" aria-label="Saving" />
        }
        @if (categories.error(); as error) {
          <app-error-state [message]="error" (retry)="categories.reload()" />
        } @else if (categories.data(); as list) {
          @if (list.length === 0) {
            <app-empty-state icon="category" title="No categories yet" />
          } @else {
            <div class="table-wrapper">
              <table mat-table [dataSource]="list" aria-label="Categories">
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Category</th>
                  <td mat-cell *matCellDef="let category">
                    <span class="primary-text">{{ category.name }}</span>
                    <span class="secondary-text">{{
                      category.description ?? 'No description'
                    }}</span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef>
                    <span class="visually-hidden">Actions</span>
                  </th>
                  <td mat-cell *matCellDef="let category" class="actions-cell">
                    <button
                      mat-icon-button
                      type="button"
                      [disabled]="busy()"
                      [attr.aria-label]="'Edit ' + category.name"
                      matTooltip="Edit"
                      (click)="edit(category)"
                    >
                      <mat-icon>edit</mat-icon>
                    </button>
                    <button
                      mat-icon-button
                      type="button"
                      [disabled]="busy()"
                      [attr.aria-label]="'Delete ' + category.name"
                      matTooltip="Delete"
                      (click)="remove(category)"
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
                ></tr>
              </table>
            </div>
          }
        } @else {
          <app-skeleton [rows]="5" message="Loading categories…" />
        }
      </mat-card>
      <p class="footnote">Categories that are used by tickets cannot be deleted.</p>
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
export class CategoriesPage {
  private readonly adminApi = inject(AdminApi);
  private readonly catalogsApi = inject(CatalogsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly columns = ['name', 'actions'];
  protected readonly busy = signal(false);
  protected readonly categories = createLoader(signal(null), () => this.catalogsApi.categories());

  protected create(): void {
    this.open('New category', { name: '', detail: '', isActive: true }, (value) =>
      this.adminApi.createCategory(value.name, value.detail || null),
    );
  }

  protected edit(category: Category): void {
    this.open(
      'Edit category',
      { name: category.name, detail: category.description ?? '', isActive: true },
      (value) => this.adminApi.updateCategory(category.id, value.name, value.detail || null),
    );
  }

  protected remove(category: Category): void {
    confirm(this.dialog, {
      title: `Delete ${category.name}?`,
      message: 'This cannot be undone. Categories used by tickets cannot be deleted.',
      confirmText: 'Delete',
      destructive: true,
    })
      .pipe(filter(Boolean), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.busy.set(true);
        this.adminApi
          .deleteCategory(category.id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.busy.set(false);
              this.snackBar.open(`${category.name} deleted`, 'Dismiss', { duration: 4000 });
              this.categories.reload();
            },
            error: (error: unknown) => {
              this.busy.set(false);
              this.snackBar.open(toApiError(error).message, 'Dismiss', { duration: 6000 });
            },
          });
      });
  }

  private open(
    title: string,
    value: CatalogFormValue,
    save: (value: CatalogFormValue) => Observable<Category>,
  ): void {
    this.dialog
      .open<CatalogDialog<Category>, CatalogDialogData<Category>, Category>(CatalogDialog, {
        data: {
          title,
          value,
          detailLabel: 'Description (optional)',
          detailRequired: false,
          detailMaxLength: 500,
          detailField: 'Description',
          showActive: false,
          save,
        },
        width: '440px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .pipe(
        filter((saved): saved is Category => typeof saved === 'object' && saved !== null),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((saved) => {
        this.snackBar.open(`${saved.name} saved`, 'Dismiss', { duration: 4000 });
        this.categories.reload();
      });
  }
}
