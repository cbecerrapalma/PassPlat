import { test, expect } from '@playwright/test';
import { API_BASE, WEB_BASE } from './api-config';
test('capture Blazor error', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      errors.push(msg.text());
      console.log('CONSOLE_ERROR:', msg.text().substring(0, 200));
    }
  });
  page.on('pageerror', err => {
    errors.push(err.message);
    console.log('PAGE_ERROR:', err.message.substring(0, 200));
  });
  // login
  const loginResp = await page.request.post(`${API_BASE}/auth/login`, {
    data: { NomUsuario: 'sistema', Password: 'Admin@123', IdApp: 1, IdTenant: 1 }
  });
  const token = (await loginResp.json()).accessToken;
  await page.goto(`${WEB_BASE}/`);
  await page.evaluate((t) => {
    localStorage.setItem('access_token', t);
    localStorage.setItem('id_usuario', '1');
    localStorage.setItem('id_tenant', '1');
    localStorage.setItem('nom_usuario', 'sistema');
  }, token);
  await page.reload();
  await page.waitForLoadState('networkidle');
  await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(8000);
  console.log('===BODY===');
  console.log(await page.locator('body').innerText());
  console.log('===ERRORS=' + errors.length);
  errors.forEach(e => console.log('ERR:', e.substring(0, 300)));
});
