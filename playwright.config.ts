import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: undefined,
  retries: 0,
  timeout: 90000,
  expect: { timeout: 20000 },
  use: {
    baseURL: 'http://localhost:5273',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'api',
      testMatch: 'crud-validation.spec.ts',
    },
    {
      name: 'e2e',
      testMatch: 'e2e.spec.ts',
    },
    {
      name: 'email',
      testMatch: 'email-certification.spec.ts',
    },
    {
      name: 'fase14',
      testMatch: 'fase14-federacion-identidades.spec.ts',
    },
  ],
});
