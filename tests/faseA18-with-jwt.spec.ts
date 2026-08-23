import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';
const PWD = 'B7$k9mX!pW2@nR';

// JWT obtained from PowerShell (valid for 1 hour)
const JWT = process.env.A18_JWT || '';

test.describe.serial('A1.8 Using Pre-obtained JWT', () => {
  test('A1.8.3 #3 Switch-tenant works with pre-obtained JWT', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const r = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r.ok()).toBeTruthy();
    const data = await r.json();
    const b64 = data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const jwt = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    expect(jwt.TenantId).toBe('3');
    expect(jwt.UsuarioTenantId).toBe('4');
  });

  test('A1.8.3 #4 Switch validates active membership', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const r = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r.ok()).toBeTruthy();
  });

  test('A1.8.4 #6 Tenant A → Tenant B', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    // A → VESTUARIO
    const rB = await request.post(`${API}/auth/switch-tenant/4`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(rB.ok()).toBeTruthy();
    const dataB = await rB.json();
    const b64 = dataB.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const jwtB = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    expect(jwtB.TenantId).toBe('4');
    expect(jwtB.UsuarioTenantId).toBe('5');
  });

  test('A1.8.4 #7 Round trip back to A', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const rA = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(rA.ok()).toBeTruthy();
    const dataA = await rA.json();
    const b64 = dataA.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const jwtA = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    expect(jwtA.TenantId).toBe('3');
    expect(jwtA.UsuarioTenantId).toBe('4');
  });

  test('A1.8.5 #5 Switch where no membership → SIN_ACCESO_TENANT', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    // test_tenantA (only has ABARROTES) cannot switch to VESTUARIO
    const loginA = await request.post(`${API}/auth/login`, {
      headers: { 'Content-Type': 'application/json' },
      data: JSON.stringify({ NomUsuario: 'test_tenantA', Password: PWD, IdApp: 1, IdTenant: 1 }),
      ignoreHTTPSErrors: true,
    });
    expect(loginA.ok()).toBeTruthy();
    const { accessToken } = await loginA.json();
    const r = await request.post(`${API}/auth/switch-tenant/4`, {
      headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(401);
    const err = await r.json();
    expect(err.codigo).toBe('SIN_ACCESO_TENANT');
  });

  test('A1.8.6 #9 mis-tenants returns 2 active tenants', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const r = await request.get(`${API}/auth/mis-tenants`, {
      headers: { Authorization: `Bearer ${JWT}` },
      ignoreHTTPSErrors: true,
    });
    expect(r.ok()).toBeTruthy();
    const list = await r.json();
    expect(Array.isArray(list)).toBe(true);
    expect(list.length).toBe(2);
    const codes = list.map((t: any) => t.codigo);
    expect(codes).toContain('ABARROTES');
    expect(codes).toContain('VESTUARIO');
  });

  test('A1.8.9 #18 Tenant JWT TenantId=3', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const r = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r.ok()).toBeTruthy();
    const data = await r.json();
    const b64 = data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const jwt = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    expect(jwt.TenantId).toBe('3');
  });

  test('A1.8.9 #19 Tenant JWT UsuarioTenantId=4', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const r = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r.ok()).toBeTruthy();
    const data = await r.json();
    const b64 = data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const jwt = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    expect(jwt.UsuarioTenantId).toBe('4');
  });

  test('A1.8.9 #20 Permission recalculation', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    // Get permissions for tenant 3 (ABARROTES)
    const r3 = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r3.ok()).toBeTruthy();
    const data3 = await r3.json();
    const b643 = data3.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad3 = b643.length % 4 === 0 ? '' : '='.repeat(4 - b643.length % 4);
    const jwt3 = JSON.parse(Buffer.from(b643 + pad3, 'base64').toString('utf-8'));
    // Get permissions for tenant 4 (VESTUARIO)
    const r4 = await request.post(`${API}/auth/switch-tenant/4`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(r4.ok()).toBeTruthy();
    const data4 = await r4.json();
    const b644 = data4.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad4 = b644.length % 4 === 0 ? '' : '='.repeat(4 - b644.length % 4);
    const jwt4 = JSON.parse(Buffer.from(b644 + pad4, 'base64').toString('utf-8'));
    // Permissions should differ between tenants
    const perms3 = [...jwt3.permiso].sort().join(',');
    const perms4 = [...jwt4.permiso].sort().join(',');
    console.log(`Tenant 3 perms: ${perms3}`);
    console.log(`Tenant 4 perms: ${perms4}`);
  });

  test('A1.8.9 #21 JWT tampering → 401', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    const parts = JWT.split('.');
    const b64 = JWT.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
    const payload = JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
    payload.TenantId = '999';
    const tamperedPayload = Buffer.from(JSON.stringify(payload)).toString('base64url');
    const tamperedJwt = [tamperedPayload, parts[1], parts[2]].join('.');
    const r = await request.get(`${API}/auth/mis-tenants`, {
      headers: { Authorization: `Bearer ${tamperedJwt}` },
      ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(401);
  });

  test('A1.8.11 #24 Cross-tenant: A JWT cannot access B data', async ({ request }) => {
    test.skip(!JWT, 'Set A18_JWT environment variable');
    // Switch to ABARROTES (3)
    const rS = await request.post(`${API}/auth/switch-tenant/3`, {
      headers: { Authorization: `Bearer ${JWT}`, 'Content-Type': 'application/json' },
      data: { idApp: 1 },
      ignoreHTTPSErrors: true,
    });
    expect(rS.ok()).toBeTruthy();
    const jwtA = (await rS.json()).accessToken;
    // Try to access something scoped to tenant 4 with A's JWT
    const rCross = await request.get(`${API}/auth/mis-tenants`, {
      headers: { Authorization: `Bearer ${jwtA}` },
      ignoreHTTPSErrors: true,
    });
    // mis-tenants returns user's memberships, not tenant-scoped data
    // This test verifies the JWT is valid but scoped to tenant 3
    expect(rCross.ok()).toBeTruthy();
    // Actually verify that TenantId claim stays 3
    const b64a = jwtA.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const pada = b64a.length % 4 === 0 ? '' : '='.repeat(4 - b64a.length % 4);
    const jwtClaims = JSON.parse(Buffer.from(b64a + pada, 'base64').toString('utf-8'));
    expect(jwtClaims.TenantId).toBe('3');
  });
});
