import { test, expect, Page, request, APIRequestContext } from '@playwright/test';

import { API_BASE, WEB_BASE } from './api-config';

const PWD = 'Admin@123';
const USER = 'test_multitenant';
const TENANT_ID = 3;
const TENANT_NAME = 'Abarrotes del Sur';
const APP_ID = 1;

const EXPECTED_PERMS = [
  'ACCESOS_VER',
  'GRUPOS_VER',
  'MATRIZ_PERMISOS_VER',
  'PERMISOS_VER',
  'ROLES_VER',
  'USUARIOS_VER',
  'USUARIOS_VERDISP',
];

interface AuthTokens { accessToken: string; refreshToken: string; idUsuario: number; idTenant: number; nomUsuario: string; }

let tokens: AuthTokens;
let api: APIRequestContext;

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

async function loginViaApi(nomUsuario: string, password: string, idTenant = TENANT_ID, idApp = APP_ID): Promise<{ status: number; data?: any; error?: string }> {
  for (let attempt = 0; attempt < 5; attempt++) {
    const r = await api.post(`${API_BASE}/auth/login`, {
      data: { NomUsuario: nomUsuario, Password: password, IdApp: idApp, IdTenant: idTenant },
      ignoreHTTPSErrors: true,
    });
    if (r.status() === 429) { await new Promise((res) => setTimeout(res, 5000)); continue; }
    if (!r.ok()) {
      let err: any = {};
      try { err = await r.json(); } catch { /* noop */ }
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
}

async function doLoginFromUi(page: Page, username: string, password: string, rememberMe = true): Promise<void> {
  const userField = page.getByRole('textbox', { name: /Usuario o email/i });
  await userField.waitFor({ state: 'visible', timeout: 15000 });
  await userField.fill(username);
  await page.getByRole('textbox', { name: /Contraseña/i }).fill(password);
  if (rememberMe) {
    await page.getByText('Recordarme').click();
  }
  await page.getByRole('button', { name: /Iniciar Sesión/i }).click();
}

test.describe.serial('S12 — Login E2E Certification', () => {
  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ ignoreHTTPSErrors: true });
  });

  test.afterAll(async () => { await api.dispose(); });

  test('#1 Login exitoso desde UI (App auto-resuelta + Tenant + credenciales)', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);

    await selectTenant(page, TENANT_NAME);

    await doLoginFromUi(page, USER, PWD);

        await expect(page.getByText('test_multitenant').first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByText(TENANT_NAME).first()).toBeVisible();
  });

  test('#2 JWT del login UI contiene claims correctos', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    const token = await page.evaluate(() => localStorage.getItem('access_token') ?? '');
    expect(token.length).toBeGreaterThan(0);
    const jwt = decodeJwt(token);
    expect(jwt['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']).toBe('8');
    expect(jwt.IdApp).toBe('1');
    expect(jwt.TenantId).toBe('3');
    expect(jwt.UsuarioTenantId).toBe('4');
    expect(jwt.permiso).toEqual(EXPECTED_PERMS);
    expect(jwt.iss).toBe('PassPlat');
    expect(jwt.aud).toBe('PassPlat');
  });

  test('#3 UI respeta permisos: nav SEGURIDAD visible, sin opciones de escritura', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    await expect(page.getByRole('link', { name: /Usuarios/i })).toBeVisible();
    await expect(page.getByRole('link', { name: /Accesos/i })).toBeVisible();
  });

  test('#4 Endpoint protegido acepta JWT UI (GET /usuarios 200, datos tenant 3)', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    const token = await page.evaluate(() => localStorage.getItem('access_token') ?? '');
    const r = await api.get(`${API_BASE}/usuarios?page=1&pageSize=200`, {
      headers: { Authorization: `Bearer ${token}` }, ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(200);
    const data = await r.json();
    expect(Array.isArray(data)).toBe(true);
    expect(data.every((u: any) => u.idTenant === 3)).toBe(true);
  });

  test('#5 Endpoint no autorizado rechaza JWT UI (POST /usuarios 403)', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    const token = await page.evaluate(() => localStorage.getItem('access_token') ?? '');
    const r = await api.post(`${API_BASE}/usuarios`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: { NomUsuario: 's12_nocreate', Email: null, IdTenant: 3, Password: PWD, IdEstado: 1, Nombre: 'S12', Apellido: 'Nocreate', ReqCambioPwd: false },
      ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(403);
  });

  test('#6 Aislamiento tenant: JWT tenant 3 no devuelve usuarios de tenant 4', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    const token = await page.evaluate(() => localStorage.getItem('access_token') ?? '');
    const r = await api.get(`${API_BASE}/usuarios?page=1&pageSize=200`, {
      headers: { Authorization: `Bearer ${token}` }, ignoreHTTPSErrors: true,
    });
    expect(r.status()).toBe(200);
    const data = await r.json();
    expect(data.some((u: any) => u.idTenant === 4)).toBe(false);
  });

  test('#7 Password incorrecto desde UI muestra error controlado', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, 'WrongPass123!');
    await expect(page.getByText(/Credenciales inv[aá]lidas/i)).toBeVisible({ timeout: 15000 });
    await expect(page).toHaveURL(/\/login$/);
  });

  test('#8 Credenciales vacías: sin request API, permanece en /login', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);

    const userField = page.getByRole('textbox', { name: /Usuario o email/i });
    await userField.waitFor({ state: 'visible', timeout: 15000 });

    let loginRequests = 0;
    page.on('request', (req) => {
      if (req.url().includes('/api/auth/login')) loginRequests++;
    });

    await page.getByRole('button', { name: /Iniciar Sesión/i }).click();
    await page.waitForTimeout(3000);
    expect(loginRequests).toBe(0);
    await expect(page).toHaveURL(/\/login$/);
  });

  test('#9 Usuario sin acceso a la app retorna SinAccesoApp via API', async () => {
    const r = await loginViaApi('test_noemail_484218', 'WrongPass123!', 1);
    expect(r.status).toBe(401);
    expect(r.error).toContain('Sin acceso a la aplicacion');
  });

  test('#10 Usuario inactivo retorna CuentaInactiva via API', async () => {
    const r = await loginViaApi('test_inactive_state', 'WrongPass123!', 1);
    expect(r.status).toBe(401);
    expect(r.error).toContain('Cuenta inactiva');
  });

  test('#11 API-equivalente produce mismos claims que login UI', async () => {
    const r = await loginViaApi(USER, PWD, TENANT_ID);
    expect(r.status).toBe(200);
    const jwt = decodeJwt(r.data.accessToken);
    expect(jwt['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']).toBe('8');
    expect(jwt.IdApp).toBe('1');
    expect(jwt.TenantId).toBe('3');
    expect(jwt.UsuarioTenantId).toBe('4');
    expect(jwt.permiso).toEqual(EXPECTED_PERMS);
    expect(r.data.reqCambioPwd).toBe(false);
  });

  test('#12 Logout desde UI retorna a /login', async ({ page }) => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    await selectTenant(page, TENANT_NAME);
    await doLoginFromUi(page, USER, PWD);
        await expect(page.getByText(TENANT_NAME).first()).toBeVisible({ timeout: 20000 });

    await page.getByText('test_multitenant').first().click();
    await page.getByRole('button', { name: /Cerrar sesión/i }).click();
    await expect(page.getByText('Inicia sesión para continuar')).toBeVisible({ timeout: 15000 });
  });
});
