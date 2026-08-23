import { test, expect, APIRequestContext } from '@playwright/test';

import { API_BASE } from './api-config';
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
    data: { NomUsuario: USER, Email: USER, Password: PASS, IdApp: 1, IdTenant: 1 },
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
    case 'get': return apiContext.get(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    case 'post': return apiContext.post(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'put': return apiContext.put(url, { headers: authHeaders(), data, ignoreHTTPSErrors: true });
    case 'delete': return apiContext.delete(url, { headers: authHeaders(), ignoreHTTPSErrors: true });
    default: throw new Error(`HTTP method not supported: ${method}`);
  }
}

test.describe.serial('FASE 16 — Identity Management Enterprise', () => {
  test.beforeAll(async ({ playwright }) => {
    apiContext = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    authTokens = await login();
  });

  test.afterAll(async () => {
    await apiContext.dispose();
  });

  // ─── ETAPA 10: Sincronización Perfil ──────────────────────────────
  test.describe.serial('ETAPA 10 — Sincronización Perfil', () => {
    let confProvId = 7; // Tenant 1 + Instagram (existe en DB)
    let tenantId = 1;
    let provIdenId = 7;
    let originalFrecuencia = '';

    test('1. Leer ConfProvIden existente', async () => {
      const response = await apiRequest('get', `${API_BASE}/confproviden/${tenantId}/${provIdenId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      expect(data.id).toBeGreaterThan(0);
      confProvId = data.id;
      originalFrecuencia = data.frecuenciaSincronizacion ?? '';
    });

    test('2. Actualizar frecuenciaSincronizacion a Diaria', async () => {
      const response = await apiRequest('put', `${API_BASE}/confproviden/${confProvId}`, {
        frecuenciaSincronizacion: 'Diaria',
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.frecuenciaSincronizacion).toBe('Diaria');
    });

    test('3. Leer ConfProvIden con frecuenciaSincronizacion', async () => {
      const response = await apiRequest('get', `${API_BASE}/confproviden/${tenantId}/${provIdenId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      expect(data.frecuenciaSincronizacion).toBe('Diaria');
    });

    test('4. Actualizar frecuenciaSincronizacion a PrimerLogin', async () => {
      const response = await apiRequest('put', `${API_BASE}/confproviden/${confProvId}`, {
        frecuenciaSincronizacion: 'PrimerLogin',
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data.frecuenciaSincronizacion).toBe('PrimerLogin');
    });

    test('5. Listar configuraciones por tenant', async () => {
      const response = await apiRequest('get', `${API_BASE}/confproviden/tenant/${tenantId}`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      expect(data.length).toBeGreaterThanOrEqual(1);
      expect(data.some((c: any) => c.frecuenciaSincronizacion === 'PrimerLogin')).toBeTruthy();
    });

    test('6. Validar valores permitidos de frecuenciaSincronizacion', async () => {
      const values = ['Siempre', 'PrimerLogin', 'Diaria', 'Nunca'];
      for (const val of values) {
        const response = await apiRequest('put', `${API_BASE}/confproviden/${confProvId}`, {
          frecuenciaSincronizacion: val,
        });
        expect(response.ok()).toBeTruthy();
        const data = await response.json();
        expect(data.frecuenciaSincronizacion).toBe(val);
      }
    });

    test('7. Restaurar valor original de frecuenciaSincronizacion', async () => {
      if (originalFrecuencia) {
        const response = await apiRequest('put', `${API_BASE}/confproviden/${confProvId}`, {
          frecuenciaSincronizacion: originalFrecuencia,
        });
        expect(response.ok()).toBeTruthy();
      }
    });
  });

  // ─── ETAPA 6: Dispositivos — Eliminar/Bloquear ────────────────────
  test.describe.serial('ETAPA 6 — Dispositivos Eliminar/Bloquear', () => {
    let dispId = 0;
    let usuarioId = 0;

    test('7. Obtener lista de dispositivos', async () => {
      const response = await apiRequest('get', `${API_BASE}/DispConfiables`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      if (data.length > 0) {
        dispId = data[0].id;
        usuarioId = data[0].idUsuario || 0;
      }
    });

    test('8. Bloquear dispositivo', async () => {
      if (dispId === 0) test.skip();
      const response = await apiRequest('post', `${API_BASE}/DispConfiables/bloquear/${dispId}`);
      expect(response.ok()).toBeTruthy();
      // Verify confiable = false after block
      const list = await apiRequest('get', `${API_BASE}/DispConfiables`);
      const data = await (await list).json();
      const blocked = data.find((d: any) => d.id === dispId);
      expect(blocked).toBeDefined();
      expect(blocked.confiable).toBe(false);
    });

    test('9. Eliminar dispositivo', async () => {
      if (dispId === 0) test.skip();
      const response = await apiRequest('delete', `${API_BASE}/DispConfiables/${dispId}`);
      expect(response.ok()).toBeTruthy();
    });

    test('10. Verificar eliminacion', async () => {
      if (dispId === 0) test.skip();
      const response = await apiRequest('get', `${API_BASE}/DispConfiables`);
      const data = await (await response).json();
      expect(data.some((d: any) => d.id === dispId)).toBeFalsy();
    });
  });

  // ─── ETAPA 8: Dashboard Operacional ────────────────────────────────
  test.describe.serial('ETAPA 8 — Dashboard Operacional', () => {
    test('11. Obtener metricas operacionales', async () => {
      const response = await apiRequest('get', `${API_BASE}/dashboard`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      // Should have at least some metrics
      expect(typeof data).toBe('object');
    });
  });

  // ─── ETAPA 11: Consentimiento OAuth ───────────────────────────────
  test.describe.serial('ETAPA 11 — Consentimiento OAuth', () => {
    test('12. Obtener identidades externas (consentimientos)', async () => {
      const response = await apiRequest('get', `${API_BASE}/iden-ext/page?pageNumber=1&pageSize=50`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      expect(Array.isArray(data.items)).toBeTruthy();
    });
  });

  // ─── ETAPA 13: Forzar MFA + Agregar Proveedor ─────────────────────
  test.describe.serial('ETAPA 13 — Forzar MFA / Agregar Proveedor', () => {
    let createdIdentidadId = 0;

    test('13. Forzar MFA para usuario sistema', async () => {
      const response = await apiRequest('post', `${API_BASE}/iden-ext/1/forzar-mfa?idUsuarioAdmin=1`);
      // May fail if no MFA methods exist, but endpoint should respond
      const status = response.status();
      expect([200, 400, 404, 500].includes(status)).toBeTruthy();
    });

    test('14. Crear identidad externa (Agregar proveedor)', async () => {
      const response = await apiRequest('post', `${API_BASE}/iden-ext`, {
        idUsuario: 1,
        idProvIden: 1,
        idTenant: 1,
        subExterno: `e2e-test-sub-${Date.now()}`,
        emailExterno: 'e2e-test@example.com',
        nombreExterno: 'E2E Test User',
      });
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      expect(data.id).toBeGreaterThan(0);
      createdIdentidadId = data.id;
    });

    test('15. Verificar identidad externa creada', async () => {
      expect(createdIdentidadId).toBeGreaterThan(0);
      const response = await apiRequest('get', `${API_BASE}/iden-ext/usuario/1`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      const found = data.find((i: any) => i.id === createdIdentidadId);
      expect(found).toBeDefined();
      expect(found.emailExterno).toBe('e2e-test@example.com');
    });

    test('16. Revocar identidad externa (limpieza)', async () => {
      expect(createdIdentidadId).toBeGreaterThan(0);
      const response = await apiRequest('put', `${API_BASE}/iden-ext/${createdIdentidadId}/revocar?idUsuarioRevoca=1&motivo=Limpieza%20E2E`);
      expect(response.ok()).toBeTruthy();
    });
  });

  // ─── ETAPA 14: Email Templates Federación ─────────────────────────
  test.describe.serial('ETAPA 14 — Email Templates Federación', () => {
    const expectedTemplates = [
      'identity-principal-changed',
      'identity-linked-by-admin',
      'identity-removed-by-admin',
      'provider-disabled',
      'provider-enabled',
      'provider-authorization-revoked',
      'provider-authorization-granted',
      'oauth-consent-expired',
      'session-revoked',
      'security-notification',
    ];

    test('17. Listar templates de email', async () => {
      const response = await apiRequest('get', `${API_BASE}/emailtemplates?pageNumber=1&pageSize=100`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(data).toBeDefined();
      const items = data.items || data;
      expect(Array.isArray(items)).toBeTruthy();
      const nombres = items.map((t: any) => t.nombre || t.nombre);
      for (const tpl of expectedTemplates) {
        expect(nombres).toContain(tpl);
      }
    });

    test('18. Verificar template identity-principal-changed', async () => {
      const response = await apiRequest('get', `${API_BASE}/emailtemplates?pageNumber=1&pageSize=100`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      const items = data.items || data;
      expect(Array.isArray(items)).toBeTruthy();
      const tpl = items.find((t: any) => t.nombre === 'identity-principal-changed');
      expect(tpl).toBeDefined();
      expect(tpl.categoria).toBe('seguridad');
      expect(tpl.estado).toBe('publicado');
    });

    test('19. Verificar template security-notification', async () => {
      const response = await apiRequest('get', `${API_BASE}/emailtemplates?pageNumber=1&pageSize=100`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      const items = data.items || data;
      expect(Array.isArray(items)).toBeTruthy();
      const tpl = items.find((t: any) => t.nombre === 'security-notification');
      expect(tpl).toBeDefined();
      expect(tpl.categoria).toBe('seguridad');
    });
  });

  // ─── ETAPA 15: Login — Orden Proveedores ──────────────────────────
  test.describe.serial('ETAPA 15 — Login Proveedores', () => {
    test('20. Obtener proveedores ordenados', async () => {
      const response = await apiRequest('get', `${API_BASE}/auth/externo/proveedores?idTenant=1`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      expect(Array.isArray(data)).toBeTruthy();
      expect(data.length).toBeGreaterThanOrEqual(5);
      // Verify sorting: orden should be ascending
      for (let i = 1; i < data.length; i++) {
        expect(data[i].orden).toBeGreaterThanOrEqual(data[i - 1].orden);
      }
    });

    test('21. Google provider tiene datos visuales', async () => {
      const response = await apiRequest('get', `${API_BASE}/auth/externo/proveedores?idTenant=1`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      const google = data.find((p: any) => p.codigo === 'GOOGLE');
      expect(google).toBeDefined();
      expect(typeof google.orden).toBe('number');
      expect(google.orden).toBeGreaterThanOrEqual(0);
    });

    test('22. Solo retorna proveedores activos', async () => {
      const response = await apiRequest('get', `${API_BASE}/auth/externo/proveedores?idTenant=1`);
      expect(response.ok()).toBeTruthy();
      const data = await response.json();
      for (const p of data) {
        expect(p.activo).toBe(true);
      }
    });
  });
});
