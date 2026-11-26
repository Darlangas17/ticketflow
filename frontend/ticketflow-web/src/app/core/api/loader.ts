import { Signal, computed, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Observable, catchError, combineLatest, of, switchMap, tap } from 'rxjs';

import { toApiError } from './api-error';

/** Loading, data and error of a request, as signals, plus a way to repeat it. */
export interface Loader<T> {
  readonly loading: Signal<boolean>;
  /** Last successful result; kept while a new request is loading (e.g. changing page). */
  readonly data: Signal<T | undefined>;
  readonly error: Signal<string | null>;
  /** HTTP status of the last failure (404 → "not found" screens). */
  readonly errorStatus: Signal<number | null>;
  readonly hasData: Signal<boolean>;
  reload(): void;
  /** Replaces the data locally, e.g. with the response of an update. */
  set(value: T): void;
}

/**
 * Runs `fetch` every time `params` changes (cancelling the previous request) and exposes the
 * result as signals. Must be called in an injection context (field initializer or constructor).
 */
export function createLoader<P, T>(
  params: Signal<P>,
  fetch: (params: P) => Observable<T>,
): Loader<T> {
  const loading = signal(true);
  const data = signal<T | undefined>(undefined);
  const error = signal<string | null>(null);
  const errorStatus = signal<number | null>(null);
  const reloads = signal(0);

  combineLatest([toObservable(params), toObservable(reloads)])
    .pipe(
      tap(() => {
        loading.set(true);
        error.set(null);
        errorStatus.set(null);
      }),
      switchMap(([value]) =>
        fetch(value).pipe(
          tap((result) => data.set(result)),
          catchError((failure: unknown) => {
            const apiError = toApiError(failure);
            error.set(apiError.message);
            errorStatus.set(apiError.status);
            return of(undefined);
          }),
        ),
      ),
      takeUntilDestroyed(),
    )
    .subscribe(() => loading.set(false));

  return {
    loading: loading.asReadonly(),
    data: data.asReadonly(),
    error: error.asReadonly(),
    errorStatus: errorStatus.asReadonly(),
    hasData: computed(() => data() !== undefined),
    reload: () => reloads.update((n) => n + 1),
    set: (value: T) => data.set(value),
  };
}
