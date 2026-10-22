import { test, expect } from '@playwright/test';

const pages = [
  ['/', 'Fault Code'],
  ['/authentication', 'Authentication and permissions'],
  ['/conventions', 'API conventions'],
];
for (const [route, title] of pages) {
  test(`anonymous reader: ${route}`, async ({ page }, info) => {
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(e.message));
    const response = await page.goto(route);
    expect(response?.status()).toBe(200);
    await expect(page.getByRole('heading', { name: title, level: 1, exact: true })).toBeVisible();
    expect(await page.getByRole('heading', { level: 1 }).evaluate(e => parseFloat(getComputedStyle(e).fontSize))).toBeGreaterThan(24);
    expect(new URL(page.url()).hostname).toBe('localhost');
    await expect(page.locator('html')).not.toHaveClass(/dark/);
    expect(await page.context().cookies()).toEqual([]);
    expect(errors).toEqual([]);
    if (route === '/') await page.screenshot({ path: info.outputPath('anonymous-docs-desktop.png'), fullPage: true });
  });
}

test('generated protected reference preserves permissions, errors and separate testing interface', async ({ page }, info) => {
  await page.goto('/');
  await page.getByRole('link', { name: 'API reference', exact: true }).click();
  await expect(page).toHaveURL(/api-reference/, { timeout: 45000 });
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('List Drivers');
  await expect(page.locator('#content-area')).toContainText('Authorization');
  await expect(page.getByRole('heading', { name: 'Authorizations', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: '401', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: '403', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: '429', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Try it|Send request/i })).toHaveCount(0);
  await page.screenshot({ path: info.outputPath('protected-reference.png'), fullPage: true });
});

test('mobile public guide fits and remains readable', async ({ page }, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Fault Code', level: 1, exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: info.outputPath('anonymous-docs-mobile.png'), fullPage: true });
});

test('authentication describes request headers without credential management', async ({ page }) => {
  await page.goto('/authentication');
  const content = page.locator('#content-area');
  await expect(content).toContainText('Authorization: Bearer');
  await expect(content).not.toContainText(/(?:reset|refresh|generate)\s+(?:an?\s+|your\s+)?(?:personal\s+|api\s+|access\s+)?token|PKCE|client credentials|personal.api.token|\/session\/api-token/i);
  await expect(content).toContainText('200 API requests per day');
  await expect(content).toContainText('00:00 UTC');
  await expect(content).toContainText('Refreshing or replacing a token does not reset usage.');
  await expect(content).toContainText('429 Too Many Requests');
  await expect(content).toContainText('Retry-After');
});

test('obsolete personal-token operation is absent', async ({ page }) => {
  const response = await page.goto('/api-reference/account/reset-personal-api-token');
  expect(response?.status()).toBe(404);
  await expect(page.getByRole('heading', {name:'Reset Personal Api Token', exact:true})).toHaveCount(0);
});
