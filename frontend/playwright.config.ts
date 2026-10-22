import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: 'tests/e2e',
  fullyParallel: true,
  retries: 0,
  // The isolated Linux workers share two CPUs across build jobs.
  workers: process.env['CI'] ? 1 : undefined,
  expect: { timeout: process.env['CI'] ? 15000 : 5000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://127.0.0.1:4200', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run start -- --host 127.0.0.1 --configuration e2e',
    url: 'http://127.0.0.1:4200',
    reuseExistingServer: false,
    timeout: 120000,
  },
});
