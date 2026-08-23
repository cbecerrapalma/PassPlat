# PassPlat — Auditoría Completa FASE 1-2
**Fecha**: 18-Junio-2026  
**Auditor**: Playwright MCP + Filesystem MCP  
**Estado**: FASE 1 (Descubrimiento) COMPLETADA, FASE 2 (Funcional) INICIADA

---

## RESUMEN EJECUTIVO

| Métrica | Valor |
|---------|-------|
| Páginas únicas descubiertas | 22 |
| Enlaces muertos | 3 |
| Errores de consola (app) | 0 |
| Errores de consola (extensiones) | 7 (1Password Chrome) |
| Llamadas API totales | 237+ |
| Llamadas API con error | 0 (todas 200 OK) |
| Problemas de duplicación | 2 (doble carga dashboard, ruta duplicada) |
| Headers con ruta raw | 3 (admin/roles, admin/permisos, admin/matriz-permisos) |

---

## MAPA DE NAVEGACIÓN COMPLETO

### Sidebar Structure

```
CATÁLOGOS
├── Gestión [EXPANDED]
│   ├── Tenants           → /tenants           ✅ FUNCIONAL
│   ├── Apps              → /apps              ✅ FUNCIONAL
│   ├── Roles y Permisos  → /admin/roles       ✅ FUNCIONAL
│   ├── Políticas de Contraseña → /politicas-pwd ✅ FUNCIONAL
│   └── Config App        → /config-app        ✅ FUNCIONAL
SEGURIDAD
├── Control [EXPANDED]
│   ├── Usuarios          → /usuarios          ✅ FUNCIONAL
│   └── Accesos           → /accesos           ✅ FUNCIONAL
IAM
├── Permisos [COLLAPSED by default]
│   ├── Roles             → /admin/roles       ⚠️ DUPLICADO (mismo que CATÁLOGOS)
│   ├── Permisos          → /admin/permisos    ✅ FUNCIONAL
│   ├── Grupos            → /admin/grupos      ✅ FUNCIONAL
│   ├── Permisos Directos → /admin/roles-permisos ✅ FUNCIONAL
│   └── Matriz de Permisos→ /admin/matriz-permisos ✅ FUNCIONAL
MONITOREO
├── Registros [COLLAPSED by default]
│   ├── Auditoría         → /auditoria         ✅ FUNCIONAL
│   ├── Historial de Contraseñas → /historial-pwd ✅ FUNCIONAL
│   ├── Intentos Acceso   → /intentos-acceso   ✅ FUNCIONAL
│   ├── Notificaciones    → /notificaciones    ✅ FUNCIONAL
│   └── Mantenimiento     → /mantenimiento     ✅ FUNCIONAL
COMUNICACIONES
├── Email [EXPANDED]
│   ├── Providers         → /email/providers   ✅ FUNCIONAL
│   ├── Cuentas de Correo → /email/accounts    ✅ FUNCIONAL
│   ├── Cuentas x Tenant  → /email/tenant-accounts ✅ FUNCIONAL
│   ├── Cuentas x App     → /email/app-accounts ✅ FUNCIONAL
│   └── Plantillas de Email → /email-templates ✅ FUNCIONAL
```

### Dashboard Quick-Access Links

| Link | URL | Estado |
|------|-----|--------|
| Usuarios | /usuarios | ✅ FUNCIONAL |
| Roles | /roles | ❌ DEAD LINK → "Not found" |
| Inquilinos | /tenants | ✅ FUNCIONAL |
| Sesiones | /sesiones | ❌ DEAD LINK → redirect a login |
| Bloqueos | /bloqueos | ❌ DEAD LINK → redirect a login |
| Auditoría | /auditoria | ✅ FUNCIONAL |
| Intentos | /intentos-acceso | ✅ FUNCIONAL |
| Notificaciones | /notificaciones | ✅ FUNCIONAL |

---

## HALLAZGOS POR PÁGINA

