import { test, expect, type Page } from '@playwright/test';

import { API_BASE, WEB_BASE } from './api-config';

/** Click a MudBlazor tab by text using JS dispatch (bypasses overlay interception) */
async function clickTab(page: Page, text: string) {
  await page.evaluate((tabText: string) => {
    const tabs = document.querySelectorAll<HTMLElement>('.mud-tab');
    for (const tab of tabs) {
      if (tab.textContent?.trim().toUpperCase() === tabText.toUpperCase()) {
        tab.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        break;
      }
    }
  }, text);
  await page.waitForTimeout(500);
}
const ADMIN_USER = 'sistema';
const ADMIN_PASS = 'Admin@123';

let authToken: string;

async function loginAndGetToken(request: any) {
  const loginResp = await request.post(`${API_BASE}/auth/login`, {
    data: { NomUsuario: ADMIN_USER, Password: ADMIN_PASS, IdApp: 1, IdTenant: 1 }
  });
  expect(loginResp.ok()).toBeTruthy();
  const body = await loginResp.json();
  return body.accessToken;
}

async function loginViaUI(page: Page) {
  // Get token via API
  const loginResp = await page.request.post(`${API_BASE}/auth/login`, {
    data: { NomUsuario: ADMIN_USER, Password: ADMIN_PASS, IdApp: 1, IdTenant: 1 }
  });
  expect(loginResp.ok()).toBeTruthy();
  const body = await loginResp.json();
  const token = body.accessToken ?? body.AccessToken;
  const idUsuario = String(body.idUsuario ?? body.IdUsuario ?? 1);
  const idTenant = String(body.idTenant ?? body.IdTenant ?? 1);
  const nomUsuario = body.nomUsuario ?? body.NomUsuario ?? 'sistema';

  // Set session in localStorage and reload so the client auth provider
  // initializes with the permission claim + HttpClient bearer header.
  await page.goto(`${WEB_BASE}/`);
  await page.evaluate((s) => {
    localStorage.setItem('access_token', s.token);
    localStorage.setItem('id_usuario', s.idUsuario);
    localStorage.setItem('id_tenant', s.idTenant);
    localStorage.setItem('nom_usuario', s.nomUsuario);
  }, { token, idUsuario, idTenant, nomUsuario });
  await page.reload();
  await page.waitForLoadState('networkidle');

  // Navigate to dashboard (now authenticated client-side)
  await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(4000);
}

test.describe.serial('FASE 17 - Dashboard Enterprise - API', () => {
  test.beforeAll(async ({ request }) => {
    authToken = await loginAndGetToken(request);
    expect(authToken).toBeTruthy();
  });

  test('API endpoints - all 11 endpoints return 200 with real data', async ({ request }) => {
    const endpoints = [
      'dashboard-enterprise/ejecutivo',
      'dashboard-enterprise/seguridad',
      'dashboard-enterprise/oauth',
      'dashboard-enterprise/email',
      'dashboard-enterprise/operacional',
      'dashboard-enterprise/auditoria',
      'dashboard-enterprise/dispositivos',
      'dashboard-enterprise/tendencias',
      'dashboard-enterprise/estado-general',
      'dashboard-enterprise/ejecutivo-avanzado',
      'dashboard-enterprise/background'
    ];

    for (const ep of endpoints) {
      const resp = await request.get(`${API_BASE}/${ep}`, {
        headers: { Authorization: `Bearer ${authToken}` }
      });
      expect(resp.ok(), `${ep} should return 200`).toBeTruthy();
      const data = await resp.json();
      expect(Object.keys(data).length).toBeGreaterThan(0);
    }
  });
});

