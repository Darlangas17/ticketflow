import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, map, merge, of } from 'rxjs';

import { CatalogsApi } from '../../../core/api/catalogs.api';
import { createLoader } from '../../../core/api/loader';
import { TicketsApi } from '../../../core/api/tickets.api';
import { AuthService } from '../../../core/auth/auth.service';
import {
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  TicketListQuery,
  TicketSort,
  TicketSortField,
} from '../../../core/models/ticket.models';
import { EmptyState, ErrorState } from '../../../shared/components/load-states';
import { PriorityBadge, StatusBadge } from '../../../shared/components/ticket-badges';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../../shared/ticket-labels';
import { PAGE_SIZE_OPTIONS, hasFilters, parseTicketQuery, toQueryParams } from '../ticket-query';
import { Skeleton } from '../../../shared/components/skeleton';

/** Value of the assignee filter: any, unassigned, or a user id. */
type AssigneeFilter = '' | 'unassigned' | `${number}`;

const SORT_OPTIONS: { value: TicketSort; label: string }[] = [
  { value: '-createdAt', label: 'Newest first' },
  { value: 'createdAt', label: 'Oldest first' },
  { value: '-updatedAt', label: 'Recently updated' },
  { value: '-priority', label: 'Highest priority' },
  { value: 'priority', label: 'Lowest priority' },
  { value: 'status', label: 'Status (workflow order)' },
  { value: 'title', label: 'Title (A–Z)' },
];

@Component({
  selector: 'app-ticket-list',
  imports: [
    Skeleton,
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    StatusBadge,
    PriorityBadge,
    ErrorState,
    EmptyState,
  ],
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketList {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ticketsApi = inject(TicketsApi);
  private readonly catalogsApi = inject(CatalogsApi);
  private readonly auth = inject(AuthService);

  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;
  protected readonly sortOptions = SORT_OPTIONS;
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  protected readonly isStaff = this.auth.isStaff;
  protected readonly currentUserId = computed(() => this.auth.currentUser()?.id ?? null);
  protected readonly title = computed(() => (this.isStaff() ? 'Tickets' : 'My tickets'));

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 959.98px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  /** The current filters, read from the URL. */
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(parseTicketQuery)), {
    requireSync: true,
  });
  protected readonly filtered = computed(() => hasFilters(this.query()));

  /** Phones show only the search box until the user opens the other filters. */
  protected readonly showFilters = signal(false);
  protected readonly activeFilterCount = computed(() => {
    const q = this.query();
    return [q.status, q.priority, q.branchId, q.categoryId, q.assigneeId ?? q.unassigned].filter(
      (value) => value !== undefined,
    ).length;
  });

  protected readonly tickets = createLoader(this.query, (query) => this.ticketsApi.list(query));

  // Catalogs for the filter dropdowns. A failure only leaves that dropdown empty.
  protected readonly branches = toSignal(
    this.catalogsApi.branches().pipe(catchError(() => of([]))),
    {
      initialValue: [],
    },
  );
  protected readonly categories = toSignal(
    this.catalogsApi.categories().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  protected readonly assignees = toSignal(
    this.auth.isStaff() ? this.catalogsApi.assignees().pipe(catchError(() => of([]))) : of([]),
    { initialValue: [] },
  );

  protected readonly columns = computed(() =>
    this.isStaff()
      ? ['id', 'title', 'status', 'priority', 'branch', 'requester', 'assignee', 'createdAt']
      : ['id', 'title', 'status', 'priority', 'branch', 'assignee', 'createdAt'],
  );

  protected readonly sortActive = computed(
    () => this.query().sort?.replace('-', '') ?? 'createdAt',
  );
  protected readonly sortDirection = computed(() =>
    this.query().sort?.startsWith('-') ? 'desc' : 'asc',
  );

  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    status: [''],
    priority: [''],
    branchId: [''],
    categoryId: [''],
    assignee: ['' as AssigneeFilter],
  });

  constructor() {
    // Keep the form in sync with the URL, also when it changes from outside (back button, links).
    // The search box is only overwritten when its trimmed text differs, so typing is not disturbed.
    effect(() => {
      const target = this.formValueFrom(this.query());
      untracked(() => {
        const current = this.filters.getRawValue();
        const searchDiffers = current.search.trim() !== target.search;
        this.filters.patchValue(
          { ...target, search: searchDiffers ? target.search : current.search },
          { emitEvent: false },
        );
      });
    });

    // Typing in the search box waits for a pause; dropdowns apply immediately.
    this.filters.controls.search.valueChanges
      .pipe(
        debounceTime(350),
        map((value) => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.applyFilters());

    const controls = this.filters.controls;
    merge(
      controls.status.valueChanges,
      controls.priority.valueChanges,
      controls.branchId.valueChanges,
      controls.categoryId.valueChanges,
      controls.assignee.valueChanges,
    )
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.applyFilters());
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ ...this.query(), page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected onSort(sort: Sort): void {
    const field = (sort.active || 'createdAt') as TicketSortField;
    const value: TicketSort = sort.direction === 'desc' ? `-${field}` : field;
    this.navigate({ ...this.query(), sort: sort.direction ? value : undefined, page: 1 });
  }

  protected onSortSelect(sort: TicketSort): void {
    this.navigate({ ...this.query(), sort, page: 1 });
  }

  protected clearFilters(): void {
    this.filters.reset(
      { search: '', status: '', priority: '', branchId: '', categoryId: '', assignee: '' },
      { emitEvent: false },
    );
    this.navigate({ page: 1, pageSize: this.query().pageSize, sort: this.query().sort });
  }

  private applyFilters(): void {
    const value = this.filters.getRawValue();
    const assignee = value.assignee;
    this.navigate({
      page: 1,
      pageSize: this.query().pageSize,
      sort: this.query().sort,
      search: value.search.trim() || undefined,
      status: TICKET_STATUSES.find((s) => s === value.status),
      priority: TICKET_PRIORITIES.find((p) => p === value.priority),
      branchId: value.branchId ? Number(value.branchId) : undefined,
      categoryId: value.categoryId ? Number(value.categoryId) : undefined,
      unassigned: assignee === 'unassigned' || undefined,
      assigneeId: assignee && assignee !== 'unassigned' ? Number(assignee) : undefined,
    });
  }

  private navigate(query: TicketListQuery): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(query),
      replaceUrl: false,
    });
  }

  private formValueFrom(query: TicketListQuery) {
    return {
      search: query.search ?? '',
      status: query.status ?? '',
      priority: query.priority ?? '',
      branchId: query.branchId ? String(query.branchId) : '',
      categoryId: query.categoryId ? String(query.categoryId) : '',
      assignee: (query.unassigned
        ? 'unassigned'
        : query.assigneeId
          ? String(query.assigneeId)
          : '') as AssigneeFilter,
    };
  }
}
