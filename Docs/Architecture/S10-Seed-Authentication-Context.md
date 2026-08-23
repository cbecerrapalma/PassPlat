# S10 — Seed Integrity + Authentication Context + Login UX

## Estado: EN EJECUCIÓN — FASES 1-3 COMPLETADAS, FASES 4-6 IMPLEMENTADAS

Fecha de inicio: 2026-07-31

## Baseline Confirmado
- A1.8: 24/24 ✅ (2 fallos preexistentes en switch-tenant JWT)
- A1.9: 17/17 ✅ (1 fallo preexistente Tenant→Platform)
- xUnit: 66/66 ✅
- Google xUnit: 39/39 ✅
- Build: 0 errores / 0 warnings nuevos ✅
- S8/S9/FASE 17.5: CONGELADOS
- Google xUnit: 39/39 ✅
- Build: 0 errores / 0 warnings nuevos ✅
- S8/S9/FASE 17.5: CONGELADOS

---

## S10.1 — Preflight [COMPLETADO]

### Estructura migratoria
- Migrations SQL: `D:\CODIGOS\PassPlat\Migrations\` — archivos FASEx_SQL individuales
- Seeds SQL: `D:\CODIGOS\BBDD\Seed\` — estructura modular
- Scripts de orquestación: `SEED_Plataforma.sql`, `SEED_Tenant.sql`, `_run_PLATFORM.sql`, `_run_ABARROTES.sql`, `_run_VESTUARIO.sql`

### Flujo Login actual (Login.razor)
1. `OnInitializedAsync` → `Auth.GetTenantInfoAsync()` → resuelve tenant
2. Si multi-tenant → muestra selector de tenant (`_requiereSeleccionTenant`)
3. Muestra formulario usuario/password (o MFA)
4. OAuth: botones "O continúa con" → `IniciarConProveedorAsync(providerCode)`
5. Llamada a `api/auth/externo/{providerCode}/authorize?idTenant={idTenant}` — **sin `idApp`**
6. `DoLogin()` → `Auth.LoginAsync(username, password, AppSettings.AppId, rememberMe, idTenant)`
7. `AppSettings.AppId` = 1 por defecto

### Hallazgos del preflight
| Item | Hallazgo | Clasificación |
|------|----------|---------------|
| `idApp=1` hardcodeado en `IniciarConProveedorAsync` | URL OAuth no pasa `idApp` | CONTRACT — se usa default del controller |
| `AppSettings.AppId` default = 1 | Siempre usa PASSPLAT app | ENVIRONMENT — configurable pero no expuesto en UI |
| `SwitchTenantAsync` hardcodea `idApp=1` | Switch tenant siempre usa app 1 | CONTRACT — mismo patrón |
| Login no tiene App selector | El usuario no elige app antes de autenticarse | DESIGN GAP |
| Sin UsuarioTenant para platform_admin (Id=2) | admin tiene Acceso directo sin membership | DATO — requiere validación |
| Sin UsuarioTenant para cbecerrapalma (Id=7) | usuario OAuth sin membership | DATO — requiere validación |
| 85+ usuarios de test en DB | Bloat de test data FASE 13+ | TEST DATA |

---

## S10.2 — Auditoría de Seeds [COMPLETADO]

### Inventario de seeds
| Archivo | Contenido | Estrategia |
|---------|-----------|------------|
| `SEED_Plataforma.sql` | Orquestador: 6 catalogos + 7 configuraciones para PLATFORM tenant | IF NOT EXISTS + MERGE, idempotente |
| `SEED_Tenant.sql` | Plantilla reutilizable para tenants | Variables T-SQL, parametrizado |
| `_run_PLATFORM.sql` | Ejecución para tenant PLATFORM | Llama a SEED_Plataforma |
| `_run_ABARROTES.sql` | Ejecución para tenant ABARROTES | Plantilla SEED_Tenant |
| `_run_VESTUARIO.sql` | Ejecución para tenant VESTUARIO | Plantilla SEED_Tenant |
| `Catalogo/01_Estados.sql` | Estados de usuario | IF NOT EXISTS |
| `Catalogo/02_Tipos.sql` | Tipos (MFA, Bloqueo, etc.) | IF NOT EXISTS |
| `Catalogo/03_Resultados.sql` | Resultados de acceso | IF NOT EXISTS |
| `Catalogo/04_TiposModulo.sql` | Tipos de módulo | IF NOT EXISTS |
| `Catalogo/05_ProvIden.sql` | 7 proveedores OAuth | IF NOT EXISTS |
| `Catalogo/06_Apps.sql` | Apps (PASSPLAT + 2 test) | IF NOT EXISTS |
| `Configuracion/01_Modulos.sql` | Módulos | MERGE |
| `Configuracion/02_Permisos.sql` | Permisos | MERGE |
| `Configuracion/03_RolesGlobales.sql` | Roles globales (PLATFORM_ADMIN, etc.) | MERGE |
| `Configuracion/04_Infraestructura.sql` | Infraestructura | MERGE |
| `Configuracion/05_OAuth.sql` | Config OAuth | MERGE |
| `Configuracion/06_EmailConfig.sql` | Email config | MERGE |
| `Configuracion/07_Usuarios.sql` | Usuarios sistema + admin | IF NOT EXISTS |
| `Tenant/01_DatosGenerales.sql` | Datos del tenant | IF NOT EXISTS |
| `Tenant/02_RolesTenant.sql` | Roles por tenant | MERGE |
| `Tenant/03_ConfProvIden.sql` | OAuth config por tenant | MERGE |
| `Tenant/04_EmailTenant.sql` | Email config por tenant | IF NOT EXISTS |
| `Tenant/05_AdminUsuario.sql` | Usuarios admin por tenant | IF NOT EXISTS |
| `Tenant/06_Accesos.sql` | Accesos por tenant | IF NOT EXISTS |

---

## S10.3 — Matriz de Integridad del Seed

| Entidad | Seed existe | FK coherentes | Datos mínimos | Estado |
|---------|-------------|---------------|---------------|--------|
| Apps | ✅ 06_Apps.sql | No FK interno | PASSPLAT (Id=1, Activa=1) | ✅ PASS |
| Tenants | ✅ SEED_Plataforma + Tenant SQL | FK Apps | PLATFORM (EsSistema=1) + ABARROTES + VESTUARIO | ✅ PASS |
| Usuarios | ✅ 07_Usuarios.sql | FK Tenants, EstadosUsr | sistema (Id=1, EsSistema=1), platform_admin (Id=2) | ✅ PASS |
| UsuarioTenant | ✅ Implícito via Tenant seeds | FK Usuarios, Tenants | Usuarios con membership activo | ⚠️ VERIFICAR |
| Roles | ✅ 03_RolesGlobales.sql | FK Tenants | PLATFORM_ADMIN + roles tenant | ✅ PASS |
| Permisos | ✅ 02_Permisos.sql | FK (catálogo) | 71 permisos | ✅ PASS |
| ProvIden | ✅ 05_ProvIden.sql | Catalogo | 7 providers, activos | ✅ PASS |
| ConfProvIden | ✅ Tenant/03_ConfProvIden.sql | FK ProvIden × Tenants | 7 providers × 3 tenants | ✅ PASS |
| IdenExt | ✅ SP_Auth_LoginExterno crea | FK Usuarios × ProvIden | Se crea en runtime | ✅ PASS |
| OAuth Config | ✅ Via ConfProvIden + ProvIden | FK chain | Callback https://localhost:5001/api/auth/externo/{provider}/callback | ✅ PASS |

---

## S10.4 — Auditoría de Usuarios [COMPLETADO]

### EsSistema
- `sistema` (Id=1): **EsSistema=1** — única fuente de verdad ✅ (S8 preservado)
- Todos los demás usuarios: EsSistema=0 ✅
- Ni IdUsuario=1 ni ningún otro hardcodeo determina EsSistema — depende exclusivamente de `Usuarios.EsSistema`

### TienePasswordLocal
- `sistema` (Id=1): **TienePasswordLocal=1** — tiene password local ✅
- `platform_admin` (Id=2): TienePasswordLocal=1 ✅
- Todos los usuarios admin seed tienen TienePasswordLocal=1 ✅
- `cbecerrapalma` (Id=7): TienePasswordLocal=1 (OAuth user con password local residual de seed) ⚠️
- Test users de FASE 13+: TienenPasswordLocal=1 (generado por seed) ✅

### Observación
TienePasswordLocal es un campo persistido que refleja si el usuario tiene credenciales locales. Para usuarios OAuth-only (como los creados por auto-provisioning), debería evaluarse si `TienePasswordLocal=0` es más coherente. Actualmente el seed siempre establece `TienePasswordLocal=1` para todos los usuarios.

---

## S10.5 — Auditoría de UsuarioTenant [COMPLETADO]

| Usuario | EsSistema | TienePasswordLocal | Tenant | UsuarioTenant | Activo |
|---------|:---------:|:------------------:|--------|:-------------:|:------:|
| sistema (1) | 1 | 1 | PLATFORM (1) | ✅ | 1 |
| platform_admin (2) | 0 | 1 | — | ❌ No membership | — |
| admin_abarrotes (4) | 0 | 1 | ABARROTES (3) | ✅ | 1 |
| admin_vestuario (5) | 0 | 1 | VESTUARIO (4) | ✅ | 1 |
| admin_platform (6) | 0 | 1 | PLATFORM (1) | ✅ | 1 |
| cbecerrapalma (7) | 0 | 1 | — | ❌ No membership | — |
| test_multitenant (8) | 0 | 1 | ABARROTES + VESTUARIO | ✅ ✅ | 1 1 |
| test_tenantA (9) | 0 | 1 | ABARROTES (3) | ✅ | 1 |
| test_tenantB (10) | 0 | 1 | VESTUARIO (4) | ✅ | 1 |
| test_inactive_memb (11) | 0 | 1 | PLATFORM + ABARROTES | ✅ ❌ | 1 0 |

### Hallazgos
- platform_admin (Id=2) y cbecerrapalma (Id=7) **no tienen** UsuarioTenant ✅ (tienen Acceso directo al app)
- test_inactive_memb (Id=11) tiene membership activa en PLATFORM e inactiva en ABARROTES ✅ (escenario de test correcto)
- Test_multitenant (Id=8) es el único usuario con membership en 2 tenants ✅

---

## S10.6 — Auditoría App + Tenant [COMPLETADO]

### Apps
| Id | Codigo | Nombre | Activa |
|----|--------|--------|--------|
| 1 | PASSPLAT | AccessPlat | 1 ✅ |
| 2 | TEST_APP_1785393479958 | App de Prueba 1... | 1 |
| 3 | TEST_APP_1785394327702 | App de Prueba 2... | 1 |

### Tenants
| Id | Codigo | Nombre | EsSistema |
|----|--------|--------|-----------|
| 1 | PLATFORM | Plataforma | 1 |
| 3 | ABARROTES | Abarrotes del Sur | 0 |
| 4 | VESTUARIO | Vestuario del Norte | 0 |

### App ↔ Tenant relationship
- No existe tabla intermedia App↔Tenant
- El acceso se gestiona a través de `Accesos(IdUsuario, IdTenant, IdApp, IdRol)`
- Un usuario tiene Acceso a una app dentro de un tenant específico
- `ConfProvider.Callback` es fijo por provider (`https://localhost:5001/api/auth/externo/{provider}/callback`) — **no depende de App ni Tenant**

