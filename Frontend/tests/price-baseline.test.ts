import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PriceBaseline } from '../src/app/core/price-baseline.ts';
const stock = (id: number, currentPrice: number) => ({ id, currentPrice, name: 'Test', amount: 1 });
test('change uses the first unrounded quote per stock through later observations', () => {
  const prices = new PriceBaseline();
  prices.observe([stock(1, 100), stock(2, 200)]);
  assert.equal(prices.change(stock(1, 100)), 0);
  prices.observe([stock(1, 110), stock(2, 180)]);
  assert.equal(prices.change(stock(1, 110)), 0.1);
  assert.equal(prices.change(stock(2, 180)), -0.1);
  prices.observe([stock(1, 120)]);
  assert.equal(prices.change(stock(1, 120)), 0.2);
  assert.equal(new PriceBaseline().change(stock(1, 120)), null);
});
test('missing or unusable baselines never produce infinite percentage changes', () => {
  const prices = new PriceBaseline();
  prices.observe([stock(1, 0), stock(2, NaN)]);
  assert.equal(prices.change(stock(1, 10)), null);
  assert.equal(prices.change(stock(2, 10)), null);
  prices.observe([stock(1, 0.0000001)]);
  assert.equal(prices.change(stock(1, 0.0000002)), 1);
  assert.equal(prices.change(stock(1, Infinity)), null);
});
