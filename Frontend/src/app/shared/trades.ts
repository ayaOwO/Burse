import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Stock, Trade } from '../core/models';
@Component({
  selector: 'app-trades',
  imports: [DecimalPipe, RouterLink],
  template: `@if (!trades().length) {
      <div class="empty">
        <h3>No trades yet</h3>
        <p>Completed trades will appear here when buy and sell orders match.</p>
      </div>
    } @else {
      <div class="table-scroll" role="region" aria-label="Recent executed trades" tabindex="0">
        <table>
          <thead>
            <tr>
              <th>Execution</th>
              @if (stocks().length) {
                <th>Company</th>
              }
              @if (traderId() !== null) {
                <th>Side</th>
              }
              <th class="numeric">Quantity</th>
              <th class="numeric">Price</th>
              <th class="numeric">Value</th>
            </tr>
          </thead>
          <tbody>
            @for (trade of trades(); track trade.id) {
              <tr>
                <td class="muted">#{{ trade.id }}</td>
                @if (stocks().length) {
                  <td>
                    <a [routerLink]="['/market', trade.stockId]">{{ name(trade.stockId) }}</a>
                  </td>
                }
                @if (traderId() !== null) {
                  <td>{{ trade.buyerId === traderId() ? 'Buy' : 'Sell' }}</td>
                }
                <td class="numeric">{{ trade.amount | number }}</td>
                <td class="numeric">{{ trade.price | number: '1.2-7' }}</td>
                <td class="numeric">{{ trade.price * trade.amount | number: '1.2-7' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="panel-footnote">Execution times are not supplied by the current API.</p>
    }`,
})
export class Trades {
  trades = input.required<Trade[]>();
  stocks = input<Stock[]>([]);
  traderId = input<number | null>(null);
  name(id: number) {
    return this.stocks().find((s) => s.id === id)?.name ?? `Stock ${id}`;
  }
}
