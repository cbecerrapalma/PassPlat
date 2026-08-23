import { test, expect, APIRequestContext } from '@playwright/test';

import { API } from '../api-config';
const USER = 'sistema';
const PASS = 'Admin@123';

interface AuthTokens { accessToken: string; refreshToken: string; jwtRaw: string; }

let apiContext: APIRequestContext;
let authTokens: AuthTokens;

async function login(): Promise<AuthTokens> {
  const response = await apiContext.post(`${API}/auth/login`, {
    data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
    ignoreHTTPSErrors: true,
  });
  expect(response.ok()).toBeTruthy();
  const data = await response.json();
  return { accessToken: data.accessToken, refreshToken: data.refreshToken, jwtRaw: data.accessToken };
}

function authHeaders(): Record<string, string> {
  return {
    Authorization: `Bearer ${authTokens.accessToken}`,
    'Content-Type': 'application/json',
    Accept: 'application/json',
  };
}

async function apiRequest(method: string, url: string, data?: any) {
  switch (method) {
    case 'get': return apiContext.get(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    case 'post': return apiContext.post(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'put': return apiContext.put(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'delete': return apiContext.delete(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    default: throw new Error(`HTTP method not supported: ${method}`);
  }
}

test.describe.serial('C1_Login_MFA', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('Login con MFA', () => {
    test('1. Login exitoso con MFA — JWT emitido', async () => {
      const response = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      authTokens = {
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        jwtRaw: data.accessToken,
      };

      const mfaPage = await apiContext.get(`${API}/mfa/validar`);
      await mfaPage.waitForURL(/mfa\/validar/);
      await mfaPage.click('button:has-text("Iniciar sesión")');
      await mfaPage.waitForLoad();

      const jwt = await apiContext.post(`${API}/mfa/validar`, {
        data: { IdUsuario: 1, IdTenant: 1, IdTipoMFA: 1, IdMFA: '123456' },
        ignoreHTTPSErrors: true,
      });
      expect(jwt.ok()).toBeTruthy();
      const jwtData = await jwt.json();
      expect(jwtData.token).toBe(authTokens.accessToken);
    });

    test('2. MFA fallida — código inválido', async () => {
      const response = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      authTokens = {
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        jwtRaw: data.accessToken,
      };

      const response2 = await apiContext.post(`${API}/mfa/validar`, {
        data: { IdUsuario: 1, IdTenant: 1, IdTipoMFA: 1, IdMFA: '000000' },
        ignoreHTTPSErrors: true,
      });
      expect(response2.ok()).toBeTruthy();
      const jwtData = await response2.json();
      expect(jwtData.ok).toBeFalsy();
    });

    test('3. Login con NomUsuario sin Email', async () => {
      const response = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      authTokens = {
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        jwtRaw: data.accessToken,
      };
      expect(data.idTenant).toBeNull();
    });
  });
});