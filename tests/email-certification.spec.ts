import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';

test.describe('Email Certification — FASE 7', () => {
  let authToken: string;

  test.beforeAll(async ({ request }) => {
    const res = await request.post(`${API_BASE}/auth/login`, {
      data: { NomUsuario: 'sistema', Password: 'Admin@123', IdApp: 1, IdTenant: 1 }
    });
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    authToken = body.accessToken || body.token;
    expect(authToken).toBeTruthy();
  });

  test('1. GET /api/EmailLog/pendientes — endpoint funciona', async ({ request }) => {
    const res = await request.get(`${API_BASE}/EmailLog/pendientes`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(Array.isArray(body)).toBeTruthy();
  });

  test('2. GET /api/EmailLog/usuario/1 — usuario existe', async ({ request }) => {
    const res = await request.get(`${API_BASE}/EmailLog/usuario/1`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    expect(res.ok()).toBeTruthy();
  });

  test('3. Acción login no rompe pipeline email', async ({ request }) => {
    const res1 = await request.get(`${API_BASE}/EmailLog/pendientes`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    expect(res1.ok()).toBeTruthy();
    const before = await res1.json();
    const countBefore = Array.isArray(before) ? before.length : 0;

    const res2 = await request.post(`${API_BASE}/auth/login`, {
      data: { NomUsuario: 'sistema', Password: 'Admin@123', IdApp: 1, IdTenant: 1 }
    });
    expect(res2.ok()).toBeTruthy();

    await new Promise(r => setTimeout(r, 3000));

    const res3 = await request.get(`${API_BASE}/EmailLog/pendientes`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    const after = await res3.json();
    const countAfter = Array.isArray(after) ? after.length : 0;
    expect(countAfter).toBeGreaterThanOrEqual(countBefore);
  });

  test('4. Login fallido (múltiple) no rompe pipeline', async ({ request }) => {
    for (let i = 0; i < 3; i++) {
      await request.post(`${API_BASE}/auth/login`, {
        data: { NomUsuario: 'sistema', Password: `WrongPass${i}`, IdApp: 1 }
      });
    }
    await new Promise(r => setTimeout(r, 2000));

    const res = await request.get(`${API_BASE}/EmailLog/pendientes`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    expect(res.ok()).toBeTruthy();
  });

  test('5. EmailLog pendientes retorna array (integridad pipeline)', async ({ request }) => {
    const res = await request.get(`${API_BASE}/EmailLog/pendientes`, {
      headers: { Authorization: `Bearer ${authToken}` }
    });
    expect(res.ok()).toBeTruthy();
    const logs = await res.json();
    expect(Array.isArray(logs)).toBeTruthy();
    if (logs.length > 0) {
      const log = logs[0];
      expect(log).toHaveProperty('id');
      expect(log).toHaveProperty('destinatario');
      expect(log).toHaveProperty('estado');
      expect(['enviado', 'pendiente', 'fallido', 'rebotado']).toContain(log.estado);
    }
  });
});
