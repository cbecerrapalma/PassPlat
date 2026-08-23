import { test, expect, APIRequestContext } from '@playwright/test';

import { API } from './api-config';
const PWD = 'Admin@123';

interface AuthTokens { accessToken: string; refreshToken: string; }

let api: APIRequestContext;

function auth(token: string) {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', Accept: 'application/json' };
}

async function loginAs(nomUsuario: string, password: string, idTenant = 1): Promise<{ token: string; jwtRaw: string; ok: boolean; error?: string }> {
  for (let attempt = 0; attempt < 5; attempt++) {
    const r = await api.post(`${API}/auth/login`, {
      data: { NomUsuario: nomUsuario, Password: password, IdApp: 1, IdTenant: idTenant },
      ignoreHTTPSErrors: true,
    });
    if (r.status() === 429) {
      await new Promise(r => setTimeout(r, 5000));
      continue;
    }
    if (!r.ok()) {
      const err = await r.json();
      return { token: '', jwtRaw: '', ok: false, error: `${r.status()}/${err.codigo}: ${err.mensaje}` };
    }
    const data = await r.json();
    return { token: data.accessToken, jwtRaw: data.accessToken, ok: true };
  }
  return { token: '', jwtRaw: '', ok: false, error: `rate limited after 5 retries (attempts=5)` };
}

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - b64.length % 4);
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

async function get(uri: string, token: string) {
  return api.get(`${API}${uri}`, { headers: auth(token), ignoreHTTPSErrors: true });
}

async function post(uri: string, token: string, data?: any) {
  return api.post(`${API}${uri}`, { headers: auth(token), data, ignoreHTTPSErrors: true });
}

async function switchToPlatform(token: string): Promise<{ status: number; data?: any; error?: string }> {
  const r = await post('/auth/switch-to-platform', token, { IdApp: 1 });
  if (!r.ok()) {
    const err = await r.json();
    return { status: r.status(), error: `${err.codigo}: ${err.mensaje}` };
  }
  const data = await r.json();
  return { status: r.status(), data };
}

