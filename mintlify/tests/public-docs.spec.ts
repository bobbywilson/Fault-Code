import { test, expect } from '@playwright/test';

const pages = [
  ['/', 'Fault Code'],
  ['/authentication', 'Authentication and permissions'],
  ['/conventions', 'API conventions'],
  ['/workflows', 'Three synthetic workflows'],
  ['/operations', 'Operations'],
  ['/release', 'Release and rollback'],
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
    expect(await page.context().cookies()).toEqual([]);
    expect(errors).toEqual([]);
    if (route === '/') await page.screenshot({ path: info.outputPath('anonymous-docs-desktop.png'), fullPage: true });
  });
}

test('generated protected reference preserves permissions, errors and non-sending mode', async ({ page }, info) => {
  await page.goto('/');
  await page.getByRole('link', { name: 'API reference', exact: true }).click();
  await expect(page).toHaveURL(/api-reference/, { timeout: 45000 });
  await expect(page.getByRole('heading', { level: 1 })).toContainText('adminroles');
  await expect(page.locator('#content-area')).toContainText('Requires users.manage');
  await expect(page.locator('#content-area')).toContainText('exact role Users.Manage');
  await expect(page.getByRole('heading', { name: 'Authorizations', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: '401', exact: true })).toBeVisible();
  await expect(page.getByRole('tab', { name: '403', exact: true })).toBeVisible();
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
