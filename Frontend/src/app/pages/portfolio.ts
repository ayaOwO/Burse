import { Component, computed, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Api } from '../core/api';
import { Session } from '../core/session';
import { liveQuery } from '../core/live-query';
import { QueryState } from '../shared/query-state';
import { Orders } from '../shared/orders';
import { Trades } from '../shared/trades';
import { PriceChange } from '../shared/price-change';
@Component({
  selector: 'app-portfolio',
  imports: [DecimalPipe, RouterLink, QueryState, Orders, Trades, PriceChange],
  template: `<div class="page-heading">
      <div>
        <h1>My portfolio</h1>
        <p>{{ session.trader()?.name }}'s positions, orders, and trading activity.</p>
      </div>
      <a class="button secondary" routerLink="/market">Browse market</a>
    </div>
    <app-query-state
      [updated]="q.updated()"
      [loading]="q.loading()"
      [error]="q.error()"
      [hasData]="!!q.data()"
      (refresh)="q.refresh()"
    />
    @if (q.data(); as d) {
      <dl class="portfolio-summary surface">
        <div>
          <dt>Cash balance</dt>
          <dd>{{ d.performance.currentBalance | number: '1.2-7' }}</dd>
        </div>
        <div>
          <dt>Holdings value</dt>
          <dd>
            {{ holdingsValue() === null ? 'Unavailable' : (holdingsValue() | number: '1.2-7') }}
          </dd>
        </div>
        <div>
          <dt>Total account value</dt>
          <dd>
            {{ accountValue() === null ? 'Unavailable' : (accountValue() | number: '1.2-7') }}
          </dd>
        </div>
        <div>
          <dt>Profit/loss since open</dt>
          <dd
            [class.positive]="d.performance.profitLoss > 0"
            [class.negative]="d.performance.profitLoss < 0"
          >
            {{ d.performance.profitLoss > 0 ? '+' : ''
            }}{{ d.performance.profitLoss | number: '1.2-7'
            }}<span class="performance-label">{{
              d.performance.profitLoss > 0
                ? 'Profit'
                : d.performance.profitLoss < 0
                  ? 'Loss'
                  : 'No change'
            }}</span>
          </dd>
        </div>
      </dl>
      <section class="surface section-gap">
        <div class="section-toolbar">
          <h2>Holdings</h2>
          <span class="muted">Valued at current quotes</span>
        </div>
        @if (!holdings().length) {
          <div class="empty">
            <h3>Your portfolio starts here</h3>
            <p>You do not own any shares yet. Browse the market to place a buy order.</p>
            <a class="button secondary" routerLink="/market">Explore stocks</a>
          </div>
        } @else {
          <div class="table-scroll" role="region" aria-label="Your share holdings" tabindex="0">
            <table>
              <thead>
                <tr>
                  <th>Company</th>
                  <th class="numeric">Shares owned</th>
                  <th class="numeric">Current price</th>
                  <th class="numeric" title="Since first load" aria-label="Change since first load">
                    Change
                  </th>
                  <th class="numeric">Position value</th>
                </tr>
              </thead>
              <tbody>
                @for (holding of holdings(); track holding.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/market', holding.id]">{{
                        holding.stock?.name ?? 'Stock ' + holding.id
                      }}</a>
                    </td>
                    <td class="numeric">{{ holding.quantity | number }}</td>
                    <td class="numeric">
                      {{
                        holding.stock
                          ? (holding.stock.currentPrice | number: '1.0-2')
                          : 'Unavailable'
                      }}
                    </td>
                    <td class="numeric">
                      @if (holding.stock) {
                        <app-price-change [stock]="holding.stock" />
                      } @else {
                        Unavailable
                      }
                    </td>
                    <td class="numeric">
                      {{
                        holding.stock
                          ? (holding.quantity * holding.stock.currentPrice | number: '1.2-7')
                          : 'Unavailable'
                      }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
      <section class="surface section-gap">
        <div class="section-toolbar">
          <h2>Open orders</h2>
          <span class="muted">{{ openOrders().length }} active</span>
        </div>
        <app-orders
          [orders]="openOrders()"
          [stocks]="d.stocks"
          emptyText="No open orders. Your pending buy and sell orders will appear here."
          (changed)="q.refresh()"
        />
      </section>
      <section class="surface section-gap">
        <div class="section-toolbar">
          <h2>Recent trades</h2>
          <span class="muted">Latest 8 executions</span>
        </div>
        <app-trades
          [trades]="d.performance.trades"
          [stocks]="d.stocks"
          [traderId]="session.trader()!.id"
        />
      </section>
      <p class="footnote">
        Profit/loss includes cash and the current value of your holdings. The exchange resets on
        server restart.
      </p>
    }`,
})
export class Portfolio {
  private api = inject(Api);
  session = inject(Session);
  q = liveQuery(() =>
    forkJoin({
      account: this.api.account(this.session.trader()!.id),
      performance: this.api.performance(this.session.trader()!.id),
      stocks: this.api.stocks(),
    }),
  );
  holdings = computed(() => {
    const d = this.q.data();
    return d
      ? Object.entries(d.account.stocksOwned)
          .filter(([, q]) => q > 0)
          .map(([id, quantity]) => ({
            id: Number(id),
            quantity,
            stock: d.stocks.find((s) => s.id === Number(id)),
          }))
      : [];
  });
  openOrders = computed(() => this.q.data()?.account.orders ?? []);
  holdingsValue = computed(() => {
    const holdings = this.holdings();
    if (!this.q.data() || holdings.some((holding) => !holding.stock)) return null;
    return holdings.reduce(
      (sum, holding) => sum + holding.quantity * holding.stock!.currentPrice,
      0,
    );
  });
  accountValue = computed(() => {
    const holdingsValue = this.holdingsValue();
    return holdingsValue === null
      ? null
      : this.q.data()!.performance.currentBalance + holdingsValue;
  });
}
