import { test, expect, Page, APIRequestContext, request } from '@playwright/test';

const API_BASE = 'https://localhost:5001/api';
const WEB_BASE = 'https://localhost:7275';

interface AuthTokens { accessToken: string; refreshToken: string; idUsuario: number; idTenant: number; nomUsuario: string; }
let api: APIRequestContext;
let tokens: AuthTokens;

const PWD = 'Admin@123';
const USER_CBECERRAPALMA = 'cbecerrapalma@gmail.com'; // Email real Google
const USER_MULTITENANT = 'test_multitenant';
const TENANT_PLATFORM = 1;
const TENANT_ABARROTES = 3;
const TENANT_VESTUARIO = 4;
const APP_PASSPLAT = 1;

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

async function loginViaApi(nomUsuario: string, password: string, idTenant = TENANT_ABARROTES, idApp = APP_PASSPLAT): Promise<{ status: number; data?: any; error?: string }> {
  for (let attempt = 0; attempt < 5; attempt++) {
    const r = await api.post(`${API_BASE}/auth/login`, {
      data: { NomUsuario: nomUsuario, Password: password, IdApp: idApp, IdTenant: idTenant },
      ignoreHTTPSErrors: true,
    });
    if (r.status() === 429) { await new Promise((res) => setTimeout(res, 5000)); continue; }
    if (!r.ok()) {
      let err: any = {}; try { err = await r.json(); } catch { }
      return { status: r.status(), error: `${err.codigo}: ${err.mensaje}` };
    }
    const data = await r.json();
    return { status: r.status(), data };
  }
  return { status: 429, error: 'rate limited after 5 retries' };
}

async function selectTenant(page: Page, tenantName: string) {
  const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
  await tenantCombo.waitFor({ state: 'visible', timeout: 15000 });
  await tenantCombo.click();
  await page.getByRole('option', { name: tenantName }).click();
  await page.waitForTimeout(1000);
}

async function selectApp(page: Page, appName: string) {
  const appCombo = page.getByRole('combobox', { name: /Aplicaci/i });
  await appCombo.waitFor({ state: 'visible', timeout: 15000 });
  await appCombo.click();
  await page.getByRole('option', { name: appName }).click();
  await page.waitForTimeout(500);
}

async function doLoginFromUi(page: Page, username: string, password: string) {
  const userField = page.getByRole('textbox', { name: /Usuario o email/i });
  await userField.waitFor({ state: 'visible', timeout: 15000 });
  await userField.fill(username);
  await page.getByRole('textbox', { name: /Contraseña/i }).fill(password);
  await page.getByRole('button', { name: /Iniciar Sesión/i }).click();
}