### 1. Dashboard (`/`)
- **KPIs**: 2 Usuarios, 10 Sesiones, 1 App, 1 Tenant
- **Actividad Reciente**: Login exitoso 18/6/2026 20:50
- **Estado de Seguridad**: 0 usuarios con intentos excedidos, 0 con contraseña expirada
- **Acceso Rápido**: 8 links (3 dead: /roles, /sesiones, /bloqueos)
- **Problema**: 7 endpoints API se llaman 2 veces (doble render)
- **Screenshot**: `00-dashboard.png`

### 2. Tenants (`/tenants`)
- **KPIs**: Total 1, Activos 1, Inactivos 0, Dominios 1
- **Tabla**: PLATFORM tenant, columns: Avatar, Código, Nombre, Tipo, Estado
- **Funciones**: Buscar, Nuevo Tenant, Refresh, Paginación
- **Estado**: ✅ PERFECTO
- **Screenshot**: `01-tenants.png`

### 3. Apps (`/apps`)
- **KPIs**: Total 1, Activas **0** (BUG), Inactivas 0, Total Conteo 1
- **Tabla**: AccessPlat (PASSPLAT), Activo
- **Problema**: KPI "Activas" muestra 0 pero tabla muestra 1 Activo
- **Estado**: ⚠️ BUG EN KPI
- **Screenshot**: `02-apps.png`

### 4. Roles y Permisos (`/admin/roles`)
- **KPIs**: Roles Totales 2, Activos 2 (100%), Globales 1, Por Tenant 1, Usuarios Asignados 2
- **Tabla**: Administrador (60 permisos), Editor (9 permisos)
- **Breadcrumb**: PassPlat > Seguridad > Roles
- **Problema**: Header muestra "Admin/roles" (ruta raw) en vez de "Roles"
- **Estado**: ⚠️ HEADER INCORRECTO
- **Screenshot**: `03-admin-roles.png`

### 5. Políticas de Contraseña (`/politicas-pwd`)
- **KPIs**: Políticas 1, Activas 1, Inactivas 0, Específicas 0
- **Tabla**: DEFAULT (10-64, Activa)
- **Estado**: ✅ PERFECTO
- **Screenshot**: `04-politicas-pwd.png`

### 6. Config App (`/config-app`)
- **Estado**: ✅ FUNCIONAL
- **Screenshot**: `05-config-app.png`

### 7. Usuarios (`/usuarios`)
- **KPIs**: Totales 2, Activos 2, Con MFA 2, Bloqueados 0, Sin Verificar 0
- **Tabla**: sistema (sistema@passplat.app), admin_tenant (admin@passplat.app)
- **Funciones**: Buscar, Filtros, Nuevo Usuario, Paginación
- **Estado**: ✅ PERFECTO
- **Screenshot**: `06-usuarios.png`

### 8. Accesos (`/accesos`)
- **KPIs**: Total 1, Activos 1, Roles Asignados 1, Apps Integradas 1
- **Tabla**: sistema → Plataforma → AccessPlat → Administrador → 16/06/2026 → Activo
- **Funciones**: Buscar, Filtros (Usuario, Estado), Asignar Acceso, Paginación
- **Estado**: ✅ PERFECTO
- **Screenshot**: `07-accesos.png`

### 9. Permisos (`/admin/permisos`)
- **Estado**: ✅ FUNCIONAL (sin errores de app)
- **Screenshot**: `08-permisos.png`

### 10. Grupos (`/admin/grupos`)
- **Estado**: ✅ FUNCIONAL
- **Screenshot**: `09-grupos.png`

### 11. Permisos Directos (`/admin/roles-permisos`)
- **Estado**: ✅ FUNCIONAL
- **Screenshot**: `10-permisos-directos.png`

### 12. Matriz de Permisos (`/admin/matriz-permisos`)
- **Tabla**: Roles × Permisos matrix (Administrador: all assigned, Editor: read-only)
- **Selector**: Dropdown para filtrar por rol
- **Permisos listados**: ~60 permisos en ~15 módulos
- **Problema**: Header muestra "Admin/matriz Permisos" (ruta raw)
- **Estado**: ⚠️ HEADER INCORRECTO
- **Screenshot**: `11-matriz-permisos.png`

