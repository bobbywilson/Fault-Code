import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
const base = process.argv[2];
assert.match(base, /^http:\/\/127\.0\.0\.1:\d+$/);
const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(m.text());
  });
  await page.goto(base + '/admin/incidents');
  await page.getByRole('heading', { name: 'Sign in to continue' }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Sign in with Microsoft' }).count(), 1);
  assert.equal(await page.getByRole('button', { name: 'Load records' }).count(), 0);
  assert.equal(await page.getByRole('navigation', { name: 'Administration' }).count(), 0);
  assert.equal(
    await page.locator('[data-slot=sidebar-wrapper]').evaluate((e) => getComputedStyle(e).display),
    'flex',
  );
  assert.equal(await page.locator('header').evaluate((e) => getComputedStyle(e).display), 'flex');
  const api = await page.request.get(base + '/api/v1/vehicles');
  assert.equal(api.status(), 401);
  await page.screenshot({
    path: 'frontend/test-results/production-linux-setup.png',
    fullPage: true,
  });
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto(base + '/swagger/index.html');
  await page.getByRole('heading', { name: /^Fault Code API/ }).waitFor();
  assert.equal(await page.locator('.opblock').count(), 30);
  assert.equal(
    await page.locator('html').evaluate((e) => e.classList.contains('dark-mode')),
    false,
  );
  assert.equal(
    await page.locator('body').evaluate((e) => getComputedStyle(e).backgroundColor),
    'rgb(255, 255, 255)',
  );
  await page
    .getByRole('button', { name: /Authorize/ })
    .first()
    .waitFor();
  await page.locator('.opblock-get').first().locator('.opblock-summary').click();
  await page.getByRole('button', { name: 'Try it out', exact: true }).click();
  await page.getByRole('button', { name: 'Execute', exact: true }).waitFor();
  await page.screenshot({
    path: 'frontend/test-results/production-linux-swagger.png',
    fullPage: true,
  });
  assert.deepEqual(errors, []);
  console.log(
    'Production browser route, unauthenticated sign-in state, no JavaScript errors and real API401 passed.',
  );
} finally {
  await browser.close();
}
