import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs';

import { AdminApi } from '../../../core/api/admin.api';
import { createLoader } from '../../../core/api/loader';
import { AuthService } from '../../../core/auth/auth.service';
import { USER_ROLES, UserRole } from '../../../core/models/auth.models';
import { UserAccount, UserListQuery } from '../../../core/models/user.models';
import { EmptyState, ErrorState } from '../../../shared/components/load-states';
import { UserDialog, UserDialogData } from './user-dialog';
import { Skeleton } from '../../../shared/components/skeleton';

@Component({
  selector: 'app-users-page',
  imports: [
    Skeleton,
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    ErrorState,
    EmptyState,
  ],
  templateUrl: './users-page.html',
  styleUrl: '../admin-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPage {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly roles = USER_ROLES;
  protected readonly currentUserId = computed(() => this.auth.currentUser()?.id);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 719.98px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly columns = computed(() =>
    this.isHandset() ? ['name', 'actions'] : ['name', 'role', 'status', 'createdAt', 'actions'],
  );

  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly role = new FormControl<UserRole | ''>('', { nonNullable: true });
  protected readonly status = new FormControl<'' | 'active' | 'inactive'>('', {
    nonNullable: true,
  });

  protected readonly query = signal<UserListQuery>({ page: 1, pageSize: 20 });
  protected readonly users = createLoader(this.query, (query) => this.api.users(query));

  constructor() {
    this.search.valueChanges
      .pipe(
        debounceTime(350),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((search) =>
        this.query.update((q) => ({ ...q, page: 1, search: search || undefined })),
      );

    this.role.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((role) => this.query.update((q) => ({ ...q, page: 1, role: role || undefined })));

    this.status.valueChanges.pipe(takeUntilDestroyed()).subscribe((status) =>
      this.query.update((q) => ({
        ...q,
        page: 1,
        isActive: status === '' ? undefined : status === 'active',
      })),
    );
  }

  protected onPage(event: PageEvent): void {
    this.query.update((q) => ({ ...q, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected create(): void {
    this.openDialog({ isSelf: false }, (user) => `User ${user.fullName} created`);
  }

  protected edit(user: UserAccount): void {
    this.openDialog(
      { user, isSelf: user.id === this.currentUserId() },
      (saved) => `${saved.fullName} updated`,
    );
  }

  private openDialog(data: UserDialogData, message: (user: UserAccount) => string): void {
    this.dialog
      .open<UserDialog, UserDialogData, UserAccount>(UserDialog, {
        data,
        width: '480px',
        maxWidth: '95vw',
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .pipe(
        filter((saved): saved is UserAccount => typeof saved === 'object' && saved !== null),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((saved) => {
        this.snackBar.open(message(saved), 'Dismiss', { duration: 4000 });
        this.users.reload();
        if (saved.id === this.currentUserId()) {
          void this.auth.restoreSession(); // refresh the name shown in the toolbar
        }
      });
  }
}
