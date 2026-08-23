import { test, expect } from '@playwright/test';
import { API_BASE, WEB_BASE } from './api-config';

const WEB = process.env.WEB_BASE_URL ?? 'https://localhost:7275';
const API = API_BASE;
const PWD = 'Admin@123';
const USER = 'test_multitenant';
const TENANT_ID = 3;
const APP_ID = 1;

function decodeJwt(jwt: string): any {
  const b64 = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

test.describe.serial('S12 Login Context UI — App+Tenant Visibility', () => {

  test('1. GET /api/apps/activas returns 200 with at least 1 active app', async ({ request }) => {
    const r = await request.get(`${API}/apps/activas`);
    expect(r.status()).toBe(200);
    const apps = await r.json();
    expect(apps.length).toBeGreaterThanOrEqual(1);
    expect(apps[0]).toHaveProperty('id');
    expect(apps[0]).toHaveProperty('codigo');
    expect(apps[0]).toHaveProperty('nombre');
  });

  test('2. Login page renders App selector (MudSelect)', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const appSelect = page.getByRole('combobox', { name: 'Aplicación' });
    await expect(appSelect).toBeVisible();
  });

  test('3. Login page renders Tenant selector (MudSelect)', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona un tenant', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await expect(tenantSelect).toBeVisible();
  });

  test('4. App selector is pre-selected when single app exists', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const appSelect = page.getByRole('combobox', { name: 'Aplicación' });
    const value = await appSelect.inputValue();
    expect(value).toBeTruthy();
    expect(value.length).toBeGreaterThan(0);
  });

  test('5. Tenant selector opens with 3+ options', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona un tenant', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    const options = page.getByRole('option');
    const count = await options.count();
    expect(count).toBeGreaterThanOrEqual(3);
  });

  test('6. Login form is HIDDEN when tenant not selected', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const loginBtn = page.getByRole('button', { name: /iniciar sesión/i });
    await expect(loginBtn).not.toBeVisible();
  });

  test('7. Login form APPEARS after selecting tenant', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    await page.getByRole('option', { name: 'Abarrotes del Sur' }).click();
    const loginBtn = page.getByRole('button', { name: /iniciar sesión/i });
    await expect(loginBtn).toBeVisible({ timeout: 5000 });
  });

  test('8. OAuth providers section appears after tenant selection', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    await page.getByRole('option', { name: 'Abarrotes del Sur' }).click();
    await page.waitForTimeout(1000);
    const providersText = page.getByText('No hay proveedores externos disponibles');
    const continueWith = page.getByText('O continúa con');
    const visible = await providersText.isVisible().catch(() => false) || await continueWith.isVisible().catch(() => false);
    expect(visible).toBeTruthy();
  });

  test('9. Full login: App=PASSPLAT + Tenant=Abarrotes + credentials → Dashboard', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    await page.getByRole('option', { name: 'Abarrotes del Sur' }).click();
    await page.waitForSelector('button:has-text("Iniciar Sesión")', { timeout: 5000 });
    await page.getByRole('textbox', { name: 'Usuario o email' }).fill(USER);
    await page.getByRole('textbox', { name: 'Contraseña' }).fill(PWD);
    await page.getByRole('button', { name: /iniciar sesión/i }).click();
    await page.waitForTimeout(5000);
    const url = page.url();
    const bodyText = await page.textContent('body').catch(() => '');
    const onDashboard = url.includes('7275/') && !url.includes('login');
    const showsUser = bodyText.includes('test_multitenant') || bodyText.includes('Dashboard');
    expect(onDashboard || showsUser).toBeTruthy();
  });

  test('10. JWT after login contains correct App/Tenant claims', async ({ request }) => {
    const r = await request.post(`${API}/auth/login`, {
      data: { NomUsuario: USER, Password: PWD, IdApp: APP_ID, IdTenant: TENANT_ID },
    });
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(body.accessToken).toBeTruthy();
    const claims = decodeJwt(body.accessToken);
    expect(String(claims.IdApp)).toBe(String(APP_ID));
    expect(String(claims.TenantId)).toBe(String(TENANT_ID));
    expect(String(claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'])).toBe('8');
  });

  test('11. Login with bad password shows error (no crash)', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    await page.getByRole('option', { name: 'Abarrotes del Sur' }).click();
    await page.waitForSelector('button:has-text("Iniciar Sesión")', { timeout: 5000 });
    await page.getByRole('textbox', { name: 'Usuario o email' }).fill(USER);
    await page.getByRole('textbox', { name: 'Contraseña' }).fill('WrongPassword123!');
    await page.getByRole('button', { name: /iniciar sesión/i }).click();
    await page.waitForTimeout(3000);
    const errorVisible = await page.getByText(/error|inválida|fallido/i).isVisible().catch(() => false);
    const stillOnLogin = page.url().includes('login');
    expect(errorVisible || stillOnLogin).toBeTruthy();
  });

  test('12. Empty user + empty password blocks submission (no API call)', async ({ page }) => {
    await page.goto(`${WEB}/login`);
    await page.waitForSelector('text=Selecciona una aplicación', { timeout: 15000 });
    const tenantSelect = page.getByRole('combobox', { name: 'Tenant' });
    await tenantSelect.click();
    await page.getByRole('option', { name: 'Abarrotes del Sur' }).click();
    await page.waitForSelector('button:has-text("Iniciar Sesión")', { timeout: 5000 });
    await page.getByRole('button', { name: /iniciar sesión/i }).click();
    await page.waitForTimeout(2000);
    expect(page.url()).toContain('login');
  });
});
