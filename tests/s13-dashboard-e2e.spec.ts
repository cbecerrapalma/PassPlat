import { test, expect, type Page } from '@playwright/test';

/** S13 — F12/F13: Dashboard Post-Login Certification (runtime oficial 5001/7275).
 *
 * Certifica que el Dashboard post-login NO genera 403:
 *  - Con usuario interno (test_multitenant, tenant 3) y permisos acotados,
 *    los widgets gateados (SESIONES_VER / APPS_VER / TENANTS_VER / AUDITORIA_VER)
 *    se degradan sin llamar a la API (graceful degradation, F2/F3).
 *  - Los endpoints permitidos (USUARIOS_VER) responden 200 con datos reales.
 *  - Durante la navegación a los dashboards NO se produce NINGUNA respuesta
 *    HTTP 403 en ninguna llamada /api.
 */
const API = process.env.API_BASE_URL ?? 'https://localhost:5001/api';
const WEB = process.env.WEB_BASE_URL ?? 'https://localhost:7275';

const USER = 'test_multitenant';
const PWD = 'Admin@123';
const ID_APP = 1;
const ID_TENANT = 3;

// Permisos que test_multitenant NO posee → sus llamadas se gatean (degradación).
const GATED_ENDPOINTS = [
  'Sesiones/contar-tenant',        // SESIONES_VER
  'Apps/count',                    // APPS_VER
  'Tenants/activos/count',         // TENANTS_VER
  'AuditoriaPwd/tenant/20',        // AUDITORIA_VER
];

// Permisos que test_multitenant SÍ posee → deben responder 200.
const ALLOWED: Array<[string, string]> = [
  ['Dashboard', 'USUARIOS_VER'],
  ['Usuarios/con-password-expirada', 'USUARIOS_VER'],
  ['iden-ext/tenant/3', 'USUARIOS_VER'],
  ['providen/activos', 'USUARIOS_VER'],
];

function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', Accept: 'application/json' };
}

async function loginToken(page: Page): Promise<string> {
  const resp = await page.request.post(`${API}/auth/login`, {
    data: { NomUsuario: USER, Password: PWD, IdApp: ID_APP, IdTenant: ID_TENANT },
    ignoreHTTPSErrors: true,
  });
  expect(resp.ok(), 'login debe devolver 200').toBeTruthy();
  const body = await resp.json();
  return body.accessToken;
}

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

/** Navega al dashboard dado inyectando la sesión en localStorage. */
async function goToDashboard(page: Page, path: string, token: string) {
  await page.goto(`${WEB}/`, { waitUntil: 'networkidle' });
  await page.evaluate((s) => {
    localStorage.setItem('access_token', s.token);
    localStorage.setItem('refresh_token', '');
    localStorage.setItem('id_usuario', '8');
    localStorage.setItem('id_tenant', '3');
    localStorage.setItem('id_usuario_tenant', '4');
    localStorage.setItem('nom_usuario', 'test_multitenant');
  }, { token });
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.goto(`${WEB}${path}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(4000);
}

test.describe.serial('S13 F12/F13 - Dashboard Post-Login sin 403', () => {
  test('S13-01 Login interno produce JWT con TenantId/IdApp correctos', async ({ page }) => {
    const token = await loginToken(page);
    const claims = decodeJwt(token);
    expect(String(claims.TenantId)).toBe(String(ID_TENANT));
    expect(String(claims.IdApp)).toBe(String(ID_APP));
    expect(claims.permiso).toContain('USUARIOS_VER');
    expect(claims.permiso).not.toContain('SESIONES_VER');
    expect(claims.permiso).not.toContain('APPS_VER');
    expect(claims.permiso).not.toContain('TENANTS_VER');
    expect(claims.permiso).not.toContain('AUDITORIA_VER');
  });

  test('S13-02 Endpoints permitidos responden 200 (sin 403)', async ({ page }) => {
    const token = await loginToken(page);
    for (const [endpoint, permiso] of ALLOWED) {
      const resp = await page.request.get(`${API}/${endpoint}`, {
        headers: authHeaders(token),
        ignoreHTTPSErrors: true,
      });
      expect(resp.status(), `${endpoint} (${permiso}) debe ser 200, obtuvo ${resp.status()}`).toBe(200);
    }
  });

  test('S13-03 Endpoints gateados no accesibles sin permiso (401/403)', async ({ page }) => {
    const token = await loginToken(page);
    for (const endpoint of GATED_ENDPOINTS) {
      const resp = await page.request.get(`${API}/${endpoint}`, {
        headers: authHeaders(token),
        ignoreHTTPSErrors: true,
      });
      // Sin el permiso correspondiente el backend debe denegar (401/403).
      expect([401, 403], `${endpoint} debe denegarse, obtuvo ${resp.status()}`).toContain(resp.status());
    }
  });

  test('S13-04 Dashboard Operacional: red sin 403 y sin llamadas gateadas', async ({ page }) => {
    const token = await loginToken(page);
    const requests: string[] = [];
    page.on('response', (res) => {
      if (res.url().includes('/api/')) requests.push(`${res.status()} ${res.url().replace(API, '')}`);
    });

    await goToDashboard(page, '/admin/dashboard-operacional', token);

    const callStatuses = requests.map((r) => Number(r.split(' ')[0]));
    expect(callStatuses.every((s) => s < 400), `ninguna llamada debe ser 4xx/5xx: ${requests.join(' | ')}`).toBe(true);

    for (const gated of GATED_ENDPOINTS) {
      expect(requests.some((r) => r.includes(gated)), `${gated} no debe llamarse (gate)`).toBe(false);
    }
    expect(requests.some((r) => r.includes('/Dashboard')), 'api/Dashboard debe llamarse').toBe(true);
  });

  test('S13-05 Dashboard IAM: red sin 403 y sin llamadas gateadas', async ({ page }) => {
    const token = await loginToken(page);
    const requests: string[] = [];
    page.on('response', (res) => {
      if (res.url().includes('/api/')) requests.push(`${res.status()} ${res.url().replace(API, '')}`);
    });

    await goToDashboard(page, '/admin/iam-dashboard', token);

    const callStatuses = requests.map((r) => Number(r.split(' ')[0]));
    expect(callStatuses.every((s) => s < 400), `ninguna llamada debe ser 4xx/5xx: ${requests.join(' | ')}`).toBe(true);

    for (const gated of GATED_ENDPOINTS) {
      expect(requests.some((r) => r.includes(gated)), `${gated} no debe llamarse (gate)`).toBe(false);
    }
    expect(requests.some((r) => r.includes('/iden-ext/tenant/3')), 'iden-ext debe llamarse').toBe(true);
  });
});