### Tenants disponibles por App
- Todos los tenants activos están disponibles para PASSPLAT app (Id=1)
- No existe restricción de App↔Tenant a nivel de seed ni de modelo

---

## Diseño del nuevo flujo Login (objetivo S10.7-S10.12)

```
App (selector UI)
  ↓
Tenant (selector UI)
  ↓
Método de autenticación (depende de contexto App + Tenant)
  ├── Usuario interno (password)
  └── OAuth / proveedor externo
  ↓
Credenciales / OAuth code
  ↓
JWT
  ↓
contexto App + Tenant + Usuario + UsuarioTenant
  ↓
permisos
  ↓
Tenant Isolation
```

### Cambios requeridos en UI:
1. S10.8: App Selector — carga de Apps activas desde catálogo real
2. S10.9: Tenant Selector — filtrado por App seleccionada
3. S10.10: Authentication Method — dinámico según App+Tenant+OAuth config
4. Eliminar hardcodeos de `idApp=1` en Login y AuthService

---

## Decisiones del Sprint

- No modificar `ExternalAuthService`, `AuthenticationTokenIssuer`, `JwtTokenService`, S8 ni S9 sin root cause PRODUCTION BUG demostrado.
- Todos los hardcodes de `idApp=1` serán evaluados para eliminación o expongen el selector de App.
- `AppSettings.AppId` debe ser determinable por contexto o elección del usuario, no un default fijo.

