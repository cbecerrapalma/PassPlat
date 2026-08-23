import { test, expect, APIRequestContext } from '@playwright/test';

import { API_BASE } from './api-config';
const TENANT = 'Plataforma';
const USER = 'sistema';
const PASS = 'Admin@123';

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

let authTokens: AuthTokens;
let apiContext: APIRequestContext;

async function login(): Promise<AuthTokens> {
  const response = await apiContext.post(`${API_BASE}/auth/login`, {
    data: {
      NomUsuario: USER,
      Email: USER,
      Password: PASS,
      IdApp: 1,
      IdTenant: 1,
    },
    ignoreHTTPSErrors: true,
  });
  expect(response.ok()).toBeTruthy();
  const data = await response.json();
  return { accessToken: data.accessToken, refreshToken: data.refreshToken };
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
    case 'get':
      return apiContext.get(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    case 'post':
      return apiContext.post(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'put':
      return apiContext.put(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'delete':
      return apiContext.delete(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    default:
      throw new Error(`HTTP method not supported: ${method}`);
  }
}

test.describe('Validación CRUD - Apps, Grupos, Permisos', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  test.describe('MÓDULO: Apps', () => {
    let createdAppId: number;

    test('CREATE - Crear App', async () => {
      const uniqueCode = `TEST_APP_${Date.now()}`;
      const response = await apiRequest('post', `${API_BASE}/apps`, {
        codigo: uniqueCode,
        nombre: `App de Prueba ${Date.now()}`,
        urlBase: 'https://test.app.com',
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.id).toBeGreaterThan(0);
      expect(data.codigo).toBe(uniqueCode);
      expect(data.activa).toBe(true);
      createdAppId = data.id;
    });

    test('READ - Obtener App por ID', async () => {
      const response = await apiRequest('get', `${API_BASE}/apps/${createdAppId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.id).toBe(createdAppId);
    });

    test('READ - Listar Apps paginado', async () => {
      const response = await apiRequest('get', `${API_BASE}/apps/page?pageNumber=1&pageSize=10`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.items).toBeDefined();
      expect(data.totalCount).toBeGreaterThanOrEqual(1);
    });

    test('UPDATE - Desactivar App', async () => {
      const response = await apiRequest('post', `${API_BASE}/apps/${createdAppId}/desactivar`);
      expect(response.ok()).toBeTruthy();
      const getResponse = await apiRequest('get', `${API_BASE}/apps/${createdAppId}`);
      const data = await getResponse.json();
      expect(data.activa).toBe(false);
    });
  });

  test.describe('MÓDULO: Grupos', () => {
    let createdGrupoId: number;
    const testTenantId = 1;

    test('CREATE - Crear Grupo', async () => {
      const uniqueCode = `TEST_GRUPO_${Date.now()}`;
      const response = await apiRequest('post', `${API_BASE}/grupos`, {
        codigo: uniqueCode,
        nombre: `Grupo de Prueba ${Date.now()}`,
        descripcion: 'Grupo creado por test automatizado',
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.id).toBeGreaterThan(0);
      expect(data.codigo).toBe(uniqueCode);
      expect(data.activo).toBe(true);
      createdGrupoId = data.id;
    });

    test('READ - Obtener Grupos por Tenant', async () => {
      const response = await apiRequest('get', `${API_BASE}/grupos/tenant/${testTenantId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      expect(data.some((g: any) => g.id === createdGrupoId)).toBeTruthy();
    });

    test('UPDATE - Actualizar Grupo', async () => {
      const response = await apiRequest('put', `${API_BASE}/grupos/${createdGrupoId}`, {
        nombre: 'Grupo Actualizado por Test',
        descripcion: 'Descripción actualizada',
        activo: true,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.nombre).toBe('Grupo Actualizado por Test');
    });

    test('DELETE - Eliminar Grupo (Soft Delete)', async () => {
      const response = await apiRequest('delete', `${API_BASE}/grupos/${createdGrupoId}`);
      expect(response.ok()).toBeTruthy();
      expect(response.status()).toBe(204);
      const getResponse = await apiRequest('get', `${API_BASE}/grupos/tenant/${testTenantId}`);
      const data = await getResponse.json();
      const deleted = data.find((g: any) => g.id === createdGrupoId);
      expect(deleted?.activo).toBe(false);
    });
  });

  test.describe('MÓDULO: Permisos', () => {
    let createdPermisoId: number;

    test('CREATE - Crear Permiso', async () => {
      const uniqueCode = `TEST_PERM_${Date.now()}`;
      const response = await apiRequest('post', `${API_BASE}/permisos`, {
        codigo: uniqueCode,
        nombre: `Permiso de Prueba ${Date.now()}`,
        descripcion: 'Permiso creado por test automatizado',
        idModulo: 103,
        orden: 99,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.id).toBeGreaterThan(0);
      expect(data.codigo).toBe(uniqueCode);
      expect(data.activo).toBe(true);
      createdPermisoId = data.id;
    });

    test('READ - Obtener Permisos Activos', async () => {
      const response = await apiRequest('get', `${API_BASE}/permisos/activos`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      expect(data.some((p: any) => p.id === createdPermisoId)).toBeTruthy();
    });

    test('READ - Obtener Permiso por ID', async () => {
      const response = await apiRequest('get', `${API_BASE}/permisos/${createdPermisoId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.id).toBe(createdPermisoId);
    });

    test('UPDATE - Actualizar Permiso', async () => {
      const response = await apiRequest('put', `${API_BASE}/permisos/${createdPermisoId}`, {
        codigo: `TEST_PERM_UPD_${Date.now()}`,
        nombre: 'Permiso Actualizado por Test',
        descripcion: 'Descripción actualizada',
        idModulo: 103,
        orden: 100,
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.nombre).toBe('Permiso Actualizado por Test');
    });

    test('DELETE - Desactivar Permiso (Soft Delete)', async () => {
      const response = await apiRequest('delete', `${API_BASE}/permisos/${createdPermisoId}`);
      expect(response.ok()).toBeTruthy();
      expect(response.status()).toBe(204);
      const getResponse = await apiRequest('get', `${API_BASE}/permisos/activos`);
      const data = await getResponse.json();
      expect(data.some((p: any) => p.id === createdPermisoId)).toBeFalsy();
    });
  });
});
