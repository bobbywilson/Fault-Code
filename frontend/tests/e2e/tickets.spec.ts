import { test, expect, Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import operations from '../../src/app/operations.json';
const id = '55555555-5555-4555-8555-555555555555';
const related = '66666666-6666-4666-8666-666666666666';
const ticket = {
  id,
  identifier: 'FAU-23',
  title: 'Review the fault workflow',
  status: 'In Progress',
  statusType: 'started',
  priority: 'High',
  assignee: 'Bobby Wilson',
  project: 'Fault Code',
  labels: ['api'],
  createdAt: '2026-10-20T12:00:00Z',
  updatedAt: '2026-10-22T03:00:00Z',
  dueDate: '2026-10-25',
  completedAt: null,
  url: 'https://linear.app/fault-code/issue/FAU-23',
};
async function setup(page: Page) {
  await page.route('**/__test/session', (r) =>
    r.fulfill({
      json: {
        state: 'signed-in',
        permissions: [...new Set(operations.flatMap((o) => o.policies.flat()))],
      },
    }),
  );
  await page.route('**/workspace/tickets?**', (r) =>
    r.fulfill({
      json: {
        items: [
          ticket,
          {
            ...ticket,
            id: related,
            identifier: 'FAU-24',
            title: 'Document the service',
            status: 'Done',
            assignee: null,
          },
        ],
        team: 'Fault Code · FAU',
        retrievedAt: ticket.updatedAt,
      },
    }),
  );
  await page.route('**/workspace/tickets/' + id, (r) =>
    r.fulfill({
      json: {
        ticket,
        description:
          '## Acceptance criteria\n\n- [x] Store incident\n- [ ] Review notification\n\n![remote tracking image](https://tracking.example.com/a.png)\n\n<script>alert(1)</script>\n\n[Unsafe](javascript:alert(1))',
        creator: 'Bobby Wilson',
        cycle: 'October',
        estimate: 3,
        parent: null,
        subtasks: [
          {
            id: related,
            identifier: 'FAU-24',
            title: 'Document the service',
            status: 'Done',
            relationship: 'Subtask',
          },
        ],
        related: [],
        comments: [
          {
            id: 'comment',
            author: 'Nathan',
            body: 'Keep **ontology** evidence.',
            createdAt: ticket.createdAt,
            updatedAt: ticket.updatedAt,
          },
        ],
        attachments: [
          {
            title: 'Source pull request',
            subtitle: 'GitHub',
            url: 'https://github.com/bobbywilson/Fault-Code/pull/26',
          },
        ],
        retrievedAt: ticket.updatedAt,
      },
    }),
  );
}
test('Spartan ticket review filters and shows complete content without Linear login or external image requests', async ({
  page,
}) => {
  await setup(page);
  const outbound: string[] = [];
  page.on('request', (r) => {
    if (r.url().includes('tracking.example.com')) outbound.push(r.url());
  });
  await page.goto('/admin/tickets');
  await expect(page.getByRole('columnheader', { name: 'Assignee', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /FAU-23 · Review/ })).toBeVisible();
  await page.getByLabel('Status', { exact: true }).click();
  await page.getByRole('option', { name: 'Done', exact: true }).click();
  await expect(page.getByRole('link', { name: /FAU-23 · Review/ })).toHaveCount(0);
  await page.getByLabel('Status', { exact: true }).click();
  await page.getByRole('option', { name: 'All statuses', exact: true }).click();
  await page.getByLabel('Search tickets').fill('FAU-23');
  await expect(page.getByRole('link', { name: /FAU-24 · Document/ })).toHaveCount(0);
  await page.getByRole('link', { name: /FAU-23 · Review/ }).click();
  await expect(page).toHaveURL(new RegExp('ticketId=' + id));
  await expect(
    page.getByRole('heading', { name: 'Review the fault workflow', exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('region', { name: 'Ticket description' })).toContainText(
    'Store incident',
  );
  await expect(page.getByRole('region', { name: 'Ticket comments' })).toContainText(
    'Keep ontology evidence.',
  );
  await expect(page.getByText('October', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Source pull request' })).toBeVisible();
  await expect(page.getByRole('link', { name: /FAU-24 · Document/ })).toBeVisible();
  await expect(
    page.locator('article script, article img, article input, article a[href^="javascript:"]'),
  ).toHaveCount(0);
  expect(outbound).toEqual([]);
  await expect(page.locator('select')).toHaveCount(0);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('link', { name: 'Back to tickets' }).click();
  await expect(page.getByLabel('Search tickets')).toHaveValue('FAU-23');
});
test('ticket failures never look like an empty or successfully refreshed list', async ({
  page,
}) => {
  await setup(page);
  await page.route('**/workspace/tickets?**', (r) =>
    r.fulfill({ status: 503, json: { title: 'Tickets could not be retrieved.' } }),
  );
  await page.goto('/admin/tickets');
  await expect(page.getByRole('alert')).toContainText('Tickets could not be retrieved.');
  await expect(page.getByText('No matching tickets.')).toHaveCount(0);
});
test('late ticket responses cannot replace the current route', async ({ page }) => {
  await setup(page);
  let release!: () => void;
  const pending = new Promise<void>((r) => (release = r));
  await page.route('**/workspace/tickets/' + id, async (r) => {
    await pending;
    await r.fulfill({ status: 404, json: {} }).catch(() => {});
  });
  await page.goto('/admin/tickets?ticketId=' + id);
  await expect(page.getByText('Loading tickets…')).toBeVisible();
  await page.getByRole('link', { name: 'Back to tickets' }).click();
  release();
  await expect(page.getByRole('link', { name: /FAU-23 · Review/ })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
