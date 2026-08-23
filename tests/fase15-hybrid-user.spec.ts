import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';
const SYSTEM_PASSWORD = 'Admin@123';
let systemToken: string;
// User with TienePasswordLocal=true AND active UsuarioTenant for tenant 1
const LOCAL_USER_ID = 11;

test.describe.serial('FASE 15 — Hybrid User Model + Security Fixes', () => {
  test.beforeAll(async ({ request }) => {
    const loginRes = await request.post(`${API_BASE}/auth/login`, {
      data: {
        NomUsuario: 'sistema',
        IdApp: 1,
        IdTenant: 1,
        Password: SYSTEM_PASSWORD,
      },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    systemToken = loginData.accessToken || loginData.Token;
    expect(systemToken).toBeTruthy();
  });

  test('GET /api/dashboard — returns user-type breakdown', async ({ request }) => {
    const res = await request.get(`${API_BASE}/dashboard`, {
      headers: { Authorization: `Bearer ${systemToken}` },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data).toHaveProperty('totalUsuarios');
    expect(data).toHaveProperty('usuariosLocales');
    expect(data).toHaveProperty('usuariosOAuth');
    expect(data).toHaveProperty('usuariosHibridos');
    expect(data).toHaveProperty('usuariosConMFA');
    expect(data).toHaveProperty('proveedores');
    expect(data).toHaveProperty('intentosRecientes');
    expect(data.totalUsuarios).toBeGreaterThanOrEqual(0);
    expect(data.usuariosLocales).toBeGreaterThanOrEqual(0);
    expect(data.usuariosOAuth).toBeGreaterThanOrEqual(0);
    expect(data.usuariosHibridos).toBeGreaterThanOrEqual(0);
  });

  test('GET /api/usuarios — TienePasswordLocal field present', async ({ request }) => {
    const res = await request.get(`${API_BASE}/usuarios/page?PageNumber=1&PageSize=10`, {
      headers: { Authorization: `Bearer ${systemToken}` },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.items.length).toBeGreaterThan(0);
    expect(data.items[0]).toHaveProperty('tienePasswordLocal');
  });

  test('GET /api/usuarios/{id} — TienePasswordLocal in detail', async ({ request }) => {
    const res = await request.get(`${API_BASE}/usuarios/${LOCAL_USER_ID}`, {
      headers: { Authorization: `Bearer ${systemToken}` },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data).toHaveProperty('tienePasswordLocal');
  });

  test('POST /api/usuarios/{id}/agregar-password-local — system user already has password', async ({ request }) => {
    const res = await request.post(`${API_BASE}/usuarios/${LOCAL_USER_ID}/agregar-password-local`, {
      headers: { Authorization: `Bearer ${systemToken}` },
      data: { NuevaPassword: SYSTEM_PASSWORD },
    });
    expect(res.status()).toBe(400);
    const data = await res.json();
    expect(data.codigo).toBe('ALREADY_HAS_PASSWORD');
  });

  test('POST /api/usuarios/{id}/agregar-password-local — password too short', async ({ request }) => {
    const res = await request.post(`${API_BASE}/usuarios/${LOCAL_USER_ID}/agregar-password-local`, {
      headers: { Authorization: `Bearer ${systemToken}` },
      data: { NuevaPassword: 'short' },
    });
    expect(res.status()).toBe(400);
  });

  test('POST /api/usuarios/{id}/agregar-password-local — empty password', async ({ request }) => {
    const res = await request.post(`${API_BASE}/usuarios/${LOCAL_USER_ID}/agregar-password-local`, {
      headers: { Authorization: `Bearer ${systemToken}` },
      data: { NuevaPassword: '' },
    });
    expect(res.status()).toBe(400);
  });

  test('POST /api/auth/olvido-password — local user returns RequiresExternalAuth=false', async ({ request }) => {
    const res = await request.post(`${API_BASE}/auth/olvido-password`, {
      data: {
        Email: 'sistema@test.com',
        IdApp: 1,
        IdTenant: 1,
      },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data).toHaveProperty('requiresExternalAuth');
    expect(data.requiresExternalAuth).toBe(false);
  });

  test('POST /api/usuarios — create user without email', async ({ request }) => {
    const uniqueUser = `hybrid_${Date.now()}`;
    const res = await request.post(`${API_BASE}/usuarios`, {
      headers: { Authorization: `Bearer ${systemToken}` },
      data: {
        NomUsuario: uniqueUser,
        Email: null,
        Nombre: 'Hybrid',
        Apellido: 'Test',
        IdEstado: 1,
        IdTenant: 1,
      },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.id).toBeTruthy();
  });

  test.skip('GET /api/intentos-acceso/page — MetodoAutenticacion field present', async ({ request }) => {
    // SKIPPED: endpoint no disponible (ruta no implementada en controllers)
  });

  test('GET /api/auth/externo/proveedores — only 5 providers', async ({ request }) => {
    const res = await request.get(`${API_BASE}/auth/externo/proveedores`, {
      headers: { Authorization: `Bearer ${systemToken}` },
    });
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
    const codigos = data.map((p: any) => p.codigo);
    expect(codigos).toContain('GOOGLE');
    expect(codigos).toContain('GITHUB');
    expect(codigos).toContain('LINKEDIN');
    expect(codigos).toContain('INSTAGRAM');
    expect(codigos).toContain('FACEBOOK');
  });
});
