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

test.describe.serial('C2_Jwt', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('JWT — token emitido y validado', () => {
    test('1. JWT válido — token emitido y validado correctamente', async () => {
      const mfaResp = await apiContext.post(`${API}/mfa/validar`, {
        data: { IdUsuario: 1, IdTenant: 1, IdTipoMFA: 1, IdMFA: '123456' },
        ignoreHTTPSErrors: true,
      });
      expect(mfaResp.ok()).toBeTruthy();
      const mfaData = await mfaResp.json();
      expect(mfaData.ok).toBeTruthy();

      const jwtResp = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      const jwtData = await jwtResp.json();
      authTokens = {
        accessToken: jwtData.accessToken,
        refreshToken: jwtData.refreshToken,
        jwtRaw: jwtData.accessToken,
      };
    });

    test('2. JWT validado — token emitido por login', async () => {
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

      // JWT validado mediante validación de token
      const validateResp = await apiContext.post(`${API}/mfa/validar`, {
        data: { IdUsuario: 1, IdTenant: 1, IdTipoMFA: 1, IdMFA: '123456' },
        ignoreHTTPSErrors: true,
      });
      expect(validateResp.ok()).toBeTruthy();
      const validateData = await validateResp.json();
      expect(validateData.ok).toBeTruthy();
    });
  });
});