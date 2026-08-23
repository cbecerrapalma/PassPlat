import { test, expect, Page, request } from '@playwright/test';

import { API_BASE, WEB_BASE } from './api-config';
const CREDS = { user: 'sistema', pass: 'Admin@123' };

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  idUsuario: number;
  idTenant: number;
  nomUsuario: string;
}

let _tokens: AuthTokens | null = null;
let _loginPromise: Promise<AuthTokens> | null = null;

async function loginViaApi(): Promise<AuthTokens> {
  if (_tokens) return _tokens;
  if (!_loginPromise) {
    _loginPromise = (async () => {
      const api = await request.newContext({ ignoreHTTPSErrors: true });
      for (let attempt = 0; attempt < 5; attempt++) {
        const res = await api.post(`${API_BASE}/auth/login`, {
          data: {
            NomUsuario: CREDS.user,
            Email: CREDS.user,
            Password: CREDS.pass,
            IdApp: 1,
            IdTenant: 1,
          },
          ignoreHTTPSErrors: true,
        });
        if (res.ok()) {
          const text = await res.text();
          if (text) {
            const data = JSON.parse(text);
            await api.dispose();
            _tokens = {
              accessToken: data.accessToken,
              refreshToken: data.refreshToken,
              idUsuario: data.idUsuario,
              idTenant: data.idTenant,
              nomUsuario: data.nomUsuario ?? CREDS.user,
            };
            return _tokens;
          }
        }
        await new Promise(r => setTimeout(r, 3000));
      }
      throw new Error('No se pudo autenticar después de 5 intentos');
    })();
  }
  return _loginPromise;
}

async function setupBlazorSession(page: Page, tokens: AuthTokens) {
  await page.goto(WEB_BASE);
  await page.waitForTimeout(2000);
  await page.evaluate((t) => {
    localStorage.setItem('access_token', t.accessToken);
    localStorage.setItem('refresh_token', t.refreshToken);
    localStorage.setItem('id_usuario', String(t.idUsuario));
    localStorage.setItem('id_tenant', String(t.idTenant));
    localStorage.setItem('nom_usuario', t.nomUsuario);
  }, tokens as any);
  await page.reload();
  await page.waitForTimeout(3000);
}

const PAGES = [
  { route: '/', name: 'Dashboard' },
  { route: '/usuarios', name: 'Usuarios' },
  { route: '/tenants', name: 'Tenants' },
  { route: '/apps', name: 'Apps' },
  { route: '/admin/grupos', name: 'Grupos' },
  { route: '/admin/roles', name: 'Roles' },
  { route: '/admin/permisos', name: 'Permisos' },
  { route: '/admin/roles-permisos', name: 'RolesPermisos' },
  { route: '/admin/matriz-permisos', name: 'MatrizPermisos' },
  { route: '/accesos', name: 'Accesos' },
  { route: '/sesiones', name: 'Sesiones' },
  { route: '/auditoria', name: 'Auditoria' },
  { route: '/bloqueos', name: 'Bloqueos' },
  { route: '/historial-pwd', name: 'HistorialPwd' },
  { route: '/intentos-acceso', name: 'IntentosAcceso' },
  { route: '/mfa', name: 'MFA' },
  { route: '/config-app', name: 'ConfigApp' },
  { route: '/config-tenants', name: 'ConfigTenants' },
  { route: '/dominios-tenant', name: 'DominiosTenant' },
  { route: '/disp-confiables', name: 'DispConfiables' },
  { route: '/notificaciones', name: 'Notificaciones' },
  { route: '/politicas-pwd', name: 'PoliticasPwd' },
  { route: '/email-templates', name: 'EmailTemplates' },
  { route: '/mantenimiento', name: 'Mantenimiento' },
] as const;

test.describe('E2E — PassPlat', () => {
  let tokens: AuthTokens;

  test.beforeAll(async () => {
    tokens = await loginViaApi();
    expect(tokens.accessToken).toBeTruthy();
  });

  test.describe('Navegación — todas las páginas', () => {
    for (const { route, name } of PAGES) {
      test(`${name} (${route})`, async ({ page }) => {
        test.setTimeout(60000);
        await setupBlazorSession(page, tokens);
        await page.goto(`${WEB_BASE}${route}`);
        await page.waitForTimeout(4000);
        const bodyText = await page.locator('body').innerText();
        expect(bodyText.length).toBeGreaterThan(50);
      });
    }
  });

  test.describe('Componentes compartidos', () => {
    test('CrudToolbar presente en /apps', async ({ page }) => {
      await setupBlazorSession(page, tokens);
      await page.goto(`${WEB_BASE}/apps`);
      await page.waitForTimeout(4000);
      const paper = page.locator('.mud-paper').first();
      await expect(paper).toBeVisible({ timeout: 10000 });
    });

    test('MudTable presente en /usuarios', async ({ page }) => {
      await setupBlazorSession(page, tokens);
      await page.goto(`${WEB_BASE}/usuarios`);
      await page.waitForTimeout(4000);
      const table = page.locator('.mud-table').first();
      await expect(table).toBeVisible({ timeout: 15000 });
    });

    test('MudTable en /tenants', async ({ page }) => {
      await setupBlazorSession(page, tokens);
      await page.goto(`${WEB_BASE}/tenants`);
      await page.waitForTimeout(4000);
      const table = page.locator('.mud-table').first();
      await expect(table).toBeVisible({ timeout: 15000 });
    });
  });

  test.describe('API — endpoints clave', () => {
    let apiContext: any;

    test.beforeAll(async () => {
      apiContext = await request.newContext({ ignoreHTTPSErrors: true });
    });

    test.afterAll(async () => {
      await apiContext.dispose();
    });

    const apiEndpoints: { path: string; name: string }[] = [
      { path: '/apps', name: 'GET /api/apps' },
      { path: '/usuarios', name: 'GET /api/usuarios' },
      { path: '/tenants', name: 'GET /api/tenants' },
      { path: '/grupos/tenant/1', name: 'GET /api/grupos/tenant/1' },
      { path: '/permisos/activos', name: 'GET /api/permisos/activos' },
      { path: '/roles', name: 'GET /api/roles' },
      { path: '/modulos', name: 'GET /api/modulos' },
    ];

    for (const ep of apiEndpoints) {
      test(ep.name, async () => {
        const res = await apiContext.get(`${API_BASE}${ep.path}`, {
          headers: { Authorization: `Bearer ${tokens.accessToken}` },
          ignoreHTTPSErrors: true,
        });
        expect(res.ok()).toBeTruthy();
      });
    }
  });
});