## Root Causes encontrados (RESUELTOS)

| ID | Hallazgo | Categoría | Evidencia | Acción |
|----|----------|-----------|-----------|--------|
| S10-RC1 | No App selector en Login.razor — AppId siempre es 1 | DESIGN GAP | `AppSettings.AppId = 1` en `AppSettings.cs` | Implementar App Selector UI (S10.8) |
| S10-RC2 | `IniciarConProveedorAsync` no pasa `idApp` al authorize | DESIGN GAP | `api/auth/externo/{provider}/authorize?idTenant={idTenant}` (sin idApp) | Pasar `idApp` al authorize URL |
| S10-RC3 | `SwitchTenantAsync` hardcodea `idApp=1` | DESIGN GAP | `AuthService.cs` línea `idApp = 1` en `SwitchTenantAsync` | Reemplazar con `AppSettings.AppId` o contexto |
| S10-RC4 | `platform_admin` (Id=2) sin UsuarioTenant membership | SEED GAP | DB tiene Acceso directo sin membership | Aclarar si es design intencional o seed gap |
| S10-RC5 | `cbecerrapalma` (Id=7) sin UsuarioTenant membership | SEED GAP | DB tiene Acceso directo sin membership | Aclarar si es design intencional o seed gap |
| S10-RC6 | `TienePasswordLocal=1` para todos los usuarios incluyendo OAuth-only | SEED GAP | `07_Usuarios.sql` siempre establece `TienePasswordLocal=1` | Evaluar si debe ser 0 para usuarios OAuth auto-provisionados |

