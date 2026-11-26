import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { EMPTY, catchError, finalize } from 'rxjs';

import { AdminApi } from '../../../core/api/admin.api';
import { toApiError } from '../../../core/api/api-error';
import { USER_ROLES, UserRole } from '../../../core/models/auth.models';
import { UserAccount } from '../../../core/models/user.models';
import { applyServerErrors, passwordStrength } from '../../../shared/forms/validators';

export interface UserDialogData {
  /** Absent when creating. */
  user?: UserAccount;
  /** The admin is editing their own account: role and status are locked. */
  isSelf: boolean;
}

export const ROLE_DESCRIPTIONS: Record<UserRole, string> = {
  Requester: 'Creates tickets and follows their own.',
  Technician: 'Works on assigned and unassigned tickets.',
  Admin: 'Sees everything and manages users and catalogs.',
};

@Component({
  selector: 'app-user-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
  ],
  templateUrl: './user-dialog.html',
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 4px;
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
      margin: 0 0 12px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserDialog {
  private readonly api = inject(AdminApi);
  private readonly dialogRef = inject<MatDialogRef<UserDialog, UserAccount>>(MatDialogRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly data = inject<UserDialogData>(MAT_DIALOG_DATA);

  protected readonly roles = USER_ROLES;
  protected readonly roleDescriptions = ROLE_DESCRIPTIONS;
  protected readonly isEdit = this.data.user !== undefined;
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: [this.data.user?.fullName ?? '', [Validators.required, Validators.maxLength(120)]],
    email: [
      this.data.user?.email ?? '',
      [Validators.required, Validators.email, Validators.maxLength(160)],
    ],
    role: [this.data.user?.role ?? ('Requester' as UserRole), Validators.required],
    isActive: [this.data.user?.isActive ?? true],
    // Required when creating; optional (only to reset it) when editing.
    password: ['', this.isEdit ? [passwordStrength] : [Validators.required, passwordStrength]],
  });

  constructor() {
    if (this.data.isSelf) {
      this.form.controls.role.disable();
      this.form.controls.isActive.disable();
    }
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const user = this.data.user;
    const request$ = user
      ? this.api.updateUser(user.id, {
          fullName: value.fullName.trim(),
          email: value.email.trim(),
          role: value.role,
          isActive: value.isActive,
          ...(value.password ? { password: value.password } : {}),
        })
      : this.api.createUser({
          fullName: value.fullName.trim(),
          email: value.email.trim(),
          password: value.password,
          role: value.role,
        });

    this.saving.set(true);
    this.errorMessage.set(null);
    request$
      .pipe(
        catchError((error: unknown) => {
          const apiError = toApiError(error);
          const unmatched = applyServerErrors(this.form, apiError.fieldErrors);
          const allShownOnFields =
            Object.keys(apiError.fieldErrors).length > 0 && unmatched.length === 0;
          this.errorMessage.set(allShownOnFields ? null : (unmatched[0] ?? apiError.message));
          return EMPTY;
        }),
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((saved) => this.dialogRef.close(saved));
  }
}
