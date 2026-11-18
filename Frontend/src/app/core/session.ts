import { inject, Injectable, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, tap } from 'rxjs';
import { Api, errorMessage } from './api';
import { Trader } from './models';

@Injectable({ providedIn: 'root' })
export class Session {
  private api = inject(Api);
  private router = inject(Router);
  readonly trader = signal<Trader | null>(null);
  readonly error = signal('');
  readonly storageNote = signal('');
  readonly key = 'burse.traderId';
  savedId(): string {
    try {
      return localStorage.getItem(this.key) ?? '';
    } catch {
      this.storageNote.set(
        'Browser storage is unavailable. Your trader ID will only be remembered for this visit.',
      );
      return '';
    }
  }
  identify(id: number) {
    return this.api.traders().pipe(
      map((traders) => {
        const trader = traders.find((t) => t.id === id && t.id > 0);
        if (!trader) throw new HttpErrorResponse({ status: 404 });
        return trader;
      }),
      tap((trader) => {
        this.trader.set(trader);
        this.error.set('');
        try {
          localStorage.setItem(this.key, String(trader.id));
        } catch {
          this.storageNote.set(
            'Browser storage is unavailable. Your trader ID will only be remembered for this visit.',
          );
        }
      }),
    );
  }
  clear() {
    this.trader.set(null);
    try {
      localStorage.removeItem(this.key);
    } catch {
      /* Storage may be disabled by the browser. */
    }
  }
  signOut() {
    this.clear();
    void this.router.navigateByUrl('/sign-in');
  }
}

export const identified: CanActivateFn = () => {
  const session = inject(Session);
  const router = inject(Router);
  if (session.trader()) return true;
  const saved = session.savedId();
  if (
    !/^[1-9]\d*$/.test(saved) ||
    !Number.isSafeInteger(Number(saved)) ||
    Number(saved) > 2147483647
  ) {
    session.clear();
    return router.parseUrl('/sign-in');
  }
  return session.identify(Number(saved)).pipe(
    map(() => true),
    catchError((error) => {
      if (error instanceof HttpErrorResponse && error.status === 404) session.clear();
      session.error.set(
        error instanceof HttpErrorResponse && error.status === 404
          ? 'Your saved trader ID no longer exists. Enter another trader ID to continue.'
          : errorMessage(error),
      );
      return of(router.parseUrl('/sign-in'));
    }),
  );
};
