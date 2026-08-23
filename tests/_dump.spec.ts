import { test, expect } from '@playwright/test';

import { API_BASE, WEB_BASE } from './api-config';
const ADMIN_USER = 'sistema';
const ADMIN_PASS = 'Admin@123';

test('dump dashboard ejecutivo', async ({ page }) => {
  page.on('response', async (r) => {
    const u = r.url();
    if (u.includes('/api/dashboard')) console.log('RESP', r.status(), u.replace(API_BASE, ''));
  });
  const loginResp = await page.request.post(`${API_BASE}/auth/login`, {
    data: { NomUsuario: ADMIN_USER, Password: ADMIN_PASS, IdApp: 1, IdTenant: 1 }
  });
  const body = await loginResp.json();
  const token = body.accessToken ?? body.AccessToken;
  await page.goto(`${WEB_BASE}/`);
  await page.evaluate((s) => {
    localStorage.setItem('access_token', s.token);
    localStorage.setItem('id_usuario', s.idUsuario);
    localStorage.setItem('id_tenant', s.idTenant);
    localStorage.setItem('nom_usuario', s.nomUsuario);
  }, { token, idUsuario: String(body.idUsuario ?? 1), idTenant: String(body.idTenant ?? 1), nomUsuario: body.nomUsuario ?? 'sistema' });
  await page.reload();
  await page.waitForLoadState('networkidle');
  await page.goto(`${WEB_BASE}/admin/dashboard-enterprise`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(6000);
  const txt = await page.evaluate(() => document.body.innerText);
  const fs = require('fs');
  fs.writeFileSync('C:/Users/DEVELO~1/AppData/Local/Temp/opencode/dom.txt', txt);
  console.log('===BODY_LEN===' + txt.length);
  const hasErr = txt.includes('unhandled') || txt.toLowerCase().includes('algo sali');
  console.log('===HAS_ERROR===' + hasErr);
  const marker = txt.indexOf('EJECUTIVO');
  console.log('===AFTER_TABS===\n' + (marker >= 0 ? txt.substring(marker, marker + 1500) : 'NO TABS MARKER'));
  const cards = await page.evaluate(() => Array.from(document.querySelectorAll('.mud-paper')).map(p => p.innerText).filter(t => t && t.trim().length < 80).slice(0, 20));
  console.log('===CARDS===\n' + JSON.stringify(cards, null, 1));
});
