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

test.describe.serial('C4_Email', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('Email — queue, envío y fallback', () => {
    test('1. Email encolado — Email_Queued', async () => {
      const response = await apiContext.post(`${API}/email/queue`, {
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
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.eventName).toBe('Email_Queued');
      expect(data.scope).toBe('email');
      expect(data.correlationId).toBe('corr-001');
    });

    test('2. Email enviado — Email_Sent', async () => {
      const response = await apiContext.post(`${API}/email/send`, {
        data: {
          toEmail: 'sistema@test.com',
          subject: 'Prueba de email',
          body: 'Mensaje de prueba',
        },
        ignoreHTTPSErrors: true,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.eventName).toBe('Email_Sent');
      expect(data.scope).toBe('email');
    });
  });
});