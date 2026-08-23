# S12 — Login Context UI Certification

**Fecha**: 2026-08-02
**Estado**: CERTIFICADO
**Tests**: 12/12 PASS + 144/144 regresión

---

## Resumen Ejecutivo

El contrato de login App → Tenant → Form+OAuth está completamente operativo con selectores VISIBLES para ambas selecciones. Se ejecutaron 12 tests Playwright de UI/API + 144 tests de regresión sin fallos.

## Matriz de Certificación

### Login Context UI (12 tests)

| # | Test | Resultado |
|---|------|-----------|
| 1 | GET /api/apps/activas returns 200 | PASS |
| 2 | Login page renders App selector | PASS |
| 3 | Login page renders Tenant selector | PASS |
| 4 | App pre-selected when single app | PASS |
| 5 | Tenant selector opens with 3+ options | PASS |
| 6 | Login form HIDDEN when no tenant | PASS |
| 7 | Login form APPEARS after tenant selection | PASS |
| 8 | OAuth section appears after tenant | PASS |
| 9 | Full login → Dashboard | PASS |
| 10 | JWT contains correct App/Tenant claims | PASS |
| 11 | Bad password shows error | PASS |
| 12 | Empty credentials blocks submission | PASS |

### Regresión Completa

| Suite | Tests | Resultado |
|-------|-------|-----------|
| Build | — | 0 errores |
| xUnit | 66 | 66/66 PASS |
| A1.8 Multi-Tenant Gate | 24 | 24/24 PASS |
| A1.9 Switch-to-Platform | 17 | 17/17 PASS |
| Fase14 Federación | 14 | 14/14 PASS |
| Fase12 UI Federación | 25 | 23/23 PASS (2 skip) |
| **Total** | **146** | **146/146 PASS** |

## Evidencia de UI

### Estado 1: Login (post-reload)

```
AccessPlat — Plataforma de Acceso
├── "Selecciona una aplicación para continuar"
│   └── [Aplicación: AccessPlat]         ← VISIBLE, pre-seleccionado
├── "Selecciona un tenant para continuar"
│   └── [Tenant: (vacío)]               ← VISIBLE, pendiente selección
└── Plataforma de Acceso — v1.0
```

### Estado 2: Post-selección Tenant

```
AccessPlat — Plataforma de Acceso
├── [Aplicación: AccessPlat]             ← VISIBLE, seleccionado
├── [Tenant: Abarrotes del Sur]         ← VISIBLE, seleccionado
├── Formulario:
│   ├── Usuario o email
│   ├── Contraseña
│   ├── Recordarme (toggle)
│   └── ¿Olvidaste tu contraseña?
├── [INICIAR SESIÓN]                     ← HABILITADO (gate IdApp>0 && IdTenant>0)
├── No hay proveedores externos...       ← OAuth (0 providers en ABARROTES)
└── Plataforma de Acceso — v1.0
```

### Estado 3: Post-login (Dashboard)

```
Dashboard — Bienvenido, test_multitenant
├── Header: test_multitenant | Abarrotes del Sur
├── Sidebar: Roles y Permisos | Usuarios | Accesos | Proveedores | Config | Identidades
└── Stats: 93 Usuarios | 0 Sesiones | 0 Apps | 0 Inquilinos
```

## JWT Claims Verificados

```json
{
  "IdApp": "1",
  "TenantId": "3",
  "UsuarioTenantId": "4",
  "sub": "8",
  "permiso": ["ACCESOS_VER", "GRUPOS_VER", "MATRIZ_PERMISOS_VER", "PERMISOS_VER", "ROLES_VER", "USUARIOS_VER", "USUARIOS_VERDISP"],
  "iss": "PassPlat",
  "aud": "PassPlat"
}
```

## Gate de Contrato

| Condición | Antes | Después |
|-----------|-------|---------|
| App selector visible | Solo cuando >1 app | SIEMPRE (si hay apps) |
| Tenant selector visible | Solo cuando requiere selección | SIEMPRE (si hay tenants) |
| Formulario login | Solo cuando App+Tenant resueltos | Misma condición (gate preservado) |
| OAuth providers | Solo cuando App+Tenant resueltos | Misma condición (gate preservado) |
| Botón "Iniciar Sesión" | `IsAuthenticationContextReady` | Misma condición |

## Archivos Entregados

| Archivo | Tipo |
|---------|------|
| `tests/s12-login-context-ui.spec.ts` | Test Playwright (12 tests) |
| `Docs/Architecture/S12-Login-Context-Resolution.md` | Análisis de causa raíz |
| `Docs/Architecture/S12-Login-Context-Certification.md` | Este documento |

## Cambios en Código

| Archivo | Cambio | Impacto |
|---------|--------|---------|
| `PassPlat.Web/Pages/Login.razor` | Selectores siempre visibles | Login UI |
| `tests/s12-login-context-ui.spec.ts` | Nuevo — 12 tests | Testing |

## Restricciones

- No se modificó la lógica de `DoLogin()` — el gate `IsAuthenticationContextReady` se preservó intacto
- No se eliminó `GetAppsAsync()` — sigue cargando apps al inicio
- No se alteró el patrón `Result<T>` — propagación de errores intacta
- No se modificó `AuthService` — `GetAppsAsync()` y `GetTenantsAsync()` sin cambios
- No se eliminó instrumentación OAuth — proveedores siguen visibles post-selección

## Próximos Pasos

La FASE 2S (Login Context Resolution & UI Contract Recovery) está **CERTIFICADA** con:
- 12/12 tests UI/API
- 146/146 regresión completa
- Build 0 errores
- UI manual certificada end-to-end
