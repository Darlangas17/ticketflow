import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { EMPTY, Observable, catchError, finalize } from 'rxjs';

import { showApiError } from '../../shared/forms/validators';

export interface CatalogFormValue {
  name: string;
  /** Branch city or category description. */
  detail: string;
  isActive: boolean;
}

export interface CatalogDialogData<T> {
  title: string;
  value: CatalogFormValue;
  detailLabel: string;
  detailRequired: boolean;
  detailMaxLength: number;
  /** Shows the "active" switch (branches only, when editing). */
  showActive: boolean;
  /** Name of the API field behind "detail" ("City", "Description"), to map server errors. */
  detailField: string;
  save: (value: CatalogFormValue) => Observable<T>;
}

/** Create/edit dialog shared by branches and categories. It saves itself, so errors show inline. */
@Component({
  selector: 'app-catalog-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (errorMessage(); as message) {
          <p class="notice-error" role="alert">{{ message }}</p>
        }
        <mat-form-field class="full-width">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="100" autocomplete="off" />
          @if (form.controls.name.hasError('required')) {
            <mat-error>Name is required.</mat-error>
          } @else if (form.controls.name.hasError('server')) {
            <mat-error>{{ form.controls.name.getError('server') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>{{ data.detailLabel }}</mat-label>
          <input
            matInput
            formControlName="detail"
            [maxlength]="data.detailMaxLength"
            autocomplete="off"
          />
          @if (form.controls.detail.hasError('required')) {
            <mat-error>{{ data.detailLabel }} is required.</mat-error>
          } @else if (form.controls.detail.hasError('server')) {
            <mat-error>{{ form.controls.detail.getError('server') }}</mat-error>
          }
        </mat-form-field>
        @if (data.showActive) {
          <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
          <p class="hint">Inactive branches keep their tickets but cannot receive new ones.</p>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" [mat-dialog-close]="undefined">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">
          {{ saving() ? 'Saving…' : 'Save' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    form {
      padding-top: 8px;
    }
    .notice-error {
      margin: 0 0 12px;
      padding: 12px 16px;
      border-radius: 8px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .hint {
      margin: 4px 0 0;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogDialog<T> {
  protected readonly data = inject<CatalogDialogData<T>>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<CatalogDialog<T>, T>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.value.name, [Validators.required, Validators.maxLength(100)]],
    detail: [
      this.data.value.detail,
      this.data.detailRequired
        ? [Validators.required, Validators.maxLength(this.data.detailMaxLength)]
        : [Validators.maxLength(this.data.detailMaxLength)],
    ],
    isActive: [this.data.value.isActive],
  });

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.errorMessage.set(null);
    this.data
      .save({ name: value.name.trim(), detail: value.detail.trim(), isActive: value.isActive })
      .pipe(
        catchError((error: unknown) => {
          this.errorMessage.set(
            showApiError(this.form, error, { [this.data.detailField]: 'Detail' }),
          );
          return EMPTY;
        }),
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((saved) => this.dialogRef.close(saved));
  }
}
