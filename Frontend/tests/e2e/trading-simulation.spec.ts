import { test, expect, Page } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';

const api = 'http://127.0.0.1:5244';
const stockId = 8; // BMW is untouched by the other browser checks.
const evidence = '../.impeccable/review/trading-simulation';

test('three traders reconcile partial fills, reservations, cancellation and replacement', async ({
  browser,
  request,
}) => {
  test.setTimeout(90000);
  await mkdir(evidence, { recursive: true });
  const contexts = await Promise.all([1, 2, 4].map(() => browser.newContext()));
  const [amit, tal, uri] = await Promise.all(contexts.map((context) => context.newPage()));
  const errors: string[] = [];
  for (const page of [amit, tal, uri]) page.on('pageerror', (error) => errors.push(error.message));
  const get = async (path: string) => {
    const response = await request.get(`${api}${path}`);
    expect(response.ok()).toBeTruthy();
    return response.json();
  };
  const account = (id: number) => get(`/traders/${id}`);
  const initialTraders = await get('/traders');
  const initialAccounts = await Promise.all(initialTraders.map((t: any) => account(t.id)));
  const initialCash = initialTraders.reduce((sum: number, t: any) => sum + t.money, 0);
  const initialShares = initialAccounts.reduce(
    (sum: number, a: any) => sum + (a.stocksOwned[stockId] ?? 0),
    0,
  );
  const initial = Object.fromEntries(initialTraders.map((t: any) => [t.id, t.money]));
  const initialPerformance = Object.fromEntries(
    await Promise.all([1, 2, 4].map(async (id) => [id, await get(`/traders/trades/${id}`)])),
  );
  const ledger: object[] = [];
  const record = async (stage: string) =>
    ledger.push({
      stage,
      accounts: await Promise.all([0, 1, 2, 4].map(async (id) => ({ id, ...(await account(id)) }))),
    });
  try {
    for (const [page, id] of [
      [amit, 1],
      [tal, 2],
      [uri, 4],
    ] as const) {
      await page.goto('/sign-in');
      await page.getByLabel('Trader ID', { exact: true }).fill(String(id));
      await page.getByRole('button', { name: 'Continue to market' }).click();
      await expect(page.getByRole('heading', { name: 'Market', exact: true })).toBeVisible();
      await page.goto(`/market/${stockId}`);
      await expect(page.getByRole('heading', { name: 'BMW', exact: true })).toBeVisible();
    }
    await record('initial');
    await place(amit, 'buy', 50, 20);
    await expect.poll(async () => (await account(1)).stocksOwned[stockId]).toBe(20);
    await place(amit, 'sell', 10, 12);
    await fill(amit, 'sell', 10, 9);
    await expect(amit.locator('#order-issue')).toContainText('exceeds your available shares');
    await expect(
      amit.getByRole('button', { name: 'Place sell order', exact: true }),
    ).toBeDisabled();
    const asksBefore = (await account(1)).orders.length;
    expect(
      (
        await request.post(`${api}/orders`, {
          data: { trader_id: 1, stock_id: stockId, type: 'Ask', price: 10, amount: 9 },
        })
      ).status(),
    ).toBe(409);
    expect((await account(1)).orders.length).toBe(asksBefore);
    await refresh(tal);
    const otherOrder = tal.locator('app-orders tr').filter({ hasText: 'Trader 1' });
    await expect(otherOrder).toHaveCount(1);
    await expect(otherOrder.getByRole('button', { name: /Cancel/ })).toHaveCount(0);
    await place(tal, 'buy', 12, 5);
    await refresh(amit);
    await expect(
      amit.locator('app-orders tr').filter({ hasText: 'Your order' }).locator('td').nth(1),
    ).toHaveText('7');
    await place(uri, 'buy', 11, 10);
    await expect.poll(async () => (await account(4)).stocksOwned[stockId]).toBe(7);
    await place(uri, 'buy', 9, 6);
    await record('partial fills with two pending bids');
    const before = await account(4);
    const bids = before.orders.filter((o: any) => o.status === 'Open' && o.type === 'Bid');
    expect(bids.map((o: any) => o.amount).sort((a: number, b: number) => a - b)).toEqual([3, 6]);
    const available =
      before.money - bids.reduce((sum: number, o: any) => sum + o.price * o.amount, 0);
    const excess = Math.floor(available / 2) + 1;
    await fill(uri, 'buy', 2, excess);
    await expect(uri.locator('#order-issue')).toContainText('exceeds your available cash');
    await expect(uri.getByRole('button', { name: 'Place buy order', exact: true })).toBeDisabled();
    expect(
      (
        await request.post(`${api}/orders`, {
          data: { trader_id: 4, stock_id: stockId, type: 'Bid', price: 2, amount: excess },
        })
      ).status(),
    ).toBe(409);
    expect((await account(4)).orders.length).toBe(before.orders.length);
    await place(amit, 'sell', 9, 5);
    const afterSweep = await account(4);
    expect(afterSweep.stocksOwned[stockId]).toBe(12);
    expect(afterSweep.money).toBe(initial[4] - 121);
    const remainder = afterSweep.orders.find((o: any) => o.status === 'Open');
    expect(remainder.price).toBe(9);
    expect(remainder.amount).toBe(4);
    await uri.setViewportSize({ width: 390, height: 844 });
    await uri.goto('/portfolio');
    await expect(uri.locator('app-orders')).toContainText('Your order');
    await uri.getByRole('button', { name: 'Cancel BMW order' }).click();
    await expect(uri.getByText('Cancel buy of 4 BMW shares at 9.00?')).toBeVisible();
    await uri.getByRole('button', { name: 'Confirm cancellation' }).scrollIntoViewIfNeeded();
    await uri.evaluate(() => window.scrollTo(0, 0));
    await uri.screenshot({ path: `${evidence}/mobile-cancellation.png`, fullPage: true });
    await uri.getByRole('button', { name: 'Confirm cancellation' }).click();
    await expect
      .poll(async () => (await account(4)).orders.some((o: any) => o.id === remainder.id))
      .toBe(false);
    await uri.goto(`/market/${stockId}`);
    await place(uri, 'buy', 8, 5);
    await place(amit, 'sell', 7, 3);
    await refresh(uri);
    await uri.getByRole('button', { name: 'Cancel BMW order' }).click();
    await uri.getByRole('button', { name: 'Confirm cancellation' }).click();
    await expect
      .poll(async () => (await account(4)).orders.filter((o: any) => o.status === 'Open').length)
      .toBe(0);
    await place(tal, 'sell', 15, 2);
    await place(amit, 'buy', 15, 2);
    await record('final settlement');
    const expected = {
      1: { money: initial[1] - 835, shares: 2 },
      2: { money: initial[2] - 20, shares: 3 },
      4: { money: initial[4] - 145, shares: 15 },
      0: { money: initial[0] + 1000, shares: initialShares - 20 },
    };
    for (const [id, value] of Object.entries(expected)) {
      const actual = await account(Number(id));
      expect(actual.money).toBe(value.money);
      expect(actual.stocksOwned[stockId]).toBe(value.shares);
    }
    const finalTraders = await get('/traders');
    const finalAccounts = await Promise.all(finalTraders.map((t: any) => account(t.id)));
    const finalCash = finalTraders.reduce((sum: number, t: any) => sum + t.money, 0);
    const finalShares = finalAccounts.reduce(
      (sum: number, a: any) => sum + (a.stocksOwned[stockId] ?? 0),
      0,
    );
    expect(finalCash).toBe(initialCash);
    expect(finalShares).toBe(initialShares);
    const stock = await get(`/stocks/${stockId}`);
    expect(stock.stock.currentPrice).toBe(15);
    expect(stock.lastTrades.map((t: any) => [t.amount, t.price])).toEqual([
      [2, 15],
      [3, 8],
      [2, 9],
      [3, 11],
      [7, 10],
      [5, 10],
      [20, 50],
    ]);
    for (const [page, id] of [
      [amit, 1],
      [tal, 2],
      [uri, 4],
    ] as const) {
      await page.goto('/portfolio');
      await expect(page.locator('.refresh-bar')).toContainText('Updated');
      const history = await get(`/traders/trades/${id}`);
      expect(history.currentBalance).toBe(expected[id].money);
      expect(history.profitLoss).toBe(
        initialPerformance[id].profitLoss +
          expected[id].money - initial[id] +
          expected[id].shares * stock.stock.currentPrice,
      );
      await expect(page.locator('app-trades')).toContainText('BMW');
      await expect(page.locator('app-trades tbody tr')).toHaveCount(
        history.trades.length,
      );
      const holdings = (await account(id)).stocksOwned;
      const quotes = await get('/stocks');
      const holdingsValue = Object.entries(holdings).reduce(
        (sum, [stock, quantity]) =>
          sum +
          Number(quantity) * quotes.find((quote: any) => quote.id === Number(stock)).currentPrice,
        0,
      );
      await expect(page.locator('.portfolio-summary > div').nth(1).locator('dd')).toHaveText(
        new Intl.NumberFormat('en-US', {
          minimumFractionDigits: 2,
          maximumFractionDigits: 7,
        }).format(holdingsValue),
      );
    }
    await amit.evaluate(() => window.scrollTo(0, 0));
    await amit.screenshot({ path: `${evidence}/final-portfolio.png`, fullPage: true });
    await amit.reload();
    await expect(amit.getByRole('heading', { name: 'My portfolio', exact: true })).toBeVisible();
    expect(await amit.evaluate(() => localStorage.getItem('burse.traderId'))).toBe('1');
    await amit.getByRole('button', { name: 'Sign out' }).click();
    expect(await amit.evaluate(() => localStorage.getItem('burse.traderId'))).toBeNull();
    await amit.goto('/portfolio');
    await expect(amit).toHaveURL(/sign-in/);
    expect(errors).toEqual([]);
    await writeFile(
      `${evidence}/ledger.json`,
      JSON.stringify(
        {
          initialCash,
          finalCash,
          initialShares,
          finalShares,
          initialPerformance,
          expected,
          trades: stock.lastTrades,
          ledger,
          errors,
        },
        null,
        2,
      ),
    );
    console.log(
      JSON.stringify({
        initialCash,
        finalCash,
        initialShares,
        finalShares,
        final: expected,
        executions: stock.lastTrades.length,
        errors,
      }),
    );
  } finally {
    await Promise.all(contexts.map((context) => context.close()));
  }
});
async function refresh(page: Page) {
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
}
async function fill(page: Page, side: 'buy' | 'sell', price: number, amount: number) {
  await page.getByRole('button', { name: side === 'buy' ? 'Buy' : 'Sell', exact: true }).click();
  await page.getByLabel('Limit price', { exact: true }).fill(String(price));
  await page.getByLabel('Quantity', { exact: true }).fill(String(amount));
}
async function place(page: Page, side: 'buy' | 'sell', price: number, amount: number) {
  await refresh(page);
  await fill(page, side, price, amount);
  await page.getByRole('button', { name: `Place ${side} order`, exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Order submitted' })).toBeVisible();
  await expect(page.getByLabel('Limit price', { exact: true })).toHaveValue('');
  await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
}
