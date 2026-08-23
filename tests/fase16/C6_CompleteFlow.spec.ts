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

test.describe.serial('C6_CompleteFlow', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('Flujo completo — Login → MFA → JWT → Caché → Email → Logout', () => {
    test('1. Login exitoso con NomUsuario', async () => {
      const loginResp = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      expect(loginResp.ok()).toBeTruthy();
      const loginData = await loginResp.json();
      expect(loginData.idTenant).toBeNull();
    });

    test('2. MFA validado — JWT emitido', async () => {
      const mfaResp = await apiContext.post(`${API}/mfa/validar`, {
        data: { IdUsuario: 1, IdTenant: 1, IdTipoMFA: 1, IdMFA: '123456' },
        ignoreHTTPSErrors: true,
      });
      expect(mfaResp.ok()).toBeTruthy();
      const mfaData = await mfaResp.json();
      expect(mfaData.ok).toBeTruthy();
    });

    test('3. JWT validado — token emitido correctamente', async () => {
      const loginResp = await apiContext.post(`${API}/auth/login`, {
        data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
        ignoreHTTPSErrors: true,
      });
      expect(loginResp.ok()).toBeTruthy();
      const loginData = await loginResp.json();
      authTokens = {
        accessToken: loginData.accessToken,
        refreshToken: loginData.refreshToken,
        jwtRaw: loginData.accessToken,
      };
    });

    test('4. Cache MISS — primera consulta PolíticaPwd', async () => {
      const cacheResp = await apiContext.get(`${API}/politicas-pwd/all`);
      expect(cacheResp.ok()).toBeTruthy();
      const cacheData = await cacheResp.json();
      expect(cacheData.cache_result).toBe('miss');
      expect(cacheData.eventName).toBe('Cache_Miss');
      expect(cacheData.scope).toBe('caching');
    });

    test('5. Cache HIT — segunda consulta PolíticaPwd', async () => {
      const cacheResp = await apiContext.get(`${API}/politicas-pwd/all`);
      expect(cacheResp.ok()).toBeTruthy();
      const cacheData = await cacheResp.json();
      expect(cacheData.cache_result).toBe('hit');
      expect(cacheData.eventName).toBe('Cache_Hit');
      expect(cacheData.scope).toBe('caching');
    });

    test('6. Cache invalidación — tras modificación', async () => {
      const invalidateResp = await apiContext.post(`${API}/politicas-pwd/invalidar`, {
        data: { key: 'test_key' },
        ignoreHTTPSErrors: true,
      });
      expect(invalidateResp.ok()).toBeTruthy();
      const invalidateData = await invalidateResp.json();
      expect(invalidateData.cache_result).toBe('invalidation');
      expect(invalidateData.eventName).toBe('Cache_Invalidation');
      expect(invalidateData.scope).toBe('caching');
    });

    test('7. Nueva consulta — Cache_MISS (re-validación)', async () => {
      const cacheResp = await apiContext.get(`${API}/politicas-pwd/all`);
      expect(cacheResp.ok()).toBeTruthy();
      const cacheData = await cacheResp.json();
      expect(cacheData.cache_result).toBe('miss');
      expect(cacheData.eventName).toBe('Cache_Miss');
      expect(cacheData.scope).toBe('caching');
    });

    test('8. Email — Email_Queued emitido correctamente', async () => {
      const emailResp = await apiContext.post(`${API}/email/queue`, {
        data: {
          kind: 'PasswordChanged',
          toEmail: 'sistema@test.com',
          userName: 'sistema',
          extra: {},
          idTenant: 1,
          idUsuario: 1,
          idApp: 1,
          correlationId: 'corr-001',
        },
        ignoreHTTPSErrors: true,
      });
      expect(emailResp.ok()).toBeTruthy();
      const emailData = await emailResp.json();
      expect(emailData.eventName).toBe('Email_Queued');
      expect(emailData.scope).toBe('email');
      expect(emailData.correlationId).toBe('corr-001');
    });

    test('9. Logout — logout exitoso, evento emitido', async () => {
      const logoutResp = await apiContext.post(`${API}/auth/logout`, {
        data: { token: authTokens.accessToken },
        ignoreHTTPSErrors: true,
      });
      expect(logoutResp.ok()).toBeTruthy();
      const logoutData = await logoutResp.json();
      expect(logoutData.mensaje).toBe('Sesión cerrada');
      expect(logoutData.eventName).toBe('Logout');
      expect(logoutData.scope).toBe('authentication');
    });
  });
});