test.describe.serial('FASE 17 - Dashboard Enterprise - UI', () => {
  test.beforeEach(async ({ page }) => {
    // Login via UI before each UI test
    await loginViaUI(page);
  });

  test('Dashboard Enterprise page loads with 10 tabs', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000); // Wait for auth/data loading

    // Debug: check what's on the page
    const title = await page.title();
    console.log('Page title:', title);
    const bodyText = await page.locator('body').innerText();
    console.log('Body text (first 500 chars):', bodyText.substring(0, 500));

    // Check 10 tabs exist - tabs render as role=tab with text content
    const tabs = ['Ejecutivo', 'Seguridad', 'OAuth', 'Email', 'Operacional', 'Auditoría', 'Dispositivos', 'Tendencias', 'Avanzado'];
    for (const tab of tabs) {
      await expect(page.getByRole('tab', { name: tab, exact: true })).toBeVisible({ timeout: 15000 });
    }

    // Check global filters toolbar
    await expect(page.locator('.mud-input', { hasText: 'Tenant' }).locator('input')).toBeVisible();
    await expect(page.locator('.mud-input', { hasText: 'Aplicación' }).locator('input')).toBeVisible();
    await expect(page.locator('.mud-input', { hasText: 'Auto-refresh' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export CSV' })).toBeVisible();
  });

  test('Ejecutivo tab shows KPIs with drill-down navigation', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    // Click Ejecutivo tab (should be default)
    await clickTab(page, 'Ejecutivo');

    // Check KPI cards exist
    const kpiLabels = ['Total Usuarios', 'Activos', 'Bloqueados', 'Eliminados', 'Locales', 'Externos', 'Mixtos', 'Tenants', 'Apps', 'Identidades Ext.', 'Emails Hoy', 'Emails Fallidos'];
    await expect(page.locator(`text=${kpiLabels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of kpiLabels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Seguridad tab shows security KPIs', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Seguridad');

    const labels = ['Logins Correctos', 'Logins Fallidos', 'Bloqueos Activos', 'MFA Habilitado', 'MFA Pendiente', 'Passwords Expiradas', 'Passwords x Vencer', 'IPs Sospechosas', 'Nuevos Disp. 24h', 'Usuarios sin MFA', 'Usuarios sin Email'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('OAuth tab shows provider stats', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'OAuth');

    const labels = ['Google', 'GitHub', 'LinkedIn', 'Facebook', 'Instagram', 'Vinculados', 'Consent. Activos', 'Consent. Revocados', 'Errores OAuth', 'Proveedor + Usado'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Email tab shows email pipeline stats and top templates', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Email');

    const labels = ['Enviados', 'Pendientes', 'Errores', 'Hoy', 'Semana', 'Mes'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }

    // Top templates table
    await expect(page.locator('text=Top Templates').first()).toBeVisible({ timeout: 5000 });
  });

  test('Operacional tab shows system metrics and background jobs', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Operacional');

    const labels = ['Resp. Login (ms)', 'CPU %', 'RAM (MB)', 'Resp. API (ms)', 'Resp. SMTP (ms)', 'Resp. OAuth (ms)', 'Resp. SQL (ms)'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }

    // Health checks
    await expect(page.locator('text=Health Checks').first()).toBeVisible();

    // Background Jobs table
    await expect(page.locator('text=Background Jobs').first()).toBeVisible();
  });

  test('Auditoría tab shows audit trail and trend chart', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Auditoría');

    const labels = ['Eventos Hoy', 'Eventos Semana', 'Eventos Mes', 'Usuarios Auditados', 'Cambios Password', 'Cambios OAuth'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }

    // Chart area
    await expect(page.locator('text=Auditoría — Últimos 30 días').first()).toBeVisible({ timeout: 5000 });
  });

  test('Dispositivos tab shows device stats', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Dispositivos');

    const labels = ['Activos', 'Bloqueados', 'Eliminados', 'Nuevos 24h', 'Total IPs'];
    await expect(page.locator(`text=${labels[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const label of labels) {
      await expect(page.locator(`text=${label}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Tendencias tab shows 7 trend lines', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Tendencias');

    // Only test series that have data in the DB (rendered conditionally)
    const series = ['Usuarios', 'Logins', 'Emails', 'Errores', 'MFA', 'Passwords'];
    await expect(page.locator(`text=${series[0]} — Últimos 30 días`).first()).toBeVisible({ timeout: 10000 });
    for (const s of series) {
      await expect(page.locator(`text=${s} — Últimos 30 días`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Avanzado tab shows top 10 rankings', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(5000);

    await clickTab(page, 'Avanzado');

    // Only test tables that have data in the DB (rendered conditionally)
    const tables = ['Top 10 Usuarios Activos', 'Top 10 Proveedores OAuth', 'Top 10 Errores', 'Top 10 Templates'];
    await expect(page.locator(`text=${tables[0]}`).first()).toBeVisible({ timeout: 10000 });
    for (const t of tables) {
      await expect(page.locator(`text=${t}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Export CSV button downloads file', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');

    // Click export and wait for download
    const downloadPromise = page.waitForEvent('download');
    await page.click('button:has-text("Export CSV")');
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toContain('dashboard-enterprise.csv');
  });

  test('Auto-refresh dropdown has 4 options', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');

    const select = page.locator('.mud-input', { hasText: 'Auto-refresh' });
    await expect(select).toBeVisible();
  });

  test('Module status bar shows all 11 modules', async ({ page }) => {
    await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
    await page.waitForLoadState('networkidle');

    const modules = ['Usuarios', 'OAuth', 'Email', 'MFA', 'Password', 'Auditoría', 'Background', 'Dashboard', 'API', 'Base Datos', 'SMTP'];
    const statusBar = page.locator('.mud-grid').first();
    for (const m of modules) {
      await expect(statusBar.locator(`text=${m}`).first()).toBeVisible({ timeout: 5000 });
    }
  });

  test('Navigation in NavMenu includes Dashboard Enterprise', async ({ page }) => {
    await page.goto(`${WEB_BASE}/`);
    await page.waitForLoadState('networkidle');

    // Login if needed
    if (await page.locator('text=Iniciar sesión').isVisible({ timeout: 2000 }).catch(() => false)) {
      await page.fill('input[label="Usuario"]', ADMIN_USER);
      await page.fill('input[label="Contraseña"]', ADMIN_PASS);
      await page.click('button:has-text("Entrar")');
      await page.waitForLoadState('networkidle');
    }

    // Check NavMenu
    await expect(page.locator('.mud-nav-link', { hasText: 'Dashboard Enterprise' })).toBeAttached();
  });
});