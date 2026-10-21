import { test, expect, Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import operations from '../../src/app/operations.json';
const id = '55555555-5555-4555-8555-555555555555';
const all = [...new Set(operations.flatMap((o) => o.policies.flat()))];
async function setup(page: Page, permissions = all) {
  await page.route('**/__test/session', (r) => r.fulfill({ json: { state: 'signed-in' } }));
  await page.route('**/api/v1/**', (r) =>
    r.fulfill({
      json: r.request().url().endsWith('/authorization/me')
        ? { callerKind: 'delegated', permissions }
        : { items: [], page: 1, pageSize: 20, totalCount: 0 },
      headers: { 'X-Correlation-ID': 'synthetic-reference' },
    }),
  );
}
async function action(page: Page, area: string, id: string) {
  await page.goto('/admin/' + area);
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
test('all areas map real protected operations, Reader cannot mutate', async ({ page }) => {
  await setup(page, ['authorization.read', 'vehicles.read']);
  await action(page, 'vehicles', 'CreateVehicle');
  await expect(page.getByRole('alert')).toContainText('Access denied');
  await expect(page.locator('button[type=submit]')).toHaveCount(0);
  await page.getByRole('link', { name: 'Users & roles', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Users & roles', exact: true })).toBeVisible();
});
test('recorded pagination, loading, empty, forbidden and unavailable never fabricate success', async ({
  page,
}) => {
  await setup(page);
  await action(page, 'vehicles', 'ListVehicles');
  await submit(page);
  await expect(page.getByText('No recorded items.')).toBeVisible();
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/spartan-vehicles-desktop-fixture.png',
      fullPage: true,
    });
  await page.route('**/api/v1/vehicles*', (r) => r.fulfill({ status: 503, json: { status: 503 } }));
  await submit(page);
  await expect(page.getByRole('alert')).toContainText('service is unavailable');
  await expect(page.getByRole('region', { name: 'Recorded results' })).toHaveCount(0);
  await page.route('**/api/v1/vehicles*', (r) => r.fulfill({ status: 403, json: { status: 403 } }));
  await submit(page);
  await expect(page.getByRole('alert')).toContainText('Access denied');
});
test('401 clears records and requires sign-in without automatic mutation retry', async ({
  page,
}) => {
  await setup(page);
  await action(page, 'vehicles', 'ListVehicles');
  await submit(page);
  await page.route('**/api/v1/vehicles*', (r) => r.fulfill({ status: 401, json: { status: 401 } }));
  await submit(page);
  await expect(page.getByRole('heading', { name: 'Your session has expired' })).toBeVisible();
  await expect(page.getByText('No recorded items.')).toHaveCount(0);
});
test('conflicts block stale resubmission and repeated outage retry retains replay key', async ({
  page,
}) => {
  await setup(page);
  await action(page, 'vehicles', 'CreateVehicle');
  await page.getByLabel('Identifier').fill('SYN-001');
  await page.getByLabel('System').fill('EPS');
  const keys: string[] = [];
  await page.route('**/api/v1/vehicles', (r) => {
    keys.push(r.request().headers()['idempotency-key']);
    return r.fulfill({ status: 503, json: { status: 503 } });
  });
  await submit(page);
  await expect(page.getByRole('alert')).toContainText('unavailable');
  await submit(page);
  await expect.poll(() => keys.length).toBe(2);
  expect(keys[0]).toBe(keys[1]);
  await page.route('**/api/v1/vehicles', (r) => r.fulfill({ status: 409, json: { status: 409 } }));
  await submit(page);
  await expect(page.getByRole('alert')).toContainText('Reload the current record');
  await expect(page.locator('button[type=submit]')).toBeDisabled();
});
test('three workflows invoke authoritative investigation, approval and notification operations', async ({
  page,
}) => {
  await setup(page);
  const requests: { url: string; method: string; body: any; headers: any }[] = [];
  await page.route('**/api/v1/**', (r) => {
    const q = r.request();
    if (q.url().endsWith('/authorization/me'))
      return r.fulfill({ json: { callerKind: 'delegated', permissions: all } });
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
  // Workflow C: engineer approval remains an explicit protected action, separate from proposed knowledge.
  await action(page, 'engineering', 'ApproveEngineeringResolution');
  await page.getByLabel('Id *', { exact: true }).fill(id);
  await page.getByLabel('Current record version').fill('1');
  // Fill required approval fields using contract metadata rather than inventing another API.
  const approval = operations.find((o) => o.id === 'ApproveEngineeringResolution')!;
  for (const field of Object.keys((approval.body as any)?.properties ?? {})) {
    const control = page.locator('#body-' + field);
    if (await control.count())
      await control.fill(
        field.toLowerCase().includes('id')
          ? id
          : field.toLowerCase().includes('version')
            ? '1'
            : 'Synthetic approved evidence',
      );
  }
  await submit(page);
  await expect(page.getByRole('status')).toContainText('request completed');
  expect(requests.at(-1)?.url).toContain('/approvals');
  await action(page, 'notifications', 'DispatchNotification');
  await page.getByLabel('Id *', { exact: true }).fill(id);
  await page.getByLabel('Current record version').fill('1');
  await submit(page);
  await expect(page.getByRole('status')).toContainText('request completed');
  expect(requests.at(-1)?.url).toContain('/dispatches');
});
test('mobile keyboard navigation, focus and accessible fields', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await setup(page);
  await action(page, 'vehicles', 'CreateVehicle');
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/spartan-vehicle-form-mobile-fixture.png',
      fullPage: true,
    });
  await page.keyboard.press('Tab');
  await page.getByLabel('Identifier').fill('SYN-001');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const a = await new AxeBuilder({ page }).analyze();
  expect(a.violations).toEqual([]);
  await page.getByRole('button', { name: 'Toggle Sidebar' }).click();
  await page.getByRole('link', { name: 'Engineering', exact: true }).click();
  await expect(page.locator('#main')).toBeFocused();
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/administration-mobile-fixture.png',
      fullPage: true,
    });
});
test('recorded list opens real detail, paginates, escapes returned markup and suppresses late route results', async ({
  page,
}, info) => {
  await setup(page);
  await action(page, 'vehicles', 'ListVehicles');
  await page.route('**/api/v1/vehicles**', (r) => {
    const u = new URL(r.request().url());
    return r.fulfill({
      json: u.pathname.endsWith(id)
        ? { id, identifier: 'SYN-021', system: 'EPS', version: 3 }
        : {
            items: [
              {
                id,
                identifier: u.searchParams.get('page') === '2' ? 'SYN-021' : 'SYN-001',
                system: 'EPS',
                version: 3,
              },
            ],
            page: Number(u.searchParams.get('page') ?? 1),
            pageSize: 20,
            totalCount: 21,
          },
      headers: { ETag: '"3"' },
    });
  });
  await submit(page);
  await expect(page.getByText('SYN-001', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(page.getByText('SYN-021', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Next', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Open record' }).click();
  await expect(page.getByLabel('Action', { exact: true })).toHaveValue('GetVehicle');
  await expect(page.getByText('SYN-021', { exact: true })).toBeVisible();
  await page.getByLabel('Action', { exact: true }).selectOption('UpdateVehicle');
  await expect(page.getByLabel('Current record version')).toHaveValue('3');
  await expect(page.getByLabel('Id *', { exact: true })).toHaveValue(id);
  await page.getByLabel('Identifier').fill('SYN-021');
  await page.getByLabel('System').fill('EPS');
  if (process.env['FAULTCODE_QA_DIR'])
    await page.screenshot({
      path: process.env['FAULTCODE_QA_DIR'] + '/administration-desktop-fixture.png',
      fullPage: true,
    });
  await action(page, 'vehicles', 'GetVehicle');
  await page.getByLabel('Id *', { exact: true }).fill(id);
  await page.route('**/api/v1/vehicles/' + id, (r) =>
    r.fulfill({ json: { id, identifier: '<img src=x onerror=alert(1)>', version: 1 } }),
  );
  await submit(page);
  await expect(page.getByText('<img src=x onerror=alert(1)>', { exact: true })).toBeVisible();
  await expect(page.locator('main img')).toHaveCount(0);
  let release!: () => void;
  const gate = new Promise<void>((r) => (release = r));
  await page.route('**/api/v1/vehicles/' + id, async (r) => {
    await gate;
    await r.fulfill({ json: { id, identifier: 'Late response' } });
  });
  await submit(page);
  await expect(page.getByRole('status')).toHaveText('Loading…');
  await page.getByRole('button', { name: 'Toggle Sidebar' }).click();
  await page.getByRole('link', { name: 'Engineering', exact: true }).click();
  release();
  await expect(page.getByText('Late response', { exact: true })).toHaveCount(0);
});
test('least privilege scope conjunctions for Operator, Engineer, Administrator and own-only Reader', async ({
  page,
}) => {
  for (const [permissions, area, operation, denied] of [
    [['authorization.read', 'vehicles.read.own'], 'vehicles', 'ListVehicles', false],
    [['authorization.read', 'vehicles.write'], 'vehicles', 'CreateVehicle', false],
    [
      ['authorization.read', 'knowledge.publish'],
      'engineering',
      'ApproveEngineeringResolution',
      true,
    ],
    [
      [
        'authorization.read',
        'knowledge.publish',
        'escalations.write',
        'faults.read',
        'vehicles.read',
        'knowledge.read',
      ],
      'engineering',
      'ApproveEngineeringResolution',
      false,
    ],
    [['authorization.read', 'users.manage'], 'users', 'ReplaceManagedUserRoles', false],
    [['authorization.read', 'integrations.manage'], 'users', 'ReplaceManagedUserRoles', true],
  ] as [string[], string, string, boolean][]) {
    await setup(page, permissions);
    await action(page, area, operation);
    if (denied) await expect(page.getByRole('alert')).toContainText('Access denied');
    else await expect(page.locator('button[type=submit]')).toBeVisible();
  }
});
test('engineering proposal supports nested evidence and nullable approved patch metadata', async ({
  page,
}) => {
  await setup(page);
  await action(page, 'engineering', 'ProposeEngineeringResolution');
  await page.getByLabel('Id *', { exact: true }).fill(id);
  await page.getByLabel('Current record version').fill('1');
  await page.locator('#body-content-description').fill('Synthetic engineering finding');
  await page.locator('#body-content-classificationId').fill('SYN-CLASS');
  await page.locator('#body-content-classificationVersion').fill('2');
  await page.locator('#body-expectedFaultVersion').fill('0');
  await page.getByRole('button', { name: 'Add Symptoms', exact: true }).click();
  await page.locator('#body-content-symptoms-0').fill('SYN-SYM');
  await page.getByRole('button', { name: 'Add References', exact: true }).click();
  await page.locator('#body-content-references-0-id').fill('SYN-DOC');
  await page.locator('#body-content-references-0-version').fill('1');
  await page.getByRole('button', { name: 'Add Remedies', exact: true }).click();
  for (const [key, value] of Object.entries({
    id: 'SYN-REMEDY',
    kind: 'guidance',
    description: 'Synthetic inspection',
    model: 'SYN-MODEL',
    softwareVersion: '1.0',
  }))
    await page.locator('#body-content-remedies-0-' + key).fill(value);
  await page.locator('#body-content-remedies-0-approved').click();
  await expect(page.locator('#body-content-remedies-0-approved')).toBeChecked();
  let payload: any;
  await page.route('**/api/v1/engineering-escalations/' + id + '/resolutions', (r) => {
    payload = r.request().postDataJSON();
    return r.fulfill({ json: { id, version: 2, state: 'PendingApproval' } });
  });
  await submit(page);
  await expect(page.getByRole('status')).toContainText('request completed');
  expect(payload.content.remedies[0].patch).toBeNull();
  expect(payload.content.references).toEqual([{ id: 'SYN-DOC', version: '1' }]);
  // A second proposal can explicitly include patch metadata through typed fields.
  await page.getByLabel('Action', { exact: true }).selectOption('GetEngineeringCase');
  await page.getByLabel('Action', { exact: true }).selectOption('ProposeEngineeringResolution');
  await page.getByRole('button', { name: 'Add Remedies', exact: true }).click();
  await page.getByRole('checkbox', { name: 'Include Patch' }).click();
  await expect(page.getByRole('checkbox', { name: 'Include Patch' })).toBeChecked();
  await expect(page.locator('#body-content-remedies-0-patch-reference')).toBeVisible();
  await page.getByRole('checkbox', { name: 'Include Patch' }).click();
  await expect(page.getByRole('checkbox', { name: 'Include Patch' })).not.toBeChecked();
  await expect(page.locator('#body-content-remedies-0-patch-reference')).toHaveCount(0);
});
