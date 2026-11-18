import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { liveQuery } from '../core/live-query';
import { QueryState } from '../shared/query-state';
import { PriceChange } from '../shared/price-change';
@Component({
  selector: 'app-market',
  imports: [DecimalPipe, RouterLink, QueryState, PriceChange],
  template: `<div class="page-heading">
      <div>
        <h1>Market</h1>
        <p>Find your next position. All companies, at a glance.</p>
      </div>
      <span class="context-label">{{ q.data()?.length ?? '—' }} listed companies</span>
    </div>
    <app-query-state
      [updated]="q.updated()"
      [loading]="q.loading()"
      [error]="q.error()"
      [hasData]="!!q.data()"
      (refresh)="q.refresh()"
    />
    <section class="surface">
      <div class="section-toolbar">
        <h2>All stocks</h2>
        <div class="search-field">
          <label for="stock-search" class="sr-only">Search companies or stock IDs</label
          ><input
            id="stock-search"
            type="search"
            placeholder="Search company or ID"
            [value]="search()"
            (input)="search.set($any($event.target).value)"
          />
        </div>
      </div>
      @if (q.data()) {
        <div class="table-scroll" role="region" aria-label="Market stocks" tabindex="0">
          <table class="market-table">
            <thead>
              <tr>
                <th>Company</th>
                <th>Stock ID</th>
                <th class="numeric">Current price</th>
                <th class="numeric" title="Since first load" aria-label="Change since first load">
                  Change
                </th>
                <th class="numeric">Total shares issued</th>
                <th><span class="sr-only">View stock</span></th>
              </tr>
            </thead>
            <tbody>
              @for (stock of filtered(); track stock.id) {
                <tr>
                  <td>
                    <a class="company-link" [routerLink]="['/market', stock.id]"
                      ><span class="company-initial" aria-hidden="true">{{
                        stock.name.slice(0, 1)
                      }}</span
                      >{{ stock.name }}</a
                    >
                  </td>
                  <td class="muted">{{ stock.id }}</td>
                  <td class="numeric price">{{ stock.currentPrice | number: '1.0-2' }}</td>
                  <td class="numeric"><app-price-change [stock]="stock" /></td>
                  <td class="numeric muted">{{ stock.amount | number }}</td>
                  <td class="row-end">
                    <a
                      [routerLink]="['/market', stock.id]"
                      [attr.aria-label]="'Trade ' + stock.name"
                      >Trade</a
                    >
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (!filtered().length) {
          <div class="empty">
            <h3>{{ search() ? 'No matching companies' : 'No stocks listed' }}</h3>
            <p>
              {{
                search()
                  ? 'Try another company name or stock ID.'
                  : 'The exchange has no stock data yet. Try refreshing.'
              }}
            </p>
            @if (search()) {
              <button class="button secondary" (click)="search.set('')">Clear search</button>
            }
          </div>
        }
      }
    </section>
    <p class="footnote">
      Prices are simulated and update during trading. Order execution may differ from the current
      quote. Change is measured from each stock's first loaded price and resets on page reload.
    </p>`,
})
export class Market {
  private api = inject(Api);
  q = liveQuery(() => this.api.stocks());
  search = signal('');
  filtered = computed(() =>
    (this.q.data() ?? []).filter((s) =>
      `${s.name} ${s.id}`.toLowerCase().includes(this.search().trim().toLowerCase()),
    ),
  );
}
