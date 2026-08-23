import { test, expect } from '@playwright/test';

import { API_BASE } from './api-config';

test('Debug módulos', async ({ playwright }) => {
  const apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
  
  // Login
  const loginResponse = await apiContext.post(`${API_BASE}/auth/login`, {
    data: {
      NomUsuario: 'sistema',
      Email: 'sistema',
      Password: 'Admin@123',
      IdApp: 1,
      IdTenant: 1
    },
    ignoreHTTPSErrors: true
  });
  
  const loginData = await loginResponse.json();
  const token = loginData.accessToken;
  
  // Get modulos
  const modulosResponse = await apiContext.get(`${API_BASE}/modulos`, {
    headers: { 'Authorization': `Bearer ${token}` },
    ignoreHTTPSErrors: true
  });
  
  const modulos = await modulosResponse.json();
  console.log('Módulos:', JSON.stringify(modulos, null, 2));
  
  await apiContext.dispose();
});