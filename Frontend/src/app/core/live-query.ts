import { DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  EMPTY,
  Observable,
  Subject,
  catchError,
  filter,
  finalize,
  fromEvent,
  merge,
  of,
  switchMap,
  tap,
  timer,
} from 'rxjs';
import { errorMessage } from './api';

export function liveQuery<T>(load: () => Observable<T>, changes: Observable<unknown> = of(null)) {
  const destroyRef = inject(DestroyRef);
  const data = signal<T | null>(null);
  const error = signal('');
  const loading = signal(true);
  const updated = signal<Date | null>(null);
  const manual = new Subject<void>();
  changes
    .pipe(
      switchMap(() => {
        data.set(null);
        updated.set(null);
        return merge(
          timer(0, 5000).pipe(filter((tick) => !document.hidden && (tick === 0 || !loading()))),
          fromEvent(document, 'visibilitychange').pipe(filter(() => !document.hidden)),
          manual,
        ).pipe(
          switchMap(() => {
            loading.set(true);
            return load().pipe(
              tap((value) => {
                data.set(value);
                error.set('');
                updated.set(new Date());
              }),
              catchError((reason) => {
                error.set(errorMessage(reason));
                return EMPTY;
              }),
              finalize(() => loading.set(false)),
            );
          }),
        );
      }),
      takeUntilDestroyed(destroyRef),
    )
    .subscribe();
  return { data, error, loading, updated, refresh: () => manual.next() };
}
