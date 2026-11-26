import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
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
import { safeReturnUrl } from '../../../core/auth/auth.guards';
import { AuthService } from '../../../core/auth/auth.service';
import { ThemeToggle } from '../../../shared/components/theme-toggle';
import { AuthHero } from '../auth-hero';
import { UserRole } from '../../../core/models/auth.models';

interface DemoAccount {
  role: UserRole;
  name: string;
  email: string;
}

/** Fictional users created by the API's demo seed (see the README). */
export const DEMO_ACCOUNTS: readonly DemoAccount[] = [
  { role: 'Admin', name: 'Laura Mejia', email: 'admin@ticketflow.demo' },
  { role: 'Technician', name: 'Carlos Rivera', email: 'carlos.rivera@ticketflow.demo' },
  { role: 'Requester', name: 'Maria Lopez', email: 'maria.lopez@ticketflow.demo' },
];
export const DEMO_PASSWORD = 'Demo123!';

@Component({
  selector: 'app-login',
  imports: [
    AuthHero,
    ThemeToggle,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.html',
  styleUrl: '../auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Query parameters, bound by the router (withComponentInputBinding). */
  readonly returnUrl = input<string>();
  readonly reason = input<string>();

  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly hidePassword = signal(true);
  protected readonly sessionExpired = computed(() => this.reason() === 'expired');

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected useDemoAccount(account: DemoAccount): void {
    this.form.setValue({ email: account.email, password: DEMO_PASSWORD });
    this.errorMessage.set(null);
  }

  protected submit(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.auth
      .login(this.form.getRawValue())
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl(safeReturnUrl(this.returnUrl())),
        error: (error: unknown) => this.errorMessage.set(toApiError(error).message),
      });
  }
}
