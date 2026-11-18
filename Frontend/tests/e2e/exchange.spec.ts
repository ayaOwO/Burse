import { test, expect, Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import AxeBuilder from '@axe-core/playwright';
const api = 'http://127.0.0.1:5244';
async function capture(page: Page, path: string) {
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path, fullPage: true });
}
test.beforeAll(async () => {
  await mkdir('../.impeccable/review', { recursive: true });
});
async function signIn(page: Page, id = '1') {
  await page.goto('/sign-in');
  await page.getByLabel('Trader ID', { exact: true }).fill(id);
  await page.getByRole('button', { name: 'Continue to market' }).click();
  await expect(page.getByRole('heading', { name: 'Market', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Apple', exact: true })).toBeVisible();
}
async function order(page: Page, side: 'buy' | 'sell', price: string, amount: string) {
  await page.getByRole('button', { name: side === 'buy' ? 'Buy' : 'Sell', exact: true }).click();
  await page.getByLabel('Limit price', { exact: true }).fill(price);
  await page.getByLabel('Quantity', { exact: true }).fill(amount);
  await page.getByRole('button', { name: `Place ${side} order`, exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Order submitted' })).toBeVisible();
  await expect(page.locator('#order-issue')).toHaveCount(0);
}

test('identity validation, browser persistence and sign out', async ({ page }) => {
  await page.goto('/portfolio');
  await expect(page).toHaveURL(/sign-in/);
  await page.getByRole('button', { name: 'Continue to market' }).click();
  await expect(page.getByText('Enter a whole-number trader ID')).toBeVisible();
  await page.getByLabel('Trader ID', { exact: true }).fill('99999');
  await page.getByRole('button', { name: 'Continue to market' }).click();
  await expect(page.getByRole('alert')).toContainText(
    'No trader found for this ID. Check the ID and try again.',
  );
  await capture(page, '../.impeccable/review/sign-in-error.png');
  await signIn(page);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Market', exact: true })).toBeVisible();
  await page.goto('/traders');
  await expect(page.getByRole('heading', { name: 'Traders', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Amit', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sign out' }).click();
  expect(await page.evaluate(() => localStorage.getItem('burse.traderId'))).toBeNull();
  await page.goBack();
  await expect(page).toHaveURL(/sign-in/);
});

test('two traders buy, match a sell, inspect performance and cancel remainder', async ({
  browser,
  request,
}) => {
  const alice = await browser.newContext();
  const bob = await browser.newContext();
  const a = await alice.newPage();
  const b = await bob.newPage();
  await signIn(a);
  await a.goto('/market/4');
  await order(a, 'buy', '33', '5');
  await expect
    .poll(async () => (await (await request.get(`${api}/traders/1`)).json()).stocksOwned['4'])
    .toBe(5);
  await order(a, 'sell', '1', '2');
  await expect(a.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled();
  await capture(a, '../.impeccable/review/order-submitted.png');
  await signIn(b, '2');
  await b.goto('/market/4');
  await order(b, 'buy', '1', '1');
  await a.getByRole('button', { name: 'Refresh', exact: true }).click();
  await a.goto('/portfolio');
  await expect(a.getByRole('heading', { name: 'My portfolio' })).toBeVisible();
  await expect(a.locator('app-trades')).toContainText('Ford');
  await expect(a.locator('app-trades')).toContainText('Sell');
  const performance = await (await request.get(`${api}/traders/trades/1`)).json();
  expect(performance.profitLoss).toBe(-160);
  await expect(a.locator('.portfolio-summary > div').nth(1)).toContainText('4.00');
  await expect(a.locator('.portfolio-summary > div').nth(2)).toContainText('9,999,840.00');
  await expect(a.locator('.portfolio-summary > div').nth(3)).toContainText(
    'Profit/loss since open',
  );
  await expect(a.locator('.portfolio-summary > div').nth(3)).toContainText('-160.00');
  const cancel = a.getByRole('button', { name: 'Cancel Ford order' });
  await cancel.click();
  await expect(a.getByText('Cancel sell of 1 Ford shares at 1.00?')).toBeVisible();
  await capture(a, '../.impeccable/review/cancel-confirmation.png');
  await a.getByRole('button', { name: 'Confirm cancellation' }).click();
  await expect(a.getByRole('status').filter({ hasText: 'Order cancelled.' })).toBeVisible();
  await expect(cancel).toHaveCount(0);
  await alice.close();
  await bob.close();
});

test('validates orders, serializes request and prevents duplicate submission', async ({
  page,
  request,
}) => {
  await signIn(page);
  await page.goto('/market/1');
  await expect(page.getByRole('heading', { name: 'Apple', exact: true })).toBeVisible();
  await page.getByLabel('Quantity', { exact: true }).fill('1.5');
  await expect(page.getByText('Enter a whole-share quantity')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Place buy order' })).toBeDisabled();
  await page.getByLabel('Quantity', { exact: true }).fill('1');
  await page.getByLabel('Limit price', { exact: true }).fill('0.0000001');
  let posts = 0;
  let payload: any;
  await page.route('**/api/orders', async (route) => {
    posts++;
    payload = route.request().postDataJSON();
    await route.continue();
  });
  const button = page.getByRole('button', { name: 'Place buy order', exact: true });
  await button.evaluate((element: HTMLButtonElement) => {
    element.click();
    element.click();
  });
  await expect(page.getByRole('status').filter({ hasText: 'Order submitted' })).toBeVisible();
  expect(posts).toBe(1);
  expect(payload).toEqual({ trader_id: 1, stock_id: 1, type: 'Bid', price: 0.0000001, amount: 1 });
  await page.getByRole('button', { name: 'Sell', exact: true }).click();
  await page.getByLabel('Limit price', { exact: true }).fill('230');
  await expect(page.getByText('Cancel your opposite-side order')).toBeVisible();
  const account = await (await request.get(`${api}/traders/1`)).json();
  const open = account.orders.find((o: any) => o.stockId === 1 && o.status === 'Open');
  expect(open.traderId).toBe(1);
  const cancelled = await request.delete(`${api}/orders/${open.id}`);
  expect(cancelled.status()).toBe(204);
});

test('uncertain submissions preserve draft and require explicit review', async ({ page }) => {
  await signIn(page);
  await page.goto('/market/2');
  await page.getByLabel('Limit price', { exact: true }).fill('1');
  let attempts = 0;
  await page.route('**/api/orders', async (route) => {
    attempts++;
    await route.abort('failed');
  });
  await page.getByRole('button', { name: 'Place buy order', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Submission outcome unknown');
  await expect(page.getByRole('button', { name: 'Place buy order', exact: true })).toBeDisabled();
  await expect(page.getByLabel('Limit price', { exact: true })).toHaveValue('1');
  await capture(page, '../.impeccable/review/unknown-submission.png');
  expect(attempts).toBe(1);
  await page.getByRole('button', { name: 'I have reviewed my orders', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Place buy order', exact: true })).toBeEnabled();
});

test('failed quote refresh retains previous data and explains staleness', async ({ page }) => {
  await signIn(page);
  await page.route('**/api/stocks', (route) => route.abort());
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Showing previously loaded data');
  await capture(page, '../.impeccable/review/stale-market.png');
  await expect(page.getByRole('link', { name: 'Apple', exact: true })).toBeVisible();
  await page.unroute('**/api/stocks');
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('renders every screen at desktop and mobile widths without page overflow', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  const directory = '../.impeccable/review';
  await mkdir(directory, { recursive: true });
  await page.goto('/sign-in');
  await capture(page, `${directory}/sign-in-desktop.png`);
  await signIn(page);
  for (const [route, name] of [
    ['/market', 'desktop'],
    ['/market/1', 'stock-desktop'],
    ['/portfolio', 'portfolio-desktop'],
    ['/traders', 'traders-desktop'],
  ]) {
    await page.goto(route);
    await expect(page.locator('.refresh-bar')).toContainText('Updated');
    expect(
      (
        await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
          .analyze()
      ).violations,
    ).toEqual([]);
    await capture(page, `${directory}/${name}.png`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
      true,
    );
  }
  await page.setViewportSize({ width: 390, height: 844 });
  for (const [route, name] of [
    ['/market', 'mobile'],
    ['/market/1', 'stock-mobile'],
    ['/portfolio', 'portfolio-mobile'],
    ['/traders', 'traders-mobile'],
  ]) {
    await page.goto(route);
    await expect(page.locator('.refresh-bar')).toContainText('Updated');
    expect(
      (
        await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
          .analyze()
      ).violations,
    ).toEqual([]);
    await capture(page, `${directory}/${name}.png`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
      true,
    );
  }
  await page.setViewportSize({ width: 320, height: 740 });
  await page.goto('/market/16');
  await expect(page.getByRole('heading', { name: 'GoldenShare' })).toBeVisible();
  await capture(page, `${directory}/user-320.png`);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await capture(page, `${directory}/sign-in-mobile.png`);
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  await page.getByLabel('Trader ID', { exact: true }).focus();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'Continue to market' })).toBeFocused();
  expect(errors).toEqual([]);
});

test('current prices use at most two decimals and signed changes retain the first quote', async ({
  page,
}) => {
  let price = 100.0049;
  const quote = () => ({ id: 1, name: 'Apple', currentPrice: price, amount: 15000 });
  await page.route('**/api/stocks', (route) => route.fulfill({ json: [quote()] }));
  await page.route('**/api/stocks/1', (route) =>
    route.fulfill({ json: { stock: quote(), orders: [], lastTrades: [] } }),
  );
  await signIn(page);
  const row = page
    .getByRole('row')
    .filter({ has: page.getByRole('link', { name: 'Apple', exact: true }) });
  const change = row.locator('app-price-change span');
  await expect(row.locator('td').nth(2)).toHaveText('100');
  await expect(change).toHaveText('0%');
  price = 110.00539;
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(row.locator('td').nth(2)).toHaveText('110.01');
  await expect(change).toHaveText('+10%');
  await expect(change).toHaveClass(/positive/);
  await expect(change).toHaveCSS('color', 'rgb(35, 100, 71)');
  await capture(page, '../.impeccable/review/price-gain-desktop.png');
  price = 90.00441;
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(change).toHaveText('-10%');
  await expect(change).toHaveClass(/negative/);
  await expect(change).toHaveCSS('color', 'rgb(161, 57, 50)');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await change.evaluate((element) => element.getBoundingClientRect().right <= innerWidth),
  ).toBe(true);
  await capture(page, '../.impeccable/review/price-loss-mobile.png');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('link', { name: 'Apple', exact: true }).click();
  await expect(page.locator('.quote strong')).toHaveText('90');
  await expect(page.locator('.quote app-price-change .negative')).toHaveText('-10%');
  await expect(page.getByLabel('Limit price', { exact: true })).toHaveValue('90.00441');
  await capture(page, '../.impeccable/review/price-detail-mobile.png');
  await page.getByRole('link', { name: 'All stocks', exact: true }).click();
  await expect(change).toHaveText('-10%');
  await page.reload();
  await expect(change).toHaveText('0%');
});
