import { test, expect, Page } from '@playwright/test';

import { API_BASE } from './api-config';

const WEB = process.env.WEB_BASE_URL ?? 'https://localhost:7275';

const PWD = 'Admin@123';
const USER = 'test_multitenant';

const TENANT_ABARROTES = 'Abarrotes del Sur';

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

// Contrato actual (post-A1.7): el selector de App se muestra y se auto-resuelve cuando
// hay una única app activa; el formulario/auth aparece cuando IsAuthenticationContextReady
// (AppId > 0 && TenantIdContexto > 0). NO existe botón "Continuar".

test.describe.serial('S12 FASE 2R.1 - Login Context Gate (7275)', () => {
  test('LOGIN-CONTEXT-01 Sin contexto: form y OAuth bloqueados, AppId real no asumida', async ({ page }) => {
    let appsPayload: any[] | null = null;

    const [resp] = await Promise.all([
      page.waitForResponse(
        (res) => res.url().includes('/api/apps/activas') && res.status() === 200,
        { timeout: 15000 },
      ),
      page.goto(`${WEB}/login`, { waitUntil: 'networkidle' }),
    ]);
    await page.waitForTimeout(2000);
    appsPayload = await resp.json();

    // La App debe resolver con un IdApp real proveniente de /apps/activas
    expect(Array.isArray(appsPayload)).toBe(true);
    expect(appsPayload!.length).toBeGreaterThan(0);
    expect(appsPayload![0].id).toBeGreaterThan(0);

    // Sin tenant aún -> contexto incompleto -> NI form NI OAuth visibles
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toHaveCount(0);
    await expect(page.locator('.login-provider-icon')).toHaveCount(0);
    await expect(page.getByText(/Selecciona un tenant para continuar/i)).toBeVisible();
  });

  test('LOGIN-CONTEXT-02 Tenant sin contexto completo: selector de tenant no habilita auth', async ({ page }) => {
    await goToLogin(page);

    // El selector de tenant existe pero, sin contexto completo (tenant seleccionado),
    // NO se muestra el form ni OAuth
    const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
    await expect(tenantCombo).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/Selecciona una aplicación para continuar/i)).toBeVisible();

    // CRITICO: con tenant aún sin seleccionar, NO debe existir form ni OAuth
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);
    await expect(page.locator('.login-provider-icon')).toHaveCount(0);
  });

  test('LOGIN-CONTEXT-03 App sin Tenant: auth bloqueada (ni form ni Google)', async ({ page }) => {
    await goToLogin(page);

    // App auto-resuelta (única app activa) pero tenant pendiente -> gate bloquea
    await expect(page.getByRole('combobox', { name: /Tenant/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toHaveCount(0);
    await expect(page.locator('.login-provider-icon')).toHaveCount(0);
  });

  test('LOGIN-CONTEXT-04 App+Tenant: auth habilitada (form completo)', async ({ page }) => {
    await goToLogin(page);

    await expect(page.getByRole('combobox', { name: /Aplicaci/i })).toBeVisible({ timeout: 15000 });
    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toBeVisible();
    await expect(page.getByText('Recordarme')).toBeVisible();
  });

  test('LOGIN-CONTEXT-05 App+Tenant+OAuth: GOOGLE visible con form', async ({ page }) => {
    await goToLogin(page);

    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(/O contin[uú]a con/i)).toBeVisible();
    await expect(page.locator('.login-provider-icon')).toHaveCount(1);
  });

  test('LOGIN-CONTEXT-06 Form+OAuth simultáneos sin selector de método', async ({ page }) => {
    await goToLogin(page);

    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.login-provider-icon')).toHaveCount(1);
    await expect(page.getByRole('radio')).toHaveCount(0);
    await expect(page.getByText(/Proveedor OAuth/i)).toHaveCount(0);
    await expect(page.getByText(/Selecciona el método/i)).toHaveCount(0);
  });

  test('LOGIN-CONTEXT-07 Login interno sin contexto: bloqueado antes de POST /api/auth/login', async ({ page }) => {
    let loginPosts = 0;
    page.on('request', (req) => {
      if (req.method() === 'POST' && req.url().includes('/api/auth/login')) loginPosts++;
    });

    await goToLogin(page);

    // Sin contexto no hay form -> no se puede disparar POST /api/auth/login
    await expect(page.getByRole('textbox', { name: /Usuario o email/i })).toHaveCount(0);
    expect(loginPosts).toBe(0);
  });

  test('LOGIN-CONTEXT-08 OAuth sin contexto: bloqueado (sin botones de proveedor)', async ({ page }) => {
    await goToLogin(page);

    // Sin contexto completo no hay proveedores -> no se puede iniciar OAuth
    await expect(page.locator('.login-provider-icon')).toHaveCount(0);
    await expect(page.getByText(/O contin[uú]a con/i)).toHaveCount(0);
  });

  test('LOGIN-CONTEXT-09 Login completo: JWT con IdApp/IdTenant/IdUsuarioTenant/permisos', async ({ page }) => {
    await goToLogin(page);

    await expect(page.getByRole('button', { name: /Iniciar Sesión/i })).toHaveCount(0);

    await selectTenant(page, TENANT_ABARROTES);

    await expect(page.getByRole('textbox', { name: /Contraseña/i })).toBeVisible({ timeout: 15000 });

    await page.getByRole('textbox', { name: /Usuario o email/i }).fill(USER);
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
});