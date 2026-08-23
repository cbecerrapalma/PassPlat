import { test, expect, APIRequestContext } from '@playwright/test';

import { API } from './api-config';

const PWD = 'Admin@123';

interface TokenInfo { accessToken: string; refreshToken: string; }

let api: APIRequestContext;
let token: TokenInfo;
let correlationId: string;

function bearer(t: string) {
  return { Authorization: `Bearer ${t}`, Accept: 'application/json' };
}

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

async function loginPlatform(): Promise<{ ok: boolean; data?: any; error?: string }> {
  const r = await api.post(`${API}/auth/login/platform`, { data: { NomUsuario: 'platform_admin', Password: PWD, IdApp: 1 }, ignoreHTTPSErrors: true });
  if (!r.ok()) {
    const err = await r.json();
    return { ok: false, error: `${r.status()}/${err.codigo}: ${err.mensaje}` };
  }
  return { ok: true, status: r.status(), data: await r.json() };
}

test.describe.serial('Gate C — S16.4 Observability Contract (E2E)', () => {
  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    const l = await loginPlatform();
    expect(l.ok, l.error).toBeTruthy();
    token = { accessToken: l.data.accessToken, refreshToken: l.data.refreshToken };
  });

  test.afterAll(async () => { await api.dispose(); });

  // ── C1.1 Login → JWT Generated ──
  test('C1.1 Login /platform → JWT + headers de correlación', async () => {
    const r = await api.post(`${API}/auth/login/platform`, { data: { NomUsuario: 'platform_admin', Password: PWD, IdApp: 1 }, ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
    correlationId = r.headers()['x-correlation-id'] ?? '';
    expect(correlationId.length).toBeGreaterThan(0);

    const body = await r.json();
    expect(body.accessToken).toBeTruthy();
    expect(body.refreshToken).toBeTruthy();
    expect(body.idUsuario).toBe(2);
  });

  test('C1.2 JWT: claims del contrato (jti/permiso/iss/aud/exp)', async () => {
    const claims = decodeJwt(token.accessToken);
    expect(claims.jti).toBeTruthy();
    expect(Array.isArray(claims.permiso)).toBe(true);
    expect(claims.permiso.length).toBeGreaterThan(0);
    expect(claims.iss).toBe('PassPlat');
    expect(claims.aud).toBe('PassPlat');
    expect(claims.exp).toBeGreaterThan(claims.iat);
    expect(claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']).toBeTruthy();
  });

  // ── C1.3 Jwt_Validated: limitación conocida (framework CBP) ──
  test('C1.3 Endpoint protegido validado por JWT → HTTP 200', async () => {
    const r = await api.get(`${API}/apps`, { headers: bearer(token.accessToken), ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
  });

  // ── C1.4 Login rechazado → Login_Failed + access denegado ──
  test('C1.4 Login inválido → 4xx (Login_Failed)', async () => {
    const r = await api.post(`${API}/auth/login/platform`, { data: { NomUsuario: 'platform_admin', Password: 'WrongPass1!', IdApp: 1 }, ignoreHTTPSErrors: true });
    expect(r.status()).toBeGreaterThanOrEqual(400);
    expect(r.status()).toBeLessThan(500);
  });

  // ── C1.5 Refresh token válido → RefreshToken_Issued (login propio, sin tocar token global) ──
  test('C1.5 Refresh válido → 200 (RefreshToken_Issued)', async () => {
    const l = await api.post(`${API}/auth/login/platform`, { data: { NomUsuario: 'platform_admin', Password: PWD, IdApp: 1 }, ignoreHTTPSErrors: true });
    expect(l.status()).toBe(200);
    const fresh = await l.json();
    const r = await api.post(`${API}/auth/refresh`, { data: { refreshToken: fresh.refreshToken }, ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(body.accessToken).toBeTruthy();
  });

  // ── C2 Cache: MISS → HIT (verificado en log post-run) ──
  test('C2.1 /apps/activas (1ª): Miss → carga SQL', async () => {
    const r = await api.get(`${API}/apps/activas`, { ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
    const list = await r.json();
    expect(Array.isArray(list)).toBe(true);
    expect(list.length).toBeGreaterThan(0);
  });

  test('C2.2 /apps/activas (2ª): Hit en memoria', async () => {
    const r = await api.get(`${API}/apps/activas`, { ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
    const list = await r.json();
    expect(Array.isArray(list)).toBe(true);
    expect(list.length).toBeGreaterThan(0);
  });

  // ── C3 Invalidation: crear App → wipe del cache de catálogo ──
  test('C3.1 Crear App → invalida app:catalog:activas', async () => {
    const stamp = Date.now().toString(36);
    const r = await api.post(`${API}/apps`, {
      headers: bearer(token.accessToken),
      data: { codigo: `GC_${stamp}`.toUpperCase(), nombre: `GateC App ${stamp}`, urlBase: 'https://example.test' },
      ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(201);
  });

  test('C3.2 Tras invalidación: /apps/activas refleja la nueva App', async () => {
    const r = await api.get(`${API}/apps/activas`, { ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
    const list = await r.json();
    expect(Array.isArray(list)).toBe(true);
    expect(list.some((a: { nombre: string }) => a.nombre.startsWith('GateC App'))).toBe(true);
  });

  // ── C4: Background/Email — los 4 jobs instrumentados arrancan (verificado en log post-run) ──
  // (No hay endpoint para forzar email en Gate C; la evidencia de Background_JobStarted/Finished
  //  se valida post-run en el archivo del log del proceso.)

  // ── C5: Logout → revocación de sesión ──
  test('C5.1 POST /auth/logout → sesión revocada', async () => {
    const r = await api.post(`${API}/auth/logout`, { headers: bearer(token.accessToken), ignoreHTTPSErrors: true });
    expect(r.status()).toBe(200);
  });

  test('C5.2 JWT tras logout: refresh rechazado (token revocado)', async () => {
    const r = await api.post(`${API}/auth/refresh`, { data: { refreshToken: token.refreshToken }, ignoreHTTPSErrors: true });
    expect(r.status()).toBe(401);
  });
});