import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Api, errorMessage, uncertainOutcome } from '../core/api';
import { Session } from '../core/session';
import { Order, Stock } from '../core/models';
@Component({
  selector: 'app-orders',
  imports: [DecimalPipe, RouterLink],
  template: `
    @if (message()) {
      <p class="notice" role="status">{{ message() }}</p>
    }
    @if (failure()) {
      <p class="notice error" role="alert">{{ failure() }}</p>
    }
    @if (!orders().length) {
      <p class="empty compact">{{ emptyText() }}</p>
    } @else {
      <div class="table-scroll" role="region" [attr.aria-label]="label()" tabindex="0">
        <table>
          <thead>
            <tr>
              @if (stocks().length) {
                <th>Company</th>
                <th>Side</th>
              }
              <th class="numeric">Limit price</th>
              <th class="numeric">Remaining</th>
              <th>Owner</th>
              <th><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (order of orders(); track order.id) {
              <tr [class.own-row]="order.traderId === session.trader()?.id">
                @if (stocks().length) {
                  <td>
                    <a [routerLink]="['/market', order.stockId]">{{ name(order.stockId) }}</a>
                  </td>
                  <td>{{ order.type === 'Bid' ? 'Buy' : 'Sell' }}</td>
                }
                <td class="numeric">{{ order.price | number: '1.2-7' }}</td>
                <td class="numeric">{{ order.amount | number }}</td>
                <td class="nowrap">
                  {{
                    order.traderId === session.trader()?.id
                      ? 'Your order'
                      : order.traderId === 0
                        ? 'Exchange'
                        : 'Trader ' + order.traderId
                  }}
                </td>
                <td>
                  @if (order.traderId === session.trader()?.id && order.status === 'Open') {
                    @if (confirming() === order.id) {
                      <div class="cancel-confirm">
                        <p>
                          Cancel {{ order.type === 'Bid' ? 'buy' : 'sell' }} of
                          {{ order.amount | number }} {{ name(order.stockId) }} shares at
                          {{ order.price | number: '1.2-7' }}?
                        </p>
                        <button
                          class="button danger small"
                          (click)="cancel(order)"
                          [disabled]="!!pending()"
                        >
                          {{ pending() ? 'Cancelling…' : 'Confirm cancellation' }}
                        </button>
                        <button
                          class="text-button"
                          (click)="confirming.set('')"
                          [disabled]="!!pending()"
                        >
                          Keep order
                        </button>
                      </div>
                    } @else {
                      <button
                        class="text-button"
                        (click)="confirming.set(order.id); failure.set('')"
                        [disabled]="!!pending() || blocked() === order.id"
                        [attr.aria-label]="'Cancel ' + name(order.stockId) + ' order'"
                      >
                        Cancel order
                      </button>
                    }
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
    @if (blocked()) {
      <button
        class="button secondary small"
        (click)="blocked.set(''); failure.set(''); changed.emit()"
      >
        I have reviewed the refreshed orders
      </button>
    }
  `,
})
export class Orders {
  session = inject(Session);
  private api = inject(Api);
  private destroyRef = inject(DestroyRef);
  orders = input.required<Order[]>();
  stocks = input<Stock[]>([]);
  label = input('Open orders');
  stockName = input('');
  emptyText = input('No open orders.');
  changed = output();
  confirming = signal('');
  pending = signal('');
  blocked = signal('');
  message = signal('');
  failure = signal('');
  name(id: number) {
    return this.stocks().find((s) => s.id === id)?.name ?? (this.stockName() || `Stock ${id}`);
  }
  cancel(order: Order) {
    if (this.pending() || order.traderId !== this.session.trader()?.id) return;
    this.pending.set(order.id);
    this.failure.set('');
    this.message.set('');
    this.api
      .cancel(order.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pending.set('');
          this.confirming.set('');
          this.message.set('Order cancelled.');
          this.changed.emit();
        },
        error: (error) => {
          this.pending.set('');
          this.confirming.set('');
          if (uncertainOutcome(error)) {
            this.blocked.set(order.id);
            this.failure.set(
              'Cancellation outcome unknown. Refresh and review this order before another attempt.',
            );
          } else this.failure.set(errorMessage(error));
          this.changed.emit();
        },
      });
  }
}
