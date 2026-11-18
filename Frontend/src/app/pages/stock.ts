import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { Api, errorMessage, uncertainOutcome } from '../core/api';
import { Session } from '../core/session';
import { liveQuery } from '../core/live-query';
import { availableCash, availableShares, orderIssue } from '../core/order-rules';
import { Order, Side } from '../core/models';
import { QueryState } from '../shared/query-state';
import { Orders } from '../shared/orders';
import { Trades } from '../shared/trades';
import { PriceChange } from '../shared/price-change';
@Component({
  selector: 'app-stock',
  imports: [DecimalPipe, RouterLink, ReactiveFormsModule, QueryState, Orders, Trades, PriceChange],
  template: `<a class="back-link" routerLink="/market">All stocks</a>
    <div class="page-heading">
      <div>
        <h1>{{ q.data()?.details?.stock?.name ?? 'Stock details' }}</h1>
        <p>
          @if (q.data(); as d) {
            Stock {{ d.details.stock.id }} · {{ d.details.stock.amount | number }} total shares
            issued
          } @else {
            Review orders and place a trade.
          }
        </p>
      </div>
      @if (q.data(); as d) {
        <div class="quote">
          <span class="muted">Current price</span
          ><strong>{{ d.details.stock.currentPrice | number: '1.0-2' }}</strong>
          <div><app-price-change [stock]="d.details.stock" [showLabel]="true" /></div>
        </div>
      }
    </div>
    <app-query-state
      [updated]="q.updated()"
      [loading]="q.loading()"
      [error]="q.error()"
      [hasData]="!!q.data()"
      (refresh)="q.refresh()"
    />
    @if (q.data(); as d) {
      <div class="trading-layout">
        <section class="surface order-book">
          <div class="section-toolbar">
            <h2>Order book</h2>
            <span class="muted">Open orders</span>
          </div>
          <div class="book-section">
            <h3>
              <span class="side-marker buy"></span>Buy orders
              <span class="muted count">{{ buys().length }}</span>
            </h3>
            <app-orders
              [orders]="buys()"
              [stockName]="d.details.stock.name"
              label="Open buy orders"
              emptyText="No open buy orders."
              (changed)="q.refresh()"
            />
          </div>
          <div class="book-section">
            <h3>
              <span class="side-marker sell"></span>Sell orders
              <span class="muted count">{{ asks().length }}</span>
            </h3>
            <app-orders
              [orders]="asks()"
              [stockName]="d.details.stock.name"
              label="Open sell orders"
              emptyText="No open sell orders."
              (changed)="q.refresh()"
            />
          </div>
          <p class="panel-footnote">
            Buy orders: highest price first. Sell orders: lowest price first.
          </p>
        </section>
        <section class="surface ticket">
          <h2>Place an order</h2>
          <p class="muted">{{ d.details.stock.name }}</p>
          <div class="side-switch" role="group" aria-label="Order side">
            <button
              [attr.aria-pressed]="side() === 'Bid'"
              [class.selected]="side() === 'Bid'"
              (click)="side.set('Bid')"
              [disabled]="pending() || uncertain()"
            >
              Buy</button
            ><button
              [attr.aria-pressed]="side() === 'Ask'"
              [class.selected]="side() === 'Ask'"
              (click)="side.set('Ask')"
              [disabled]="pending() || uncertain()"
            >
              Sell
            </button>
          </div>
          <dl class="account-context">
            <div>
              <dt>Available cash</dt>
              <dd>{{ cash() | number: '1.2-7' }}</dd>
            </div>
            <div>
              <dt>Available shares</dt>
              <dd>{{ shares() | number }}</dd>
            </div>
            <div>
              <dt>Shares owned</dt>
              <dd>{{ d.account.stocksOwned[d.details.stock.id] ?? 0 | number }}</dd>
            </div>
          </dl>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <label for="limit-price">Limit price</label>
            <div class="label-action">
              <span class="field-help" id="price-help">Maximum buy / minimum sell price</span
              ><button
                type="button"
                class="text-button"
                (click)="useQuote()"
                [disabled]="pending() || uncertain()"
              >
                Use quote
              </button>
            </div>
            <input
              id="limit-price"
              type="number"
              min="0.0000001"
              step="any"
              formControlName="price"
              aria-describedby="price-help order-issue"
              [readOnly]="pending() || uncertain()"
            />
            <label for="quantity">Quantity</label
            ><input
              id="quantity"
              type="number"
              min="1"
              max="2147483647"
              step="1"
              formControlName="amount"
              aria-describedby="quantity-help order-issue"
              [readOnly]="pending() || uncertain()"
            />
            <p id="quantity-help" class="field-help">
              Whole shares only. Open orders reserve cash or shares.
            </p>
            <div class="order-value">
              <span>Order value</span><strong>{{ total() | number: '1.2-7' }}</strong>
            </div>
            @if (issue() && (form.dirty || form.touched || form.controls.price.value !== null)) {
              <p id="order-issue" class="field-error">{{ issue() }}</p>
            }
            @if (failure()) {
              <div class="notice error" role="alert">{{ failure() }}</div>
            }
            @if (message()) {
              <div class="notice success" role="status">{{ message() }}</div>
            }
            <button
              class="button primary full"
              type="submit"
              [disabled]="pending() || uncertain() || !!q.error() || !!issue() || form.invalid"
            >
              {{
                pending()
                  ? 'Submitting…'
                  : side() === 'Bid'
                    ? 'Place buy order'
                    : 'Place sell order'
              }}
            </button>
            @if (uncertain()) {
              <p class="field-help">
                Refresh and review the order book or your portfolio before unlocking this ticket.
              </p>
              <button
                type="button"
                class="button secondary full"
                [disabled]="q.loading() || !!q.error()"
                (click)="unlock()"
              >
                I have reviewed my orders
              </button>
            }
          </form>
          <p class="ticket-note">
            An order executes only when it matches another owner's offer. Quotes may change before
            execution.
          </p>
        </section>
      </div>
      <section class="surface section-gap">
        <div class="section-toolbar">
          <h2>Recent trades</h2>
          <span class="muted">Latest 10 executions</span>
        </div>
        <app-trades [trades]="d.details.lastTrades" />
      </section>
    }`,
})
export class StockPage {
  private api = inject(Api);
  session = inject(Session);
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  q = liveQuery(
    () =>
      forkJoin({
        details: this.api.stock(Number(this.route.snapshot.paramMap.get('stockId'))),
        account: this.api.account(this.session.trader()!.id),
      }),
    this.route.paramMap,
  );
  side = signal<Side>('Bid');
  pending = signal(false);
  uncertain = signal(false);
  message = signal('');
  failure = signal('');
  form = new FormGroup({
    price: new FormControl<number | null>(null, [Validators.required, Validators.min(0.0000001)]),
    amount: new FormControl<number | null>(1, [
      Validators.required,
      Validators.min(1),
      Validators.max(2147483647),
    ]),
  });
  values = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  cash = computed(() => (this.q.data() ? availableCash(this.q.data()!.account) : 0));
  shares = computed(() =>
    this.q.data() ? availableShares(this.q.data()!.account, this.q.data()!.details.stock.id) : 0,
  );
  total = computed(() => {
    const { price, amount } = this.values();
    const value = Number(price) * Number(amount);
    return Number.isFinite(value) ? value : 0;
  });
  issue = computed(() => {
    const d = this.q.data();
    const v = this.values();
    return d
      ? orderIssue(d.account, d.details.stock.id, this.side(), Number(v.price), Number(v.amount))
      : '';
  });
  orders = computed<Order[]>(() => this.q.data()?.details.orders ?? []);
  buys = computed(() =>
    this.orders()
      .filter((o) => o.type === 'Bid')
      .sort((a, b) => b.price - a.price),
  );
  asks = computed(() =>
    this.orders()
      .filter((o) => o.type === 'Ask')
      .sort((a, b) => a.price - b.price),
  );
  private initializedId = 0;
  constructor() {
    effect(() => {
      const d = this.q.data();
      if (d && this.initializedId !== d.details.stock.id) {
        this.initializedId = d.details.stock.id;
        this.form.reset({ price: d.details.stock.currentPrice, amount: 1 });
        this.side.set('Bid');
        this.message.set('');
        this.failure.set('');
        this.uncertain.set(false);
      }
    });
  }
  useQuote() {
    this.form.controls.price.setValue(this.q.data()!.details.stock.currentPrice);
  }
  unlock() {
    this.uncertain.set(false);
    this.failure.set('');
  }
  submit() {
    if (
      this.pending() ||
      this.uncertain() ||
      this.form.invalid ||
      this.issue() ||
      this.q.error() ||
      !this.q.data()
    )
      return;
    this.pending.set(true);
    this.message.set('');
    this.failure.set('');
    this.api
      .place({
        trader_id: this.session.trader()!.id,
        stock_id: this.q.data()!.details.stock.id,
        type: this.side(),
        price: this.form.controls.price.value!,
        amount: this.form.controls.amount.value!,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.pending.set(false);
          this.message.set(
            `Order submitted. Reference ${result.id}. Check the order book and recent trades for its outcome.`,
          );
          this.form.reset({ price: null, amount: 1 });
          this.q.refresh();
        },
        error: (error) => {
          this.pending.set(false);
          if (uncertainOutcome(error)) {
            this.uncertain.set(true);
            this.failure.set(
              'Submission outcome unknown. Your order may have been accepted. Review your orders before trying again.',
            );
          } else this.failure.set(errorMessage(error));
          this.q.refresh();
        },
      });
  }
}
