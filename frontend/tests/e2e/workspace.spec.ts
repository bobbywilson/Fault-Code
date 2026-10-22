import { test, expect, Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import operations from '../../src/app/operations.json';
import { ontologyRelationships } from '../fixtures/ontology-model';
const id = '55555555-5555-4555-8555-555555555555';
const all = [...new Set(operations.flatMap((o) => o.policies.flat()))];
async function setup(page: Page, permissions = all) {
  await page.route('**/__test/session', (r) => r.fulfill({ json: { state: 'signed-in' } }));
  await page.route('**/api/v1/**', (r) =>
    r.fulfill({
      json: r.request().url().endsWith('/authorization/me')
        ? { callerKind: 'delegated', permissions }
        : r.request().url().endsWith('/ontology/relationships')
          ? { version: '1', items: ontologyRelationships }
          : { items: [], page: 1, pageSize: 20, totalCount: 0 },
      headers: { 'X-Correlation-ID': 'synthetic-reference' },
    }),
  );
}
async function action(page: Page, area: string, id: string) {
  await page.goto('/admin/' + area);
  if (area === 'incidents')
    await page
      .getByRole('button', { name: /Review incident SYN-/ })
      .first()
      .click();
  await page.getByLabel('Action', { exact: true }).selectOption(id);
  await expect(page.getByText('Checking permissions…')).not.toBeVisible();
}
async function submit(page: Page) {
  const checkbox = page.getByRole('checkbox', { name: /Confirm / });
  if (await checkbox.count()) {
    if (!(await checkbox.isChecked())) await checkbox.click();
    await expect(checkbox).toBeChecked();
  }
  await page.locator('button[type=submit]').click();
}
test('fleet pages are read-only and contain no management controls', async ({ page }) => {
  await setup(page);
  for (const area of ['vehicles', 'drivers']) {
    await page.goto('/admin/' + area);
    await expect(page.getByRole('button', { name: /Manage vehicles|Manage drivers/ })).toHaveCount(
      0,
    );
    await expect(page.getByLabel('Action', { exact: true })).toHaveCount(0);
    await expect(page.locator('button[type=submit]')).toHaveCount(0);
  }
});

test('fleet empty, forbidden and unavailable states never fabricate records', async ({ page }) => {
  await setup(page);
  await page.goto('/admin/vehicles');
  await expect(page.getByText('No vehicles recorded.', { exact: true })).toBeVisible();
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({ status: 503, json: { status: 503 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('unavailable');
  await expect(page.getByRole('table')).toHaveCount(0);
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({ status: 403, json: { status: 403 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Access denied');
});

test('401 clears records and navigation until a real session is renewed', async ({ page }) => {
  await setup(page);
  await page.goto('/admin/vehicles');
  await expect(page.getByText('No vehicles recorded.', { exact: true })).toBeVisible();
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({ status: 401, json: { status: 401 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your session has expired' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Administration', exact: true })).toHaveCount(
    0,
  );
});

test('retained incident investigation and notification views invoke authenticated operations', async ({
  page,
}) => {
  await setup(page);
  const requests: { url: string; method: string; body: any; headers: any }[] = [];
  await page.route('**/api/v1/**', (r) => {
    const q = r.request();
    if (q.url().endsWith('/authorization/me'))
      return r.fulfill({ json: { callerKind: 'delegated', permissions: all } });
    if (q.method() === 'GET' && new URL(q.url()).pathname === '/api/v1/incidents')
      return r.fulfill({
        json: {
          items: [
            {
              id,
              code: 'SYN-EPS:UNKNOWN',
              system: 'EPS',
              status: 'Received',
              occurredAt: '2026-10-21T12:00:00Z',
              fleet: {
                vehicleId: id,
                driverId: id,
                vehicleIdentifier: 'SYN-VEH-1',
                driverIdentifier: 'SYN-DRIVER-1',
              },
              knowledge: null,
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
        },
      });
    requests.push({
      url: q.url(),
      method: q.method(),
      body: q.postDataJSON(),
      headers: q.headers(),
    });
    return r.fulfill({
      json: { id, version: 2, outcome: 'Recorded synthetic fixture' },
      headers: { ETag: '"2"' },
    });
  });
  // Workflow A known fault and Workflow B approved patch use the same authoritative investigation boundary.
  for (const workflow of ['known-fault', 'approved-patch']) {
    await action(page, 'incidents', 'InvestigateIncident');
    await page.getByLabel('Id *', { exact: true }).fill(id);
    await page.getByLabel('Current record version').fill('1');
    await submit(page);
    await expect(page.getByRole('status')).toContainText('request completed');
    expect(requests.at(-1)?.url).toContain('/investigations');
    expect(requests.at(-1)?.body).toEqual({});
    expect(requests.at(-1)?.headers['if-match']).toBe('"1"');
  }
});
test('mobile fleet navigation remains accessible without management forms', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setup(page);
  await page.goto('/admin/vehicles');
  await expect(page.getByText('No vehicles recorded.', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  {
    const audit = await new AxeBuilder({ page }).analyze();
    expect(
      audit.violations.map((v) => ({ id: v.id, nodes: v.nodes.map((n) => n.failureSummary) })),
    ).toEqual([]);
  }
  await page.getByRole('button', { name: 'Toggle Sidebar' }).click();
  await page
    .getByRole('navigation', { name: 'Administration' })
    .getByRole('link', { name: 'Drivers', exact: true })
    .click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('heading', { name: 'Drivers', exact: true })).toBeVisible();
});

test('fleet pagination escapes names and suppresses late route results', async ({ page }) => {
  await setup(page);
  await page.route('**/api/v1/drivers?**', (r) =>
    r.fulfill({
      json: {
        items: [
          {
            id,
            displayName:
              new URL(r.request().url()).searchParams.get('page') === '2'
                ? 'Second driver'
                : '<img src=x onerror=alert(1)>',
            email: 'demo@example.com',
          },
        ],
        pageSize: 20,
        totalCount: 21,
      },
    }),
  );
  await page.goto('/admin/drivers');
  await expect(page.getByText('<img src=x onerror=alert(1)>', { exact: true })).toBeVisible();
  await expect(page.getByRole('table').locator('img')).toHaveCount(0);
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(page.getByText('Second driver', { exact: true })).toBeVisible();
  let release!: () => void;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/v1/drivers?**', async (r) => {
    await gate;
    await r.fulfill({ json: { items: [{ id, displayName: 'Late driver' }], totalCount: 1 } });
  });
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await page
    .getByRole('navigation', { name: 'Administration' })
    .getByRole('link', { name: 'Vehicles', exact: true })
    .click();
  release();
  await expect(page.getByText('No vehicles recorded.', { exact: true })).toBeVisible();
  await expect(page.getByText('Late driver', { exact: true })).toHaveCount(0);
});

test('users and roles is a read-only API-backed table', async ({ page }) => {
  await setup(page, ['authorization.read', 'users.manage']);
  await page.route('**/api/v1/admin/users?**', (r) =>
    r.fulfill({
      json: {
        items: [{ id, displayName: 'Alex Morgan', roles: ['Reader', 'Driver'] }],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
    }),
  );
  await page.goto('/admin/users');
  const table = page.getByRole('table', { name: 'Users and roles records', exact: true });
  await expect(table).toContainText('Alex Morgan');
  await expect(table).toContainText('Reader');
  await expect(table).toContainText('Driver');
  await expect(page.getByLabel('Action', { exact: true })).toHaveCount(0);
  await page.route('**/api/v1/admin/users?**', (r) =>
    r.fulfill({ status: 403, json: { status: 403 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Access denied');
  await expect(table).not.toContainText('Alex Morgan');
});
test('ontology opens a read-only fault-code relationship table with search and protected record navigation', async ({
  page,
}) => {
  await setup(page, ['authorization.read', 'knowledge.read']);
  const ontologyRequests: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/v1/ontology')) ontologyRequests.push(request.method());
  });
  await page.goto('/admin/ontology');
  const table = page.getByRole('table', { name: /Fault-code domain model/ });
  await expect(table).toBeVisible();
  for (const concept of ['Fault code', 'Incident', 'Vehicle', 'Driver', 'Notification'])
    await expect(table.getByText(concept, { exact: true }).first()).toBeVisible();
  await expect(page.getByLabel('Action', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Create|Publish|Update/ })).toHaveCount(0);
  await page.getByLabel('Find a relationship').fill('is addressed to');
  await expect(table.getByRole('row')).toHaveCount(2);
  await expect(table.getByText('Driver', { exact: true })).toBeVisible();
  await page.getByLabel('Find a relationship').fill('no-such-concept');
  await expect(table).toContainText('No matching relationships.');
  await page.getByLabel('Find a relationship').fill('');
  {
    const audit = await new AxeBuilder({ page }).analyze();
    expect(
      audit.violations.map((v) => ({ id: v.id, nodes: v.nodes.map((n) => n.failureSummary) })),
    ).toEqual([]);
  }
  expect(ontologyRequests.length).toBeGreaterThan(0);
  expect(ontologyRequests.every((method) => method === 'GET')).toBe(true);
  await page
    .getByRole('link', { name: 'Browse vehicle records for incident', exact: true })
    .click();
  await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
});

test('Incidents automatically lists vehicle reports and preserves unavailable, empty and review states', async ({
  page,
}) => {
  await setup(page);
  let requested = '';
  await page.route('**/api/v1/incidents?**', (r) => {
    requested = r.request().url();
    return r.fulfill({
      json: {
        items: [
          {
            id,
            code: 'SYN-EPS:UNKNOWN',
            system: 'EPS',
            status: 'Received',
            occurredAt: '2026-10-21T12:00:00Z',
            fleet: {
              vehicleId: id,
              driverId: id,
              vehicleIdentifier: 'SYN-VEH-1',
              driverIdentifier: 'SYN-DRIVER-1',
            },
            knowledge: null,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
    });
  });
  await page.goto('/admin/incidents');
  const table = page.getByRole('table', { name: 'Incidents records' });
  await expect(table).toContainText('SYN-EPS:UNKNOWN');
  await expect(table.getByRole('link', { name: 'SYN-VEH-1', exact: true })).toHaveAttribute(
    'href',
    '/admin/vehicles?vehicleId=' + id,
  );
  await expect(table.getByRole('link', { name: 'SYN-DRIVER-1', exact: true })).toHaveAttribute(
    'href',
    '/admin/drivers?driverId=' + id,
  );
  await expect(table.getByRole('link', { name: 'Notifications', exact: true })).toHaveAttribute(
    'href',
    '/admin/notifications?incidentId=' + id,
  );
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/incidents-table-test-fixture.png',
      fullPage: true,
    });
  expect(new URL(requested).searchParams.has('unmatchedOnly')).toBe(false);
  await expect(page.getByLabel('Action', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Create Incident', exact: true })).toHaveCount(0);
  {
    const audit = await new AxeBuilder({ page }).analyze();
    expect(
      audit.violations.map((v) => ({ id: v.id, nodes: v.nodes.map((n) => n.failureSummary) })),
    ).toEqual([]);
  }
  await page.route('**/api/v1/incidents?**', (r) =>
    r.fulfill({ status: 503, json: { status: 503 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByText('Incidents could not be loaded.', { exact: true })).toHaveCount(0);
  await expect(table).toBeVisible();
  await expect(table).not.toContainText('SYN-EPS:UNKNOWN');
  await expect(page.getByText('No incidents', { exact: true })).toHaveCount(0);
  await page.route('**/api/v1/incidents?**', (r) =>
    r.fulfill({ json: { items: [], page: 1, pageSize: 20, totalCount: 0 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(table.getByRole('row')).toHaveCount(1);
});

test('ontology remains read-only even for engineering publish permission', async ({ page }) => {
  await setup(page, ['authorization.read', 'knowledge.read', 'knowledge.publish']);
  await page.goto('/admin/ontology');
  await expect(page.getByRole('table', { name: /Fault-code domain model/ })).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Manage concepts & relationships', exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel('Action', { exact: true })).toHaveCount(0);
});

test('vehicle and driver lists show associations and email and dispatch the selected vehicle', async ({
  page,
}) => {
  await setup(page);
  const driverId = '66666666-6666-4666-8666-666666666666';
  const driver = {
    id: driverId,
    identifier: 'SYN-DRIVER-1',
    displayName: 'Synthetic Demo Driver',
    email: 'demo@example.com',
    emailNotifications: true,
    active: true,
  };
  const vehicle = { id, identifier: 'SYN-SILVERADO-1', system: 'EPS', active: true, version: 1 };
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({ json: { items: [vehicle], page: 1, pageSize: 20, totalCount: 1 } }),
  );
  await page.route('**/api/v1/vehicles/' + id + '/associations?**', (r) =>
    r.fulfill({
      json: {
        items: [{ id: 'association', vehicleId: id, driverId, until: null }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      },
    }),
  );
  await page.route('**/api/v1/drivers/' + driverId, (r) => r.fulfill({ json: driver }));
  await page.route('**/api/v1/drivers?**', (r) =>
    r.fulfill({ json: { items: [driver], page: 1, pageSize: 20, totalCount: 1 } }),
  );
  await page.route('**/api/v1/fault-code-catalog', (r) =>
    r.fulfill({
      json: {
        scope: 'Test catalog',
        items: [
          {
            code: 'SYN-EPS:ASSIST-SIGNAL',
            system: 'EPS',
            vehicles: ['Chevrolet Silverado', 'GMC Sierra'],
            description: 'Assist signal',
          },
          {
            code: 'SYN-SBW:LINK-TIMING',
            system: 'steer-by-wire',
            vehicles: ['Chevrolet Silverado'],
            description: 'Other system',
          },
        ],
      },
    }),
  );
  let incidentCount = 0;
  await page.route('**/api/v1/incidents?**', (r) =>
    r.fulfill({ json: { items: [], page: 1, pageSize: 1, totalCount: incidentCount } }),
  );
  await page.goto('/admin/vehicles');
  const table = page.getByRole('table', { name: 'Vehicles records' });
  await expect(table).toContainText('Chevrolet Silverado');
  await expect(table).not.toContainText('Synthetic');
  await expect(table.getByRole('columnheader', { name: 'Email', exact: true })).toBeVisible();
  await expect(table).toContainText('Demo Driver');
  await expect(table).toContainText('demo@example.com');
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/vehicles-list-test-fixture.png',
      fullPage: true,
    });
  for (const name of [
    'Fault definitions',
    'Documents',
    'Engineering',
    'Agent activity',
    'Operational health',
  ])
    await expect(
      page
        .getByRole('navigation', { name: 'Administration' })
        .getByRole('link', { name, exact: true }),
    ).toHaveCount(0);
  const dispatch = table.getByRole('button', { name: 'Dispatch', exact: true });
  const select = table.getByRole('combobox');
  await expect(dispatch).toBeDisabled();
  await expect(select.locator('option')).toHaveCount(2);
  await select.selectOption('SYN-EPS:ASSIST-SIGNAL');
  await expect(dispatch).toBeEnabled();
  const replayKeys: string[] = [];
  await page.route('**/api/v1/vehicle-fault-dispatches', (r) => {
    replayKeys.push(r.request().headers()['idempotency-key']);
    return r.fulfill({ status: 503, json: { status: 503 } });
  });
  await dispatch.click();
  await expect(
    page.getByText('Dispatch was not confirmed. Retry to check the same request.').first(),
  ).toBeVisible();
  await dispatch.click();
  await expect.poll(() => replayKeys.length).toBe(2);
  expect(replayKeys[0]).toBe(replayKeys[1]);
  let request: any;
  await page.route('**/api/v1/vehicle-fault-dispatches', (r) => {
    incidentCount = 1;
    request = { body: r.request().postDataJSON(), headers: r.request().headers() };
    return r.fulfill({
      status: 202,
      json: { incidentId: id, notificationId: driverId, state: 'Accepted' },
    });
  });
  await dispatch.click();
  await expect(page.getByText('Dispatched', { exact: true })).toBeVisible();
  expect(request.body).toEqual({ vehicleId: id, code: 'SYN-EPS:ASSIST-SIGNAL' });
  expect(request.headers['idempotency-key']).toBe(replayKeys[0]);
  await expect(select).toHaveValue('SYN-EPS:ASSIST-SIGNAL');
  await expect(dispatch).toBeEnabled();
  await expect(table.getByRole('link', { name: /View 1 incidents/ })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Back to vehicles' })).toHaveCount(0);
  await expect(page.getByText('Workspace actions', { exact: true })).toHaveCount(0);
  await page.goto('/admin/drivers');
  await expect(page.getByRole('table', { name: 'Drivers records' })).toContainText(
    'demo@example.com',
  );
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/drivers-list-test-fixture.png',
      fullPage: true,
    });
  await page.goto('/admin/health');
  await expect(page).toHaveURL(/admin\/incidents$/);
});

test('driver notification links filter the data table and show recorded delivery state', async ({
  page,
}) => {
  await setup(page);
  await page.route('**/api/v1/drivers?**', (r) =>
    r.fulfill({
      json: {
        items: [{ id, displayName: 'Alex Morgan', email: 'alex.morgan@example.com' }],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
    }),
  );
  const notificationRequests: string[] = [];
  await page.route('**/api/v1/notifications?**', (r) => {
    notificationRequests.push(r.request().url());
    return r.fulfill({
      json: {
        items: [
          {
            id,
            driverId: id,
            driverName: 'Alex Morgan',
            email: 'alex.morgan@example.com',
            message: 'Your fault is being investigated.',
            state: 'Pending',
            createdAt: '2026-10-21T12:00:00Z',
            attemptCount: 0,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      },
    });
  });
  await page.goto('/admin/drivers');
  await page.getByRole('link', { name: 'View notifications', exact: true }).click();
  const table = page.getByRole('table', { name: 'Driver notifications records', exact: true });
  await expect(table).toContainText('Alex Morgan');
  await expect(table).toContainText('alex.morgan@example.com');
  await expect(table).toContainText('Pending');
  expect(notificationRequests.some((url) => new URL(url).searchParams.get('driverId') === id)).toBe(
    true,
  );
  await page.getByRole('link', { name: 'Show all notifications', exact: true }).click();
  await expect(table).toContainText('Your fault is being investigated.');
  await expect
    .poll(() => new URL(notificationRequests.at(-1)!).searchParams.has('driverId'))
    .toBe(false);
  await page.route('**/api/v1/notifications?**', (r) =>
    r.fulfill({ status: 503, json: { status: 503 } }),
  );
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('unavailable');
  await expect(page.getByText('No driver notifications recorded.')).toHaveCount(0);
});

test('account avatar opens Spartan sign-out menu without an authentication badge', async ({
  page,
}) => {
  await setup(page);
  await page.goto('/admin/vehicles');
  await expect(page.getByText('Authenticated', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('menuitem', { name: 'Sign out', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: /^Account menu/ }).click();
  await expect(page.getByRole('menu', { name: 'Account', exact: true })).toContainText(
    'Bobby Wilson',
  );
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Sign in to continue', exact: true }),
  ).toBeVisible();
});

test('vehicle incident badges use filtered totals, disable zero or unavailable counts and preserve the filter when paging', async ({
  page,
}) => {
  await setup(page);
  const zeroId = '66666666-6666-4666-8666-666666666666';
  const unavailableId = '77777777-7777-4777-8777-777777777777';
  await page.route('**/api/v1/vehicles?**', (r) =>
    r.fulfill({
      json: {
        items: [
          { id, identifier: 'SYN-SILVERADO-1', system: 'EPS', active: true },
          { id: zeroId, identifier: 'SYN-SIERRA-1', system: 'EPS', active: true },
          { id: unavailableId, identifier: 'SYN-SIERRA-2', system: 'EPS', active: true },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 3,
      },
    }),
  );
  const requests: URL[] = [];
  await page.route('**/api/v1/incidents?**', (r) => {
    const url = new URL(r.request().url());
    requests.push(url);
    if (url.searchParams.get('vehicleId') === unavailableId)
      return r.fulfill({ status: 503, json: { status: 503 } });
    const count = url.searchParams.get('vehicleId') === id ? 21 : 0;
    return r.fulfill({
      json: {
        items: [],
        page: Number(url.searchParams.get('page')),
        pageSize: Number(url.searchParams.get('pageSize')),
        totalCount: count,
      },
    });
  });
  await page.goto('/admin/vehicles');
  const table = page.getByRole('table', { name: 'Vehicles records' });
  const rows = table.getByRole('row');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(1).getByRole('link', { name: /View 21 incidents/ })).toContainText('21');
  await expect(rows.nth(2).getByRole('button', { name: /View incidents/ })).toBeDisabled();
  await expect(rows.nth(2).locator('[data-slot="badge"]')).toHaveText('0');
  await expect(rows.nth(3).getByRole('button', { name: /View incidents/ })).toBeDisabled();
  await expect(rows.nth(3).locator('[data-slot="badge"]')).toHaveCount(0);
  expect(
    requests
      .filter((u) => u.searchParams.get('pageSize') === '1')
      .map((u) => u.searchParams.get('vehicleId'))
      .sort(),
  ).toEqual([id, zeroId, unavailableId].sort());
  await rows
    .nth(1)
    .getByRole('link', { name: /View 21 incidents/ })
    .click();
  await expect(page).toHaveURL(new RegExp('/admin/incidents\\?vehicleId=' + id));
  await expect(page.getByRole('button', { name: 'Next', exact: true })).toBeEnabled();
  expect(requests.at(-1)?.searchParams.get('vehicleId')).toBe(id);
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get('page')).toBe('2');
  expect(requests.at(-1)?.searchParams.get('vehicleId')).toBe(id);
  await page
    .getByRole('navigation', { name: 'Administration' })
    .getByRole('link', { name: 'Incidents', exact: true })
    .click();
  await expect(page).toHaveURL(/\/admin\/incidents$/);
  await expect.poll(() => requests.at(-1)?.searchParams.has('vehicleId')).toBe(false);
});
