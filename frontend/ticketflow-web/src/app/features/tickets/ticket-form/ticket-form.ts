import { TextFieldModule } from '@angular/cdk/text-field';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  numberAttribute,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, catchError, finalize, forkJoin, of } from 'rxjs';

import { toApiError } from '../../../core/api/api-error';
import { CatalogsApi } from '../../../core/api/catalogs.api';
import { createLoader } from '../../../core/api/loader';
import { TicketsApi } from '../../../core/api/tickets.api';
import { AuthService } from '../../../core/auth/auth.service';
import { Branch, Category } from '../../../core/models/catalog.models';
import {
  TICKET_PRIORITIES,
  TicketDetails,
  TicketPriority,
} from '../../../core/models/ticket.models';
import { ErrorState, Loading } from '../../../shared/components/load-states';
import { applyServerErrors } from '../../../shared/forms/validators';
import { PRIORITY_LABELS } from '../../../shared/ticket-labels';
import { HasUnsavedChanges } from '../../../shared/unsaved-changes.guard';
import { canEditTicket } from '../ticket-permissions';

// Same limits as the API (TicketValidators).
export const TITLE_MAX_LENGTH = 150;
export const DESCRIPTION_MAX_LENGTH = 4000;

interface FormData {
  branches: Branch[];
  categories: Category[];
  /** Only when editing. */
  ticket: TicketDetails | null;
}

/** Create (/tickets/new) and edit (/tickets/:id/edit) a ticket. */
@Component({
  selector: 'app-ticket-form',
  imports: [
    TextFieldModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    Loading,
    ErrorState,
  ],
  templateUrl: './ticket-form.html',
  styleUrl: './ticket-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketForm implements HasUnsavedChanges {
  private readonly ticketsApi = inject(TicketsApi);
  private readonly catalogsApi = inject(CatalogsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  /** Route parameter :id; absent when creating. */
  readonly id = input<number | undefined, unknown>(undefined, {
    transform: (value: unknown) => (value == null ? undefined : numberAttribute(value)),
  });

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly priorityLabels = PRIORITY_LABELS;
  protected readonly titleMaxLength = TITLE_MAX_LENGTH;
  protected readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  private saved = false;

  protected readonly data = createLoader(this.id, (id): Observable<FormData> =>
    forkJoin({
      branches: this.catalogsApi.branches(),
      categories: this.catalogsApi.categories(),
      ticket: id === undefined ? of(null) : this.ticketsApi.get(id),
    }),
  );

  /** Editing a closed ticket (or one the user may not edit) is not allowed. */
  protected readonly readOnlyReason = computed(() => {
    const ticket = this.data.data()?.ticket;
    const user = this.auth.currentUser();
    if (!ticket || !user || canEditTicket(user, ticket)) {
      return null;
    }
    return ticket.status === 'Closed'
      ? 'This ticket is closed and can no longer be edited.'
      : 'You cannot edit this ticket.';
  });

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(TITLE_MAX_LENGTH)]],
    description: ['', [Validators.required, Validators.maxLength(DESCRIPTION_MAX_LENGTH)]],
    priority: ['Medium' as TicketPriority, Validators.required],
    categoryId: [null as number | null, Validators.required],
    branchId: [null as number | null, Validators.required],
  });

  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  protected readonly titleLength = computed(() => this.formValue().title?.length ?? 0);
  protected readonly descriptionLength = computed(() => this.formValue().description?.length ?? 0);

  constructor() {
    // Fill the form once the ticket (when editing) has loaded.
    effect(() => {
      const ticket = this.data.data()?.ticket;
      if (!ticket) {
        return;
      }
      untracked(() => {
        this.form.reset({
          title: ticket.title,
          description: ticket.description,
          priority: ticket.priority,
          categoryId: ticket.category.id,
          branchId: ticket.branch.id,
        });
        this.form.controls.branchId.disable(); // the branch cannot change after creation
      });
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved;
  }

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const fields = {
      title: value.title.trim(),
      description: value.description.trim(),
      priority: value.priority,
      categoryId: value.categoryId!,
    };
    const id = this.id();
    const request$ =
      id === undefined
        ? this.ticketsApi.create({ ...fields, branchId: value.branchId! })
        : this.ticketsApi.update(id, fields);

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
      .subscribe((ticket) => {
        this.saved = true;
        this.snackBar.open(
          id === undefined ? `Ticket #${ticket.id} created` : 'Changes saved',
          'Dismiss',
          { duration: 4000 },
        );
        void this.router.navigate(['/tickets', ticket.id]);
      });
  }

  protected cancelLink(): (string | number)[] {
    const id = this.id();
    return id === undefined ? ['/tickets'] : ['/tickets', id];
  }
}