### 13-17. MONITOREO Pages
- Auditoría, Historial Pwd, Intentos Acceso, Notificaciones, Mantenimiento — all functional
- **Screenshots**: `12-auditoria.png` through `16-mantenimiento.png`

### 18-22. COMUNICACIONES Pages
- Email Providers, Accounts, Tenant-Accounts, App-Accounts, Templates — all functional
- **Screenshots**: `17-email-providers.png` through `21-email-templates.png`

---

## PROBLEMAS ENCONTRADOS

### CRÍTICOS (0)
Ninguno.

### ALTOS (3)
| # | Problema | Ubicación | Impacto |
|---|----------|-----------|---------|
| 1 | **Dead link /roles** en Dashboard quick-access | Dashboard.razor | Usuario llega a "Not found" |
| 2 | **Dead link /sesiones** en Dashboard quick-access | Dashboard.razor | Redirect a login |
| 3 | **Dead link /bloqueos** en Dashboard quick-access | Dashboard.razor | Redirect a login |

### MEDIOS (4)
| # | Problema | Ubicación | Impacto |
|---|----------|-----------|---------|
| 4 | **Doble carga API en Dashboard** — 7 endpoints se llaman 2 veces | Dashboard.razor | Performance innecesaria |
| 5 | **Ruta duplicada /admin/roles** — aparece en CATÁLOGOS y IAM | NavMenu.razor | Confusión de navegación |
| 6 | **KPI "Activas" en Apps muestra 0** — tabla muestra 1 Activo | Apps/Index.razor | Datos inconsistentes |
| 7 | **3 headers muestran ruta raw** ("Admin/roles", "Admin/permisos", "Admin/matriz Permisos") | MainLayout.razor / Page headers | UX deficiente |

### BAJOS (2)
| # | Problema | Ubicación | Impacto |
|---|----------|-----------|---------|
| 8 | **7 errores de consola** de extensión 1Password Chrome | Chrome extension | No afecta app |
| 9 | **429 en tenant-info** durante login rápido | Rate limiting | Funcional, no es bug |

---

## INVENTARIO DE API CONSUMIDA

### Dashboard (7 endpoints, duplicados = 14 calls)
- `GET /api/Usuarios/count`
- `GET /api/Sesiones/contar-tenant`
- `GET /api/Apps/count`
- `GET /api/Tenants/count`
- `GET /api/AuditoriaPwd/tenant/{id}`
- `GET /api/Usuarios/con-intentos-excedidos`
- `GET /api/Usuarios/con-password-expirada`

### Auth
- `POST /api/auth/login`
- `GET /api/auth/current-tenant`
- `GET /api/auth/tenant-info` (rate limited 429)

### CRUD Pages
- Tenants: `GET /api/Tenants` (paged), `POST /api/Tenants`, `PUT /api/Tenants/{id}`, `DELETE /api/Tenants/{id}`
- Apps: `GET /api/Apps`, `POST /api/Apps`, `PUT /api/Apps/{id}`, `DELETE /api/Apps/{id}`
- Usuarios: `GET /api/Usuarios`, `POST /api/Usuarios`, `PUT /api/Usuarios/{id}`
- Roles: `GET /api/Roles`, `POST /api/Roles`, `PUT /api/Roles/{id}`
- Permisos: `GET /api/permisos`, `GET /api/permisos/activos`
- Accesos: `GET /api/Accesos`, `POST /api/Accesos`
- Grupos: `GET /api/Grupos`, `POST /api/Grupos`
- MatrizPermisos: `GET /api/MatrizPermisos?idRol={id}`
- PoliticasPwd: `GET /api/PoliticasPwd`, `POST /api/PoliticasPwd`
- ConfigApp: `GET /api/ConfigApp`, `PUT /api/ConfigApp`
- AuditoriaPwd: `GET /api/AuditoriaPwd`
- HistorialPwd: `GET /api/HistorialPwd`
- IntentosAcceso: `GET /api/IntentosAcceso`
- Notificaciones: `GET /api/Notificaciones`
- Mantenimiento: `POST /api/Maintenance/purge`
- Email Providers: `GET /api/EmailProviders`
- Email Accounts: `GET /api/EmailAccounts`
- TenantEmailAccounts: `GET /api/TenantEmailAccounts`
- AppEmailAccounts: `GET /api/AppEmailAccounts`
- EmailTemplates: `GET /api/EmailTemplates`

