import { Component, computed, inject, input } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { Api } from '../core/api';
import { Stock } from '../core/models';

@Component({
  selector: 'app-price-change',
  imports: [PercentPipe],
  template: `<span
      class="numeric"
      [class.positive]="change() !== null && change()! > 0"
      [class.negative]="change() !== null && change()! < 0"
      [class.muted]="change() === null || change() === 0"
      title="Change since this stock was first loaded. Resets when the page is reloaded."
    >
      @if (change(); as value) {
        {{ value > 0 ? '+' : '' }}{{ value | percent: '1.0-2' }}
      } @else {
        {{ change() === 0 ? '0%' : 'Unavailable' }}
      }
    </span>
    @if (showLabel()) {
      <span class="muted"> since first load</span>
    }`,
})
export class PriceChange {
  private api = inject(Api);
  stock = input.required<Stock>();
  showLabel = input(false);
  change = computed(() => this.api.prices.change(this.stock()));
}
