import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { toApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import {
  applyServerErrors,
  matchesField,
  passwordStrength,
} from '../../../shared/forms/validators';

/** Self-registration: always creates a Requester account (the API ignores any other role). */
@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './register.html',
  styleUrl: '../auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hidePassword = signal(true);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(160)]],
    password: ['', [Validators.required, passwordStrength]],
    confirmPassword: ['', [Validators.required, matchesField('password')]],
  });

  constructor() {
    // Re-check the confirmation when the password changes after it was typed.
    this.form.controls.password.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() =>
        this.form.controls.confirmPassword.updateValueAndValidity({ emitEvent: false }),
      );
  }

  protected submit(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    const { fullName, email, password } = this.form.getRawValue();
    this.loading.set(true);
    this.errorMessage.set(null);
    this.auth
      .register({ fullName, email, password })
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl('/dashboard'),
        error: (error: unknown) => {
          const apiError = toApiError(error);
          // Field errors appear under their inputs; anything else (e.g. 409 e-mail taken) on top.
          const unmatched = applyServerErrors(this.form, apiError.fieldErrors);
          const allShownOnFields =
            Object.keys(apiError.fieldErrors).length > 0 && unmatched.length === 0;
          this.errorMessage.set(allShownOnFields ? null : (unmatched[0] ?? apiError.message));
        },
      });
  }
}
