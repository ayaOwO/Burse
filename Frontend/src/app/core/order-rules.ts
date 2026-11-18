import type { Account, Side } from './models';
export function availableCash(account: Account): number {
  return (
    account.money -
    account.orders
      .filter((o) => o.status === 'Open' && o.type === 'Bid')
      .reduce((sum, o) => sum + o.price * o.amount, 0)
  );
}
export function availableShares(account: Account, stockId: number): number {
  return (
    (account.stocksOwned[stockId] ?? 0) -
    account.orders
      .filter((o) => o.status === 'Open' && o.type === 'Ask' && o.stockId === stockId)
      .reduce((sum, o) => sum + o.amount, 0)
  );
}
export function orderIssue(
  account: Account,
  stockId: number,
  side: Side,
  price: number,
  amount: number,
): string {
  if (!Number.isFinite(price) || price < 0.0000001) return 'Enter a price of at least 0.0000001.';
  if (!Number.isInteger(amount) || amount < 1 || amount > 2147483647)
    return 'Enter a whole-share quantity between 1 and 2,147,483,647.';
  if (!Number.isFinite(price * amount)) return 'This order value is too large.';
  if (account.orders.some((o) => o.stockId === stockId && o.status === 'Open' && o.type !== side))
    return 'Cancel your opposite-side order for this stock first. Find it in the order book or My portfolio.';
  if (side === 'Bid' && price * amount > availableCash(account))
    return 'This order exceeds your available cash.';
  if (side === 'Ask' && amount > availableShares(account, stockId))
    return 'This order exceeds your available shares.';
  return '';
}
