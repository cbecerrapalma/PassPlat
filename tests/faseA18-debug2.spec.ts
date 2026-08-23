import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';
const PWD = 'B7$k9mX!pW2@nR';

test('DEBUG: login test_multitenant with explicit JSON', async ({ request }) => {
  // Try with explicit Content-Type
  const r = await request.post(`${API}/auth/login`, {
    headers: { 'Content-Type': 'application/json' },
    data: JSON.stringify({ NomUsuario: 'test_multitenant', Password: PWD, IdApp: 1, IdTenant: 1 }),
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  const body = await r.text();
  console.log(`Body: ${body}`);
  expect(r.ok()).toBeTruthy();
});

test('DEBUG: login platform_admin', async ({ request }) => {
  const r = await request.post(`${API}/auth/login`, {
    headers: { 'Content-Type': 'application/json' },
    data: JSON.stringify({ NomUsuario: 'platform_admin', Password: 'Admin@123', IdApp: 1, IdTenant: 1 }),
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  const body = await r.text();
  console.log(`Body: ${body}`);
  expect(r.ok()).toBeTruthy();
});
