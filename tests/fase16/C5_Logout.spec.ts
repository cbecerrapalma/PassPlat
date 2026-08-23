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

test.describe.serial('C5_Logout', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('Logout', () => {
    test('1. Cerrar sesión — logout exitoso, evento emitido', async () => {
      const logoutResponse = await apiContext.post(`${API}/auth/logout`, {
        data: { token: authTokens.accessToken },
        ignoreHTTPSErrors: true,
      });
      expect(logoutResponse.ok()).toBeTruthy();
      const logoutData = await logoutResponse.json();
      expect(logoutData.mensaje).toBe('Sesión cerrada');
      expect(logoutData.eventName).toBe('Logout');
      expect(logoutData.scope).toBe('authentication');
    });

    test('2. Cerrar sesión — error si token inválido', async () => {
      const logoutResponse = await apiContext.post(`${API}/auth/logout`, {
        data: { token: 'invalid_token_xyz' },
        ignoreHTTPSErrors: true,
      });
      expect(logoutResponse.ok()).toBeTruthy();
      const logoutData = await logoutResponse.json();
      expect(logoutData.mensaje).toBe('Sesión cerrada');
    });
  });
});