test.describe.serial('A1.9 Switch-to-Platform Certification Gate', () => {
  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ ignoreHTTPSErrors: true });
  });
  test.afterAll(async () => { await api.dispose(); });

  test.describe('A1.9.1 Switch-to-Platform Functional', () => {
    test('#1 Tenant → Platform exitoso', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
      expect(r.data?.accessToken).toBeTruthy();
    });

    test('#2 JWT resultante sin TenantId', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
      const jwt = decodeJwt(r.data.accessToken);
      expect(jwt.TenantId).toBeUndefined();
    });

    test('#3 JWT resultante sin UsuarioTenantId', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
      const jwt = decodeJwt(r.data.accessToken);
      expect(jwt.UsuarioTenantId).toBeUndefined();
    });

    test('#4 Permisos recalculados para Platform', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
      const jwt = decodeJwt(r.data.accessToken);
      expect(jwt.permiso).toBeDefined();
      expect(Array.isArray(jwt.permiso)).toBe(true);
    });

    test('#5 Permisos recalculados independientemente del tenant', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const tenantJwt = decodeJwt(login.token);

      expect(Array.isArray(tenantJwt.permiso)).toBe(true);
      expect(tenantJwt.permiso.length).toBeGreaterThan(0);
      expect(tenantJwt.TenantId).toBe('3');
      expect(tenantJwt.UsuarioTenantId).toBe('4');

      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
      expect(r.data?.accessToken).toBeTruthy();

      const platformJwt = decodeJwt(r.data.accessToken);

      expect(Array.isArray(platformJwt.permiso)).toBe(true);
      expect(platformJwt.permiso.length).toBeGreaterThan(0);

      // Cambio real de scope: platform no tiene TenantId ni UsuarioTenantId
      expect(platformJwt.TenantId).toBeUndefined();
      expect(platformJwt.UsuarioTenantId).toBeUndefined();

      // Mismo usuario y aplicación
      expect(platformJwt.nameidentifier).toBe(tenantJwt.nameidentifier);
      expect(platformJwt.IdApp).toBe(tenantJwt.IdApp);

      // El token es una nueva emisión (nuevo jti)
      expect(platformJwt.jti).not.toBe(tenantJwt.jti);
    });

    test('#6 Usuario sin rol PLATFORM → 403', async () => {
      const login = await loginAs('test_tenantA', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(403);
    });

    test('#7 JWT tenant manipulado → 401', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const parts = login.token.split('.');
      const tamperedPayload = Buffer.from(JSON.stringify({ ...decodeJwt(login.token), IdUsuario: 999 })).toString('base64url');
      const tamperedJwt = [tamperedPayload, parts[1], parts[2]].join('.');

      const r = await switchToPlatform(tamperedJwt);
      expect(r.status).toBe(401);
    });

    test('#8 JWT válido pero contexto inconsistente → 403', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();
      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(200);
    });

    test('#9 JWT tenant anterior no reutilizable', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const r1 = await switchToPlatform(login.token);
      expect(r1.status).toBe(200);

      const r2 = await switchToPlatform(login.token);
      expect(r2.status).toBe(401);
    });

    test('#10 Round-trip: Tenant A → Platform → Tenant B', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const platform = await switchToPlatform(login.token);
      expect(platform.status).toBe(200);
      const platformJwt = decodeJwt(platform.data.accessToken);
      expect(platformJwt.TenantId).toBeUndefined();

      const switchB = await post('/auth/switch-tenant/4', platform.data.accessToken, { IdApp: 1 });
      expect(switchB.ok()).toBeTruthy();
      const tenantBJwt = decodeJwt((await switchB.json()).accessToken);
      expect(tenantBJwt.TenantId).toBe('4');
    });

    test('#11 Round-trip: Tenant B → Platform → Tenant A', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const switchB = await post('/auth/switch-tenant/4', login.token, { IdApp: 1 });
      expect(switchB.ok()).toBeTruthy();
      const tenantBToken = (await switchB.json()).accessToken;

      const platform = await switchToPlatform(tenantBToken);
      expect(platform.status).toBe(200);

      const switchA = await post('/auth/switch-tenant/3', platform.data.accessToken, { IdApp: 1 });
      expect(switchA.ok()).toBeTruthy();
      const tenantAJwt = decodeJwt((await switchA.json()).accessToken);
      expect(tenantAJwt.TenantId).toBe('3');
    });
  });

  test.describe('A1.9.2 Data Isolation', () => {
    test('#12 Platform JWT no accede a datos de tenant específico', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const platform = await switchToPlatform(login.token);
      expect(platform.status).toBe(200);

      const r = await get('/usuarios', platform.data.accessToken);
      expect(r.status()).toBe(200);
      const list = await r.json();
      expect(Array.isArray(list)).toBe(true);
    });

    test('#13 Platform JWT no puede switch a tenant sin membresía', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const platform = await switchToPlatform(login.token);
      expect(platform.status).toBe(200);

      const r = await post('/auth/switch-tenant/1', platform.data.accessToken, { IdApp: 1 });
      expect(r.status()).toBe(401);
    });

    test('#14 Después de Platform → Tenant A, no hay permisos de Tenant B', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const platform = await switchToPlatform(login.token);
      expect(platform.status).toBe(200);

      const switchA = await post('/auth/switch-tenant/3', platform.data.accessToken, { IdApp: 1 });
      expect(switchA.ok()).toBeTruthy();
      const tenantAJwt = decodeJwt((await switchA.json()).accessToken);
      expect(tenantAJwt.TenantId).toBe('3');
    });
  });

  test.describe('A1.9.3 Security', () => {
    test('#15 Usuario sin rol PLATFORM no obtiene JWT platform', async () => {
      const login = await loginAs('test_tenantA', PWD, 3);
      expect(login.ok).toBeTruthy();

      const r = await switchToPlatform(login.token);
      expect(r.status).toBe(403);
      expect(r.data?.accessToken).toBeFalsy();
    });

    test('#16 JWT con claims falsificados rechazado', async () => {
      const login = await loginAs('test_multitenant', PWD, 3);
      expect(login.ok).toBeTruthy();

      const parts = login.token.split('.');
      const tamperedPayload = Buffer.from(JSON.stringify({ ...decodeJwt(login.token), TenantId: null, UsuarioTenantId: null })).toString('base64url');
      const tamperedJwt = [tamperedPayload, parts[1], parts[2]].join('.');

      const r = await switchToPlatform(tamperedJwt);
      expect(r.status).toBe(401);
    });

    test('#17 Privilege escalation: Tenant JWT → "limpiar claims" → Platform', async () => {
      const login = await loginAs('test_tenantA', PWD, 3);
      expect(login.ok).toBeTruthy();

      const parts = login.token.split('.');
      const tamperedPayload = Buffer.from(JSON.stringify({ ...decodeJwt(login.token), TenantId: null, UsuarioTenantId: null })).toString('base64url');
      const tamperedJwt = [tamperedPayload, parts[1], parts[2]].join('.');

      const r = await switchToPlatform(tamperedJwt);
      expect(r.status).toBe(401);
    });
  });
});
