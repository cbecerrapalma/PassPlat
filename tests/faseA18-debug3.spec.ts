import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';
const PWD = 'B7$k9mX!pW2@nR';

test('Login test_tenantA', async ({ request }) => {
  const r = await request.post(`${API}/auth/login`, {
    headers: { 'Content-Type': 'application/json' },
    data: { NomUsuario: 'test_tenantA', Password: PWD, IdApp: 1, IdTenant: 1 },
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  const text = await r.text();
  console.log(`Body: ${text}`);
});

test('Login test_tenantB', async ({ request }) => {
  const r = await request.post(`${API}/auth/login`, {
    headers: { 'Content-Type': 'application/json' },
    data: { NomUsuario: 'test_tenantB', Password: PWD, IdApp: 1, IdTenant: 1 },
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  const text = await r.text();
  console.log(`Body: ${text}`);
});

test('Login test_multitenant with accept header', async ({ request }) => {
  const r = await request.post(`${API}/auth/login`, {
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    data: { NomUsuario: 'test_multitenant', Password: PWD, IdApp: 1, IdTenant: 1 },
    ignoreHTTPSErrors: true,
  });
  console.log(`Status: ${r.status()}`);
  const text = await r.text();
  console.log(`Body: ${text}`);
});
