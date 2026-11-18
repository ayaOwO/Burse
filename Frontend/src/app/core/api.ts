import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Account, CreateOrder, Performance, Stock, StockSummary, Trader } from './models';
import { map, tap } from 'rxjs';
import { PriceBaseline } from './price-baseline';

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  readonly prices = new PriceBaseline();
  stocks = () =>
    this.http
      .get<Stock[]>('/api/stocks', { timeout: 12000 })
      .pipe(tap((stocks) => this.prices.observe(stocks)));
  stock = (id: number) =>
    this.http
      .get<StockSummary>(`/api/stocks/${id}`, { timeout: 12000 })
      .pipe(tap((details) => this.prices.observe([details.stock])));
  traders = () =>
    this.http
      .get<Trader[]>('/api/traders', { timeout: 12000 })
      .pipe(map((traders) => traders.filter((trader) => trader.id > 0)));
  account = (id: number) => this.http.get<Account>(`/api/traders/${id}`, { timeout: 12000 });
  performance = (id: number) =>
    this.http.get<Performance>(`/api/traders/trades/${id}`, { timeout: 12000 });
  place = (order: CreateOrder) =>
    this.http.post<{ id: string }>('/api/orders', order, { timeout: 12000 });
  cancel = (id: string) => this.http.delete(`/api/orders/${id}`, { timeout: 12000 });
}

export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    console.warn('Exchange request failed', { status: error.status });
    if (error.status === 0)
      return 'Cannot reach the exchange. Check your connection and try refreshing.';
    if (error.status === 404)
      return 'This trader, stock, or order no longer exists. Refresh to check the latest data.';
    if (typeof error.error?.title === 'string') return error.error.title;
  }
  return 'The exchange could not complete this request. Please try refreshing.';
}
export function uncertainOutcome(error: unknown): boolean {
  return !(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500;
}
