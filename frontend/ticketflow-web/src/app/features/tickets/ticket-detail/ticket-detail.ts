import { TextFieldModule } from '@angular/cdk/text-field';
import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  numberAttribute,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, filter, finalize, of, switchMap } from 'rxjs';

import { toApiError } from '../../../core/api/api-error';
import { CatalogsApi } from '../../../core/api/catalogs.api';
import { createLoader } from '../../../core/api/loader';
import { TicketsApi } from '../../../core/api/tickets.api';
import { AuthService } from '../../../core/auth/auth.service';
import { User } from '../../../core/models/auth.models';
import { TicketDetails, TicketStatus } from '../../../core/models/ticket.models';
import { confirm } from '../../../shared/components/confirm-dialog';
import { ErrorState, Loading } from '../../../shared/components/load-states';
import { PriorityBadge, StatusBadge } from '../../../shared/components/ticket-badges';
import {
  STATUS_ICONS,
  transitionDoneMessage,
  transitionLabel,
} from '../../../shared/ticket-labels';
import {
  availableTransitions,
  canAssignTicket,
  canCommentTicket,
  canDeleteTicket,
  canEditTicket,
} from '../ticket-permissions';
import { describeHistory } from './history-text';

const COMMENT_MAX_LENGTH = 2000;

@Component({
  selector: 'app-ticket-detail',
  imports: [
    DatePipe,
    TextFieldModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatDividerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    StatusBadge,
    PriorityBadge,
    Loading,
    ErrorState,
  ],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetail {
  private readonly ticketsApi = inject(TicketsApi);
  private readonly catalogsApi = inject(CatalogsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  /** Route parameter :id, bound by the router. */
  readonly id = input.required({ transform: numberAttribute });

  protected readonly ticket = createLoader(this.id, (id) => this.ticketsApi.get(id));
  protected readonly user = this.auth.currentUser;
  protected readonly commentMaxLength = COMMENT_MAX_LENGTH;

  /** A status change, assignment or deletion is in progress. */
  protected readonly acting = signal(false);
  protected readonly sendingComment = signal(false);

  protected readonly canEdit = this.permission((user, t) => canEditTicket(user, t));
  protected readonly canAssign = this.permission((user, t) => canAssignTicket(user, t));
  protected readonly canComment = this.permission((_, t) => canCommentTicket(t));
  protected readonly canDelete = this.permission((user) => canDeleteTicket(user));
  protected readonly transitions = computed(() => {
    const user = this.user();
    const ticket = this.ticket.data();
    return user && ticket ? availableTransitions(user, ticket) : [];
  });

  protected readonly assignees = toSignal(
    this.auth.isStaff() ? this.catalogsApi.assignees().pipe(catchError(() => of([]))) : of([]),
    { initialValue: [] },
  );
  protected readonly assignedToMe = computed(
    () => this.ticket.data()?.assignee?.id === this.user()?.id,
  );

  protected readonly commentForm = inject(NonNullableFormBuilder).group({
    body: ['', [Validators.required, Validators.maxLength(COMMENT_MAX_LENGTH)]],
  });

  protected readonly describeHistory = describeHistory;
  protected readonly statusIcons = STATUS_ICONS;

  protected transitionText(to: TicketStatus): string {
    const from = this.ticket.data()?.status;
    return from ? transitionLabel(from, to) : to;
  }

  protected changeStatus(status: TicketStatus): void {
    const ticket = this.ticket.data();
    if (!ticket) {
      return;
    }

    // Closing is final, so it asks first.
    const proceed$: Observable<boolean> =
      status === 'Closed'
        ? confirm(this.dialog, {
            title: 'Close this ticket?',
            message: 'A closed ticket cannot be reopened, edited or commented on.',
            confirmText: 'Close ticket',
          })
        : of(true);

    proceed$
      .pipe(
        filter(Boolean),
        switchMap(() => this.run(this.ticketsApi.changeStatus(ticket.id, status))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((updated) => {
        this.ticket.set(updated);
        this.notify(transitionDoneMessage(ticket.status, status));
      });
  }

  protected assign(assigneeId: number | null): void {
    const ticket = this.ticket.data();
    if (!ticket || ticket.assignee?.id === assigneeId) {
      return;
    }
    this.run(this.ticketsApi.assign(ticket.id, assigneeId))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((updated) => {
        this.ticket.set(updated);
        this.notify(
          updated.assignee ? `Assigned to ${updated.assignee.fullName}` : 'Ticket unassigned',
        );
      });
  }

  protected assignToMe(): void {
    const me = this.user();
    if (me) {
      this.assign(me.id);
    }
  }

  protected addComment(): void {
    const ticket = this.ticket.data();
    const body = this.commentForm.controls.body.value.trim();
    if (!ticket || !body || this.commentForm.invalid || this.sendingComment()) {
      this.commentForm.markAllAsTouched();
      return;
    }

    this.sendingComment.set(true);
    this.ticketsApi
      .addComment(ticket.id, body)
      .pipe(
        finalize(() => this.sendingComment.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (comment) => {
          const current = this.ticket.data();
          if (current) {
            this.ticket.set({ ...current, comments: [...current.comments, comment] });
          }
          this.commentForm.reset();
          this.notify('Comment added');
        },
        error: (error: unknown) => this.notify(toApiError(error).message),
      });
  }

  protected deleteTicket(): void {
    const ticket = this.ticket.data();
    if (!ticket) {
      return;
    }
    confirm(this.dialog, {
      title: `Delete ticket #${ticket.id}?`,
      message: 'The ticket, its comments and its history will be permanently deleted.',
      confirmText: 'Delete',
      destructive: true,
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.run(this.ticketsApi.delete(ticket.id))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.notify(`Ticket #${ticket.id} deleted`);
        void this.router.navigate(['/tickets']);
      });
  }

  /** Runs an action with the buttons disabled; failures are shown and complete the stream. */
  private run<T>(action: Observable<T>): Observable<T> {
    this.acting.set(true);
    return action.pipe(
      catchError((error: unknown) => {
        this.notify(toApiError(error).message);
        if (toApiError(error).status === 400) {
          this.ticket.reload(); // e.g. someone else changed the status meanwhile
        }
        return of();
      }),
      finalize(() => this.acting.set(false)),
    );
  }

  private notify(message: string): void {
    this.snackBar.open(message, 'Dismiss', { duration: 4000 });
  }

  private permission(rule: (user: User, ticket: TicketDetails) => boolean) {
    return computed(() => {
      const user = this.user();
      const ticket = this.ticket.data();
      return user && ticket ? rule(user, ticket) : false;
    });
  }
}