---

## ARQUITECTURA CONFIRMADA

### Solution Structure
```
PassPlat/
├── PassPlat.Dominio/          (60+ files: 43 entities, 8 enums)
├── PassPlat.Datos/            (100+ files: 43 repos, 39 configs, 12 SP results)
├── PassPlat.Aplicacion/       (90 files: 56 services, 22 validators, 1 mapping)
├── PassPlat.Aplicacion.Dtos/  (shared DTOs)
├── PassPlat.WebAPI/           (55 files: 54 controllers, auth, middleware)
├── PassPlat.Web/              (83 razor files: 22 pages, shared components)
└── PassPlat.Consola/          (console app)
```

### Statistics
| Layer | Files | Key Components |
|-------|-------|----------------|
| Web | 83 .razor | 22 pages, 12 shared components, 2 layouts |
| WebAPI | 55 .cs | 54 controllers, 2 auth, 1 middleware, 1 service |
| Aplicacion | 90 .cs | 35 BBDD services, 13 SP services, 8 email, 22 validators |
| Datos | 100+ .cs | 43 repositories, 39 EF configs, 12 SP results |
| Dominio | 60+ .cs | 43 entities, 8 enums, 1 GlobalUsings |

### Key Components
- **IamKpiCard**: Parameter is `Label` (not `Title`) — verified working
- **IamInspector**: `Visible`/`OnClose` params with `ChildContent` body
- **IamPermissionBadge**: Permission status display
- **ConfirmDialog**: Reusable confirmation modal
- **PasswordStrength**: Password strength indicator
- **RedirectToLogin**: Auth redirect component

### Build Status
- **Dominio**: ✅ 0 errors
- **Datos**: ✅ 0 errors
- **Aplicacion**: ✅ 0 errors
- **Web/WebAPI**: MSB3027 file-lock errors only (VS process) — zero CS compilation errors

---

## SIGUIENTES PASOS

### FASE 3 — MudBlazor Component Audit
- [ ] Check each page for broken MudBlazor components
- [ ] Verify all events fire correctly
- [ ] Test dialog open/close/fill/save flow
- [ ] Validate pagination, search, filter functionality

### FASE 4 — Visual Audit
- [ ] Responsive screenshots at 1920x1080, 1366x768, 768px (tablet), 375px (mobile)
- [ ] Check for overflow, truncation, alignment issues

### FASE 5 — Security Audit
- [ ] Test route access without auth
- [ ] Verify permission-based menu visibility
- [ ] Check for broken authorization

### FASE 6 — Performance Audit
- [ ] Fix double API calls on Dashboard
- [ ] Measure page load times
- [ ] Identify slow requests

### FASE 7 — Code Analysis (SharpLens MCP)
- [ ] Dead code detection
- [ ] SOLID violations
- [ ] Dependency cycles

### FASE 8 — Filesystem Analysis
- [ ] Orphan files
- [ ] Unused components
- [ ] DI registration verification

### FASE 9 — Architectural Validation
- [ ] Clean Architecture compliance
- [ ] DDD patterns
- [ ] SOLID ratings

### FASE 10 — Correction Proposals
- [ ] Fix dead links in Dashboard
- [ ] Fix double API calls
- [ ] Fix KPI "Activas" bug
- [ ] Fix raw route headers
- [ ] Remove duplicate /admin/roles

### FASE 11 — Final Report
- [ ] Complete report with all findings
- [ ] Screenshots and evidence
- [ ] Priority recommendations
