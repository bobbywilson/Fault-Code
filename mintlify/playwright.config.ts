import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests', timeout: 60000, workers: 1, retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: 'http://localhost:5083', browserName: 'chromium', trace: 'retain-on-failure' },
  webServer: { command: 'npm run dev', url: 'http://localhost:5083', timeout: 180000, reuseExistingServer: !process.env['CI'] },
});
