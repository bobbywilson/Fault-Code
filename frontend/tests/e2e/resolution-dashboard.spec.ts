import { test, expect, Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import operations from '../../src/app/operations.json';

const id = '55555555-5555-4555-8555-555555555555';
const permissions = [...new Set(operations.flatMap((o) => o.policies.flat()))];
async function session(page: Page) {
  await page.route('**/__test/session', (r) =>
    r.fulfill({ json: { state: 'signed-in', permissions } }),
  );
  await page.route('**/api/v1/**', (r) =>
    r.fulfill({ json: { items: [], page: 1, totalCount: 0 } }),
  );
}
test('dashboard explains the topics in order and stack navigation anchors below them', async ({
  page,
}) => {
  await session(page);
  await page.goto('/admin/vehicles');
  await expect(
    page.getByRole('navigation', { name: 'Administration' }).getByRole('link'),
  ).toHaveText([
    'Dashboard',
    'Vehicles',
    'Drivers',
    'Incidents',
    'Notifications',
    'Users',
    'Ontology',
    'Fault codes',
    'Token',
    'Tickets',
  ]);
  await page.getByRole('link', { name: 'Development stack', exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/dashboard#development-stack$/);
  await expect(
    page.getByRole('heading', { name: 'Development stack', exact: true }),
  ).toBeInViewport();
  const sections = page.locator(
    'section[aria-label="OpenSpec overview"], section[aria-label="Ontology overview"], section[aria-label="Agentic fault flow"], section[aria-label="Development stack"]',
  );
  expect(
    await sections.evaluateAll((nodes) => nodes.map((n) => n.getAttribute('aria-label'))),
  ).toEqual(['Agentic fault flow', 'OpenSpec overview', 'Ontology overview', 'Development stack']);
  await expect(
    page.getByRole('group', { name: 'Fault investigation flow diagram' }),
  ).toBeAttached();
  await expect(page.getByRole('region', { name: 'Development stack', exact: true })).toContainText(
    'Microsoft Agent Framework',
  );
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('collapsed footer links identify API reference and development stack', async ({ page }) => {
  await session(page);
  await page.goto('/admin/dashboard');
  await page.getByRole('button', { name: 'Toggle Sidebar' }).click();
  await expect(page.locator('[data-state="collapsed"][data-collapsible="icon"]')).toBeVisible();
  for (const name of ['API reference', 'Development stack']) {
    await page.getByRole('link', { name, exact: true }).hover();
    await expect(page.getByRole('tooltip', { name, exact: true })).toBeVisible();
  }
});

test('vehicles offer known and unknown paths and dispatch the exact selection', async ({
  page,
}) => {
  await session(page);
  await page.route('**/api/v1/fault-codes?**', (r) =>
    r.fulfill({
      json: {
        items: [
          { code: 'SYN-EPS:ASSIST-SIGNAL', system: 'EPS', draft: { description: 'Assist signal' } },
        ],
        totalCount: 1,
      },
    }),
  );
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({
      json: {
        items: [
          {
            id,
            identifier: 'SYN-SILVERADO-1',
            system: 'EPS',
            active: true,
            drivers: [{ displayName: 'Jordan Lee', email: 'jordan.lee@example.com' }],
          },
        ],
        totalCount: 1,
      },
    }),
  );
  const dispatched: any[] = [];
  await page.route('**/api/v1/incidents', (r) => {
    dispatched.push(r.request().postDataJSON());
    return r.fulfill({ json: { incidentId: id, notificationId: id } });
  });
  await page.goto('/admin/vehicles');
  const select = page.getByRole('combobox');
  await select.click();
  await expect(page.getByRole('option')).toHaveText([
    'SYN-EPS:ASSIST-SIGNAL — Catalog',
    'SYN-EPS:UNLISTED — Unknown',
  ]);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await select.press('Escape');
  const dispatch = page.getByRole('button', { name: 'Dispatch', exact: true });
  await expect(dispatch).toBeDisabled();
  for (const code of ['SYN-EPS:ASSIST-SIGNAL', 'SYN-EPS:UNLISTED']) {
    await select.click();
    await page.getByRole('option', { name: new RegExp(code) }).click();
    await expect(dispatch).toBeEnabled();
    await dispatch.click();
    await expect(page.getByText('Dispatched', { exact: true }).last()).toBeVisible();
    await expect(dispatch).toBeEnabled();
    await expect(select).toContainText(code);
  }
  await expect(page.locator('select')).toHaveCount(0);
  expect(dispatched).toEqual(
    ['SYN-EPS:ASSIST-SIGNAL', 'SYN-EPS:UNLISTED'].map((code) => ({ vehicleId: id, code })),
  );
});

test('incident review resolves with notes and links to its actual Datadog incident', async ({
  page,
}) => {
  await session(page);
  let record: any = {
    id,
    code: 'SYN-EPS:UNLISTED',
    system: 'EPS',
    status: 'Received',
    version: 1,
    receivedAt: '2026-10-22T03:14:34Z',
    intakeCorrelationId: 'a06e0606bca242d186e6e490920d5058',
    datadog: {
      state: 'Linked',
      id: '66666666-6666-4666-8666-666666666666',
      number: 42,
      url: 'https://app.datadoghq.com/incidents/42',
    },
    fleet: { vehicleIdentifier: 'SYN-SILVERADO-1', driverIdentifier: 'Jordan Lee' },
  };
  await page.route('**/api/v1/incidents?**', (r) =>
    r.fulfill({ json: { items: [record], totalCount: 1 } }),
  );
  const writes: { body: any; headers: any }[] = [];
  await page.route('**/api/v1/incidents/' + id, (r) => {
    if (r.request().method() === 'PUT') {
      const body = r.request().postDataJSON();
      writes.push({ body, headers: r.request().headers() });
      record = { ...record, version: 2, status: 'Resolved', resolution: { text: body.resolution } };
    }
    return r.fulfill({ json: record, headers: { ETag: '"' + record.version + '"' } });
  });
  await page.goto('/admin/incidents');
  await page.getByRole('button', { name: 'Review incident SYN-EPS:UNLISTED', exact: true }).click();
  await expect(page.getByRole('button', { name: /Load records|Refresh incident/ })).toHaveCount(0);
  const link = page.getByRole('link', { name: 'View in Datadog' });
  const url = new URL((await link.getAttribute('href'))!);
  expect(url.origin).toBe('https://app.datadoghq.com');
  expect(url.pathname).toBe('/incidents/42');
  expect(url.search).toBe('');
  await expect(link).toHaveAttribute('target', '_blank');
  const resolve = page.getByRole('button', { name: 'Resolve incident', exact: true });
  await expect(resolve).toBeDisabled();
  const notes = 'Connector inspection completed.\nArrange a qualified service assessment.';
  await page.getByLabel('Resolution *', { exact: true }).fill(notes);
  await resolve.click();
  await expect(page.getByText('Incident resolved', { exact: true })).toBeVisible();
  await expect(resolve).toBeDisabled();
  expect(writes).toHaveLength(1);
  expect(writes[0].body).toEqual({ status: 'Resolved', resolution: notes });
  expect(writes[0].headers['if-match']).toBe('"1"');
  expect(writes[0].headers['idempotency-key']).toMatch(/^[a-f0-9-]{36}$/);
  record.datadog.url = 'https://untrusted.example.com/incidents/42';
  await page.getByRole('button', { name: 'Back to incidents', exact: true }).click();
  await page.getByRole('button', { name: 'Review incident SYN-EPS:UNLISTED', exact: true }).click();
  await expect(page.getByRole('link', { name: 'View in Datadog' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'View in Datadog' })).toBeDisabled();
});
