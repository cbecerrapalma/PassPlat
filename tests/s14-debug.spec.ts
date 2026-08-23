import { test, expect, Page, BrowserContext } from '@playwright/test';
import * as fs from 'fs';

const WEB_BASE = 'https://localhost:7275';

test.describe.serial('Debug Login', () => {
  let context: BrowserContext;
  let page: Page;

  test.beforeAll(async ({ playwright }) => {
    context = await playwright.chromium.launchPersistentContext('', {
      headless: false,
      ignoreHTTPSErrors: true,
    });
    page = await context.newPage();
  });

  test.afterAll(async () => {
    await context.close();
  });

  test('Debug: ver página completa', async () => {
    await page.goto(`${WEB_BASE}/login`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);

    // Select App
    const appCombo = page.getByRole('combobox', { name: /Aplicaci/i });
    await appCombo.waitFor({ state: 'visible', timeout: 15000 });
    await appCombo.click();
    await page.getByRole('option', { name: /AccessPlat|PASSPLAT/i }).click();
    await page.waitForTimeout(500);

    // Select Tenant
    const tenantCombo = page.getByRole('combobox', { name: /Tenant/i });
    await tenantCombo.waitFor({ state: 'visible', timeout: 15000 });
    await tenantCombo.click();
    await page.getByRole('option', { name: /Plataforma/i }).click();
    await page.waitForTimeout(3000);

    // Print page HTML for debugging
    const html = await page.content();
    fs.writeFileSync('debug-page.html', html);
    console.log('HTML written to debug-page.html, length:', html.length);
    
    // Also check for provider buttons
    const providerButtons = page.locator('button');
    const count = await providerButtons.count();
    console.log('Total buttons:', count);
    
    for (let i = 0; i < count; i++) {
      const btn = providerButtons.nth(i);
      const className = await btn.getAttribute('class');
      const text = await btn.textContent();
      console.log(`Button ${i}: class="${className}" text="${text}"`);
    }
  });
});