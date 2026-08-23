import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';
const PWD = 'B7$k9mX!pW2@nR';

test('DEBUG: login test_multitenant', async ({ request }) => {
  const r = await request.post(`${API}/auth/login`, {
    data: { NomUsuario: 'test_multitenant', Password: PWD, IdApp: 1, IdTenant: 1 },
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  console.log(`Headers: ${JSON.stringify(r.headers())}`);
  const body = await r.text();
  console.log(`Body: ${body}`);
  expect(r.ok()).toBeTruthy();
});