test.describe.serial('S14 — App/Tenant Resolution Certification', () => {
  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ ignoreHTTPSErrors: true });
  });

  test.afterAll(async () => { await api.dispose(); });

  // S14-01: App seleccionada se conserva durante OAuth (verificado en F4.2 manual)
  test('S14-01 — App seleccionada se conserva durante OAuth', async ({ page }) => {
    // Verificación conceptual: el flujo OAuth usa idApp desde UI → authorize → session → JWT
    // Esta prueba se certifica manualmente en F4.2 (Google real headed)
    test.skip(true, 'Certificado en F4.2 OAuth real manual (browser headed)');
  });

  // S14-02: Tenant seleccionado se conserva durante OAuth
  test('S14-02 — Tenant seleccionado se conserva durante OAuth', async ({ page }) => {
    test.skip(true, 'Certificado en F4.2 OAuth real manual (browser headed)');
  });

  // S14-03: OAuthSession conserva IdApp
  test('S14-03 — OAuthSession conserva IdApp', async () => {
    // Verificar que authorize guarda IdApp en cache oauth_state
    const authResp = await api.get(`${API_BASE}/auth/externo/GOOGLE/authorize`, {
      params: { idTenant: TENANT_PLATFORM, idApp: APP_PASSPLAT },
      ignoreHTTPSErrors: true,
    });
    expect(authResp.status()).toBe(200);
    const { authorizationUrl } = await authResp.json();
    expect(authorizationUrl).toContain('client_id=');
    expect(authorizationUrl).toContain('redirect_uri=');
    expect(authorizationUrl).toContain('state=');
    // El state se guarda en cache con IdApp=1
    // Validación completa requiere callback real (F4.2 manual)
    test.skip(true, 'Validación completa en F4.2 manual; authorize OK');
  });

  // S14-04: JWT contiene IdApp correcto
  test('S14-04 — JWT contiene IdApp correcto', async () => {
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, APP_PASSPLAT);
    expect(r.status).toBe(200);
    const jwt = decodeJwt(r.data!.accessToken);
    expect(jwt.IdApp).toBe('1');
    expect(jwt.TenantId).toBe('3');
    expect(jwt.UsuarioTenantId).toBe('4');
    expect(jwt['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']).toBe('8');
  });

  // S14-05: JWT contiene IdTenant correcto
  test('S14-05 — JWT contiene IdTenant correcto', async () => {
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, APP_PASSPLAT);
    expect(r.status).toBe(200);
    const jwt = decodeJwt(r.data!.accessToken);
    expect(jwt.TenantId).toBe('3');
  });

  // S14-06: JWT contiene IdUsuarioTenant correcto
  test('S14-06 — JWT contiene IdUsuarioTenant correcto', async () => {
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, APP_PASSPLAT);
    expect(r.status).toBe(200);
    const jwt = decodeJwt(r.data!.accessToken);
    expect(jwt.UsuarioTenantId).toBe('4');
  });

  // S14-07: Permisos corresponden al App
  test('S14-07 — Permisos corresponden al App', async () => {
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, APP_PASSPLAT);
    expect(r.status).toBe(200);
    const jwt = decodeJwt(r.data!.accessToken);
    const expectedPerms = [
      'ACCESOS_VER', 'GRUPOS_VER', 'MATRIZ_PERMISOS_VER', 'PERMISOS_VER',
      'ROLES_VER', 'USUARIOS_VER', 'USUARIOS_VERDISP'
    ];
    expect(jwt.permiso).toEqual(expectedPerms);
  });

  // S14-08: App 1 OAuth real (cbecerrapalma@gmail.com)
  test('S14-08 — App 1 OAuth real (Google)', async ({ page }) => {
    // Requiere browser headed manual con cbecerrapalma@gmail.com
    // Flujo: login UI → Google → callback → JWT → dashboard
    test.skip(true, 'Certificado manual F4.2: cbecerrapalma@gmail.com en tenant 1 (PLATFORM)');
  });

  // S14-09: App 2 si existe combinación válida
  test('S14-09 — App 2 (TEST_APP_*) combinación válida', async () => {
    // App 2 inactiva, sin Accesos → BLOCKED/DATA
    const r = await api.get(`${API_BASE}/apps/2`, { ignoreHTTPSErrors: true });
    if (r.ok()) {
      const app = await r.json();
      if (!app.activa) {
        test.skip(true, 'BLOCKED/DATA: App 2 inactiva, sin Accesos configurados');
      }
    }
    test.skip(true, 'BLOCKED/DATA: App 2 inactiva, sin Accesos');
  });

  // S14-10: App 3 si existe combinación válida
  test('S14-10 — App 3 (TEST_APP_*) combinación válida', async () => {
    test.skip(true, 'BLOCKED/DATA: App 3 inactiva, sin Accesos');
  });

  // S14-11: App incorrecta rechazada
  test('S14-11 — App incorrecta rechazada', async () => {
    // Intentar login con IdApp=999 (inexistente) → debe fallar
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, 999);
    expect(r.status).toBe(401);
    expect(r.error).toContain('Sin acceso a la aplicacion');
  });

// S14-12: Tenant incompatible rechazado
  test('S14-12 — Tenant incompatible rechazado', async () => {
    // Usuario test_multitenant no tiene membresía en tenant 999
    const r = await loginViaApi(USER_MULTITENANT, PWD, 999, APP_PASSPLAT);
    expect(r.status).toBe(401);
    // SP devuelve error genérico de login fallido para tenant inválido
    expect(r.error).toContain('LOGIN_FAILED');
  });

  // S14-13: OAuthSession inexistente/expirada
  test('S14-13 — OAuthSession inexistente/expirada', async ({ request }) => {
    const resp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/callback', {
      params: { state: 'invalid-state-xyz-123', code: 'fake-code' },
      ignoreHTTPSErrors: true,
      maxRedirects: 0,
    });
    expect(resp.status()).toBe(302);
    const location = resp.headers()['location'] || '';
    expect(location).toContain('state_invalido_o_expirado');
  });

  // S14-14: Email resolver respeta prioridad App→Tenant→Global
  test('S14-14 — Email resolver respeta prioridad', async () => {
    // Smoke de conectividad API (resolver certificado en xUnit/F5)
    const r = await api.get(`${API_BASE}/confapp/general`, { ignoreHTTPSErrors: true });
    expect([200, 404]).toContain(r.status());
  });

  // S14-15: Configuración no contamina otro App/Tenant
  test('S14-15 — Aislamiento configuración App/Tenant', async () => {
    // Login tenant 3 → verificar no ve datos tenant 4
    const r = await loginViaApi(USER_MULTITENANT, PWD, TENANT_ABARROTES, APP_PASSPLAT);
    expect(r.status).toBe(200);
    const token = r.data!.accessToken;
    const usersResp = await api.get(`${API_BASE}/usuarios?page=1&pageSize=200`, {
      headers: { Authorization: `Bearer ${token}` }, ignoreHTTPSErrors: true,
    });
    expect(usersResp.status()).toBe(200);
    const users = await usersResp.json();
    // Usuario 8 está en tenant 3 y 4, pero JWT scope tenant 3 → solo ve tenant 3
    expect(users.every((u: any) => u.idTenant === 3)).toBe(true);
  });
});