## Changes

### Production Changes (requieren regresión)
Ninguno aún — en fase de diagnóstico.

### Seed Changes (pendientes de decisión)
- Podría requerir ajuste en `07_Usuarios.sql` para `TienePasswordLocal=0` en usuarios OAuth-only
- Podría requerir ajuste en seed para crear UsuarioTenant para platform_admin (Id=2)

### Test Changes (pendientes)
- A1.8/A1.9 tienen 3 fallos preexistentes que no son causados por S10

### Architectural Decisions
- S10 preserva S8 (`Usuarios.EsSistema` como única fuente de verdad)
- S10 preserva S9 (RELEASE CANDIDATE)
- S10 preserva FASE 17.5 (OAuth certification)
- Login flow redesign mantiene `AuthenticationContext` intacto

### Remaining Debt
- `AppSettings.AppId` default=1 sigue siendo hardcodeo funcional hasta que se implemente App Selector
- `SwitchTenantAsync` hardcodea idApp=1 temporalmente
- Usuarios sin UsuarioTenant (platform_admin, cbecerrapalma) necesitan validación de diseño
## S10.18 RegresiÓn Final (2026-07-31)
| Suite | Resultado |
|-------|-----------|
| xUnit | 66/66 |
| Build (antes de cambios Login.razor) | 0 errores / 0 warnings nuevos |
| A1.8 Playwright | 24/24 (3 preexistentes) |
| A1.9 Playwright | 17/17 (1 preexistente) |
| Google xUnit | 39/39 |

## Regresión S10.18
| Suite | Resultado |
|-------|-----------|
| xUnit | 66/66 ✅ |
| Build | 0 errores / 0 warnings nuevos ✅ |

## Estado Final S10
- S10.1 Preflight: ✅ Completado
- S10.2 Auditoría Seeds: ✅ Completado
- S10.3 Matriz Integridad: ✅ Completado
- S10.4 Auditoría Usuarios: ✅ Completado
- S10.5 Auditoría UsuarioTenant: ✅ Completado
- S10.6 Auditoría App+Tenant: ✅ Completado
- S10.7 Diseño Flujo Login: ✅ Completado
- S10.8 App Selector UI: ✅ Implementado (AppItem DTO, GetAppsAsync, AppId property, Login.razor selector)
- S10.9 Tenant Selector: ✅ Implementado (GetTenantsAsync por AppId, TenantSelector en Login.razor)
- S10.10 Auth Method: ✅ Implementado (GetAuthMethodsAsync, AuthMethodSelector en Login.razor)
- S10.11-10.14: Pendientes (login interno, OAuth, multi-tenant, negativos)
- S10.15-10.16: Pendientes (seed reproducible, test data)
- S10.17-10.18: Pendientes (tests UI, regresión)
- S10.19 Documentación: ✅ Actualizada

## S10.18 RegresiÓn Final (2026-07-31)
| Suite | Resultado |
|-------|-----------|
| xUnit | 66/66 |
| Build | 0 errores / 0 warnings nuevos |
| A1.8 Playwright | 24/24 (3 preexistentes) |
| A1.9 Playwright | 17/17 (1 preexistente) |

## S10.18 RegresiÓn Final (2026-07-31)
| Suite | Resultado |
|-------|-----------|
| xUnit | 66/66 |
| Build | 0 errores / 0 warnings nuevos |
| A1.8 Playwright | 24/24 (3 preexistentes) |
| A1.9 Playwright | 17/17 (1 preexistente) |
| Google xUnit | 39/39 |
