import { test, expect, Page, BrowserContext } from '@playwright/test';

const WEB_BASE = 'https://localhost:7275';
const API_BASE = 'https://localhost:5001/api';

function decodeJwt(token: string): any {
  const b64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const pad = b64.length % 4 === 0 ? '' : '='.repeat(4 - (b64.length % 4));
  return JSON.parse(Buffer.from(b64 + pad, 'base64').toString('utf-8'));
}

test.describe.serial('F4.2 — OAuth Google Real (PASSPLAT)', () => {
  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ playwright }) => {
    context = await playwright.chromium.launchPersistentContext('', {
      headless: false,
      ignoreHTTPSErrors: true,
      args: ['--ignore-certificate-errors'],
    });
    page = await context.newPage();
  });

  test.afterAll(async () => {
    await context.close();
  });

  test('F4.2.1 — Login UI carga, selecciona App PASSPLAT y Tenant PLATFORM', async () => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);

    const appCombo = page.getByRole('combobox', { name: /Aplicaci/i });
    await appCombo.waitFor({ state: 'visible', timeout: 15000 });
    await expect(appCombo).toBeVisible();

    await appCombo.click();
    await page.getByRole('option', { name: /AccessPlat|PASSPLAT/i }).click();
    await page.waitForTimeout(500);

    const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
    await tenantCombo.waitFor({ state: 'visible', timeout: 15000 });
    await expect(tenantCombo).toBeVisible();

    await tenantCombo.click();
    await page.getByRole('option', { name: /Plataforma/i }).click();
    
    // Esperar a la llamada API de proveedores y a que rendericen
    await page.waitForResponse(resp => resp.url().includes('/proveedores-login') && resp.status() === 200, { timeout: 15000 });
    await page.waitForTimeout(2000); // render Blazor

    // Debug: print page HTML
    const html = await page.content();
    console.log('=== PAGE HTML after tenant select ===');
    console.log(html.substring(0, 8000));

    const userField = page.getByRole('textbox', { name: /Usuario o email/i });
    await expect(userField).toBeVisible({ timeout: 15000 });
    const passField = page.getByRole('textbox', { name: /Contraseña/i });
    await expect(passField).toBeVisible();
  });

  test('F4.2.2 — Botón Google visible y clickeable', async () => {
    // Google es el primer proveedor (OrdenVisual=0) - usar el primer botón login-provider-icon
    const googleBtn = page.locator('button.login-provider-icon').first();
    await googleBtn.waitFor({ state: 'visible', timeout: 20000 });
    await expect(googleBtn).toBeVisible();
    await expect(googleBtn).toBeEnabled();
  });

  test('F4.2.3 — Click Google inicia OAuth real', async () => {
    const googleBtn = page.locator('button.login-provider-icon').first();
    
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'networkidle', timeout: 30000 }),
      googleBtn.click()
    ]);

    await expect(page).toHaveURL(/accounts\.google\.com/);
    
    const url = page.url();
    expect(url).toContain('accounts.google.com');
    expect(url).toContain('client_id=');
    expect(url).toContain('redirect_uri=');
    expect(url).toContain('scope=');
    expect(url).toContain('state=');
    expect(url).toContain('code_challenge=');
    expect(url).toContain('access_type=offline');
  });

  test('F4.2.4 — OAuth callback y JWT con IdApp=1', async () => {
    await page.waitForTimeout(3000);
    
    const currentUrl = page.url();
    console.log('Current URL after OAuth:', currentUrl);
    
    if (currentUrl.includes(WEB_BASE) && !currentUrl.includes('/login')) {
      const token = await page.evaluate(() => localStorage.getItem('access_token') ?? '');
      expect(token.length).toBeGreaterThan(0);
      
      const jwt = decodeJwt(token);
      console.log('JWT claims:', JSON.stringify(jwt, null, 2));
      
      expect(jwt['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']).toBeTruthy();
      expect(jwt.IdApp).toBe('1');
      expect(jwt.TenantId).toBe('1');
      expect(jwt.UsuarioTenantId).toBeTruthy();
      expect(jwt.permiso).toBeInstanceOf(Array);
      expect(jwt.permiso.length).toBeGreaterThan(0);
      expect(jwt.iss).toBe('PassPlat');
      expect(jwt.aud).toBe('PassPlat');
    } else {
      console.log('OAuth flow incomplete - URL:', currentUrl);
      test.skip(true, 'Requires manual Google sign-in in headed browser');
    }
  });
});

test.describe('F4.5 — Rechazo de contexto OAuth inválido', () => {
  test('F4.5.1 — State inexistente redirige a state_invalido_o_expirado', async ({ request }) => {
    const resp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/callback', {
      params: { state: 'invalid-state-xyz', code: 'fake-code' },
      ignoreHTTPSErrors: true,
      maxRedirects: 0
    });
    
    expect(resp.status()).toBe(302);
    const location = resp.headers()['location'] || '';
    expect(location).toContain('state_invalido_o_expirado');
  });

  test('F4.5.2 — State vacío redirige a state_invalido', async ({ request }) => {
    const resp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/callback', {
      params: { state: '', code: 'fake-code' },
      ignoreHTTPSErrors: true,
      maxRedirects: 0
    });
    
    expect(resp.status()).toBe(302);
    const location = resp.headers()['location'] || '';
    expect(location).toContain('state_invalido');
  });

  test('F4.5.3 — Proveedor no coincide con session', async ({ request }) => {
    const authorizeResp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/authorize', {
      params: { idTenant: 1, idApp: 1 },
      ignoreHTTPSErrors: true
    });
    expect(authorizeResp.status()).toBe(200);
    const { authorizationUrl } = await authorizeResp.json();
    
    const stateMatch = authorizationUrl.match(/state=([^&]+)/);
    expect(stateMatch).toBeTruthy();
    const state = stateMatch![1];
    
    const callbackResp = await request.get('https://localhost:5001/api/auth/externo/GITHUB/callback', {
      params: { state, code: 'fake-code' },
      ignoreHTTPSErrors: true,
      maxRedirects: 0
    });
    
    expect(callbackResp.status()).toBe(302);
    const location = callbackResp.headers()['location'] || '';
    expect(location).toContain('proveedor_no_coincide');
  });

  test('F4.5.4 — Sin código de autorización', async ({ request }) => {
    const authorizeResp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/authorize', {
      params: { idTenant: 1, idApp: 1 },
      ignoreHTTPSErrors: true
    });
    const { authorizationUrl } = await authorizeResp.json();
    const stateMatch = authorizationUrl.match(/state=([^&]+)/);
    const state = stateMatch![1];
    
    const resp = await request.get('https://localhost:5001/api/auth/externo/GOOGLE/callback', {
      params: { state },
      ignoreHTTPSErrors: true,
      maxRedirects: 0
    });
    
    expect(resp.status()).toBe(400);
    const body = await resp.json();
    expect(body.codigo).toBe('NO_CODE');
  });

  test('F4.5.5 — RedirectUri nulo en sesión', async () => {
    test.skip();
  });
});