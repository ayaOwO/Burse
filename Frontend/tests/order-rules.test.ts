import { test } from 'node:test';
import assert from 'node:assert/strict';
import { availableCash, availableShares, orderIssue } from '../src/app/core/order-rules.ts';
import type { Account } from '../src/app/core/models.ts';
const account: Account = {
  money: 100,
  stocksOwned: { '1': 10 },
  orders: [
    { id: 'buy', stockId: 2, traderId: 1, type: 'Bid', price: 5, amount: 4, status: 'Open' },
    { id: 'sell', stockId: 1, traderId: 1, type: 'Ask', price: 10, amount: 3, status: 'Open' },
    {
      id: 'cancelled',
      stockId: 1,
      traderId: 1,
      type: 'Ask',
      price: 10,
      amount: 100,
      status: 'Cancelled',
    },
  ],
};
test('reservations include only open orders and correct stock/side', () => {
  assert.equal(availableCash(account), 80);
  assert.equal(availableShares(account, 1), 7);
  assert.match(orderIssue(account, 3, 'Bid', 10, 9), /cash/);
  assert.match(orderIssue(account, 1, 'Ask', 10, 8), /shares/);
  assert.equal(orderIssue(account, 1, 'Ask', 10, 7), '');
});
test('opposite sides cannot coexist for the same stock', () => {
  assert.match(orderIssue(account, 1, 'Bid', 10, 1), /opposite-side/);
  assert.equal(orderIssue(account, 3, 'Bid', 10, 1), '');
});
test('validates finite prices, minimum precision, integer quantities and overflow', () => {
  for (const price of [NaN, Infinity, -1, 0, 0.00000001])
    assert.notEqual(orderIssue(account, 3, 'Bid', price, 1), '');
  for (const quantity of [0, -1, 1.5, Infinity, 2147483648])
    assert.notEqual(orderIssue(account, 3, 'Bid', 1, quantity), '');
  assert.match(orderIssue(account, 3, 'Bid', Number.MAX_VALUE, 2), /too large/);
  assert.equal(orderIssue(account, 3, 'Bid', 0.0000001, 1), '');
});
