import type { Stock } from './models';

export class PriceBaseline {
  private initial = new Map<number, number>();

  observe(stocks: Stock[]) {
    for (const stock of stocks) {
      if (
        !this.initial.has(stock.id) &&
        Number.isFinite(stock.currentPrice) &&
        stock.currentPrice > 0
      )
        this.initial.set(stock.id, stock.currentPrice);
    }
  }

  change(stock: Stock): number | null {
    const initial = this.initial.get(stock.id);
    return initial !== undefined && Number.isFinite(stock.currentPrice)
      ? (stock.currentPrice - initial) / initial
      : null;
  }
}
