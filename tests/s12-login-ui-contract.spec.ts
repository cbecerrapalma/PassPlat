import { test, expect, Page } from '@playwright/test';

import { API_BASE } from './api-config';

const WEB = process.env.WEB_BASE_URL ?? 'https://localhost:7275';

const PWD = 'Admin@123';
const USER = 'test_multitenant';

const TENANT_PLATAFORMA = 'Plataforma';
const TENANT_ABARROTES = 'Abarrotes del Sur';
const TENANT_VESTUARIO = 'Vestuario del Norte';

const EXPECTED_PERMS = [
  'ACCESOS_VER',
  'GRUPOS_VER',
  'MATRIZ_PERMISOS_VER',
  'PERMISOS_VER',
  'ROLES_VER',
  'USUARIOS_VER',
  'USUARIOS_VERDISP',
];

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

async function selectTenant(page: Page, tenantName: string) {
  const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
  await tenantCombo.waitFor({ state: 'visible', timeout: 15000 });
  await tenantCombo.click();
  await page.getByRole('option', { name: tenantName }).click();
}

async function goToLogin(page: Page) {
  await page.goto(`${WEB}/login`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
}

test.describe.serial('S12 FASE 2.1 — Login UI Contract (7275)', () => {
  test('#1 La página de login carga sin errores de consola', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text());
    });
    page.on('pageerror', (err) => errors.push(err.message));

    await goToLogin(page);

    await expect(page).toHaveTitle(/AccessPlat/);
    await expect(page.getByText('Inicia sesión para continuar')).toBeVisible();
    await expect(page.getByText(/Selecciona un tenant para continuar/i)).toBeVisible();

    expect(errors.filter((e) => !e.includes('HTTP 403'))).toEqual([]);
  });

  test('#2 App: GET /apps/activas 200 y selector de App presente (auto-resuelto con 1 app)', async ({ page }) => {
    const [resp] = await Promise.all([
      page.waitForResponse(
        (res) => res.url().includes('/api/apps/activas') && res.status() === 200,
        { timeout: 15000 },
      ),
      page.goto(`${WEB}/login`, { waitUntil: 'networkidle' }),
    ]);
    await page.waitForTimeout(2000);

    expect(resp.status()).toBe(200);
    await expect(page.getByRole('combobox', { name: /Aplicaci/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('combobox', { name: /Tenant/i })).toBeVisible({ timeout: 15000 });
  });

  test('#3 Tenant: 3 opciones y form se habilita al seleccionar', async ({ page }) => {
    await goToLogin(page);

    const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
    await expect(tenantCombo).toBeVisible({ timeout: 15000 });
    await tenantCombo.click();
    await expect(page.getByRole('option')).toHaveCount(3);
    for (const name of [TENANT_PLATAFORMA, TENANT_ABARROTES, TENANT_VESTUARIO]) {
      await expect(page.getByRole('option', { name })).toBeVisible();
    }
    await page.keyboard.press('Escape');

    // Sin tenant seleccionado el gate IsAuthenticationContextReady bloquea el form
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);

    await selectTenant(page, TENANT_ABARROTES);
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toBeVisible({ timeout: 15000 });
  });

  test('#4 NO existe selector de método (sin radios Contraseña/OAuth)', async ({ page }) => {
    await goToLogin(page);

    await expect(page.getByRole('radio')).toHaveCount(0);
    await expect(page.getByText(/Proveedor OAuth/i)).toHaveCount(0);
    await expect(page.getByText(/Selecciona el método/i)).toHaveCount(0);

    await selectTenant(page, TENANT_ABARROTES);
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });

    await expect(page.getByRole('radio')).toHaveCount(0);
    await expect(page.getByText(/Proveedor OAuth/i)).toHaveCount(0);
  });

  test('#5 Formulario Usuario/Contraseña siempre visible tras resolver tenant', async ({ page }) => {
    await goToLogin(page);

    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toHaveCount(0);

    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toBeVisible();
    await expect(page.getByText('Recordarme')).toBeVisible();
    await expect(page.getByRole('button', { name: /¿Olvidaste tu contraseña\?/i })).toBeVisible();
  });

  test('#6 OAuth y formulario simultáneos en tenant 1 (GOOGLE + password juntos)', async ({ page }) => {
    await goToLogin(page);

    await selectTenant(page, TENANT_PLATAFORMA);

    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/O contin[uú]a con/i)).toBeVisible();
    await expect(page.locator('.login-provider-icon')).toHaveCount(1);
  });

  test('#7 Proveedores dinámicos coinciden con la respuesta del API', async ({ page }) => {
    const providersByTenant = new Map<string, any[]>();

    page.on('response', async (res) => {
      if (res.url().includes('/api/auth/externo/proveedores-login')) {
        const match = res.url().match(/idTenant=(\d+)/);
        const tenant = match ? match[1] : 'unknown';
        providersByTenant.set(tenant, await res.json());
      }
    });

    await goToLogin(page);

    await selectTenant(page, TENANT_PLATAFORMA);

    await expect(page.locator('.login-provider-icon')).toHaveCount(1, { timeout: 15000 });
    const t1 = providersByTenant.get('1');
    expect(Array.isArray(t1)).toBe(true);
    expect(t1.some((p: any) => p.codigo === 'GOOGLE')).toBe(true);
    await expect(page.locator('.login-provider-icon')).toHaveCount(
      t1.filter((p: any) => p.nombre).length,
    );
  });

  test('#8 Login real 7275: JWT con claims correctos y form permanece', async ({ page }) => {
    await goToLogin(page);

    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toHaveCount(0);

    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });
    // FASE 2R: ABARROTES (t3) sin proveedores propios válidos hereda GOOGLE de la plataforma
    await expect(page.locator('.login-provider-icon')).toHaveCount(1);
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toBeVisible();

    const userField = page.getByRole('textbox', { name: /Usuario o email/i });
    await userField.fill(USER);
    await page.getByRole('textbox', { name: /Contraseña/i }).fill(PWD);
    await page.getByText('Recordarme').click();
    await page.getByRole('button', { name: /Iniciar Sesión/i }).click();

        await expect(page.getByText('test_multitenant').first()).toBeVisible({ timeout: 20000 });
    await expect(page.getByText(TENANT_ABARROTES).first()).toBeVisible();

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

  test('#9 FASE 2R — Herencia de proveedores de plataforma vía API', async ({ request }) => {
    // t1 (PLATFORM): GOOGLE propio, NO es de plataforma
    const t1 = await request.get(`${API_BASE}/auth/externo/proveedores-login?idTenant=1`);
    expect(t1.status()).toBe(200);
    const p1 = await t1.json();
    expect(Array.isArray(p1)).toBe(true);
    const google1 = p1.find((p: any) => p.codigo === 'GOOGLE');
    expect(google1).toBeTruthy();
    expect(google1.esDePlataforma).toBe(false);

    // t3 (ABARROTES): sin proveedores propios válidos, hereda GOOGLE de la plataforma
    const t3 = await request.get(`${API_BASE}/auth/externo/proveedores-login?idTenant=3`);
    expect(t3.status()).toBe(200);
    const p3 = await t3.json();
    expect(Array.isArray(p3)).toBe(true);
    const google3 = p3.find((p: any) => p.codigo === 'GOOGLE');
    expect(google3).toBeTruthy();
    expect(google3.esDePlataforma).toBe(true);

    // t4 (VESTUARIO): misma herencia que t3
    const t4 = await request.get(`${API_BASE}/auth/externo/proveedores-login?idTenant=4`);
    expect(t4.status()).toBe(200);
    const p4 = await t4.json();
    const google4 = p4.find((p: any) => p.codigo === 'GOOGLE');
    expect(google4).toBeTruthy();
    expect(google4.esDePlataforma).toBe(true);
  });
});
