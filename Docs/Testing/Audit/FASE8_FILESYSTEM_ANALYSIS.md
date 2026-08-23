# FASE 8 — Análisis de Filesystem

**Fecha**: 2026-06-21
**Proyecto**: PassPlat
**Stack**: Blazor WASM + MudBlazor 9.5.0 / .NET 10.0
**Herramientas**: Filesystem MCP + SharpLens MCP + Glob
**Solución**: 27 proyectos, 650 documentos

---

## Resumen Ejecutivo

| Categoría | Cantidad |
|-----------|----------|
| 🔴 HIGH | 2 |
| 🟡 MEDIUM | 6 |
| 🟢 LOW | 8 |
| **Total issues** | **16** |

**Calificación General**: 🟢 BUENA — Estructura limpia, sin archivos huérfanos críticos. Email subsystem es el principal candidato a limpieza.

---

## 1. Inventario de Archivos

### 1.1 PassPlat.Web (Blazor WASM)

| Categoría | Cantidad | Archivos |
|-----------|----------|----------|
| Pages (.razor) | 72 | Dashboard, Login, ResetPassword, Usuarios (8 componentes), Tenants, Apps, Roles, RolesPermisos (6 diálogos), Permisos, Grupos, MatrizPermisos, HistorialPwd, IntentosAcceso, AuditoriaPwd, Sesiones, Bloqueos, MFA, DispConfiables, Notificaciones, ConfigApp, ConfigTenants, DominiosTenant, PoliticasPwd, Maintenance, Email (8), EmailTemplates |
| Shared Components | 7 | IamInspector, IamKpiCard, IamPermissionBadge, SinPermiso, ConfirmDialog, PasswordStrength, RedirectToLogin |
| Services | 3 | ApiClient, AuthService, CustomAuthenticationStateProvider |
| Models | 2 | Dtos.cs, AppSettings.cs |
| Helpers | 1 | LocalDateTimeConverter |
| wwwroot | 6 | app.css, iam-pages.css, index.html, appsettings.json, favicon.png, icon-192.png |

### 1.2 PassPlat.WebAPI (ASP.NET Core)

| Categoría | Cantidad |
|-----------|----------|
| Controllers | 54 |
| Auth | PermissionPolicyProvider, JwtTenantContext |

### 1.3 PassPlat.Datos (Data Layer)

| Categoría | Cantidad |
|-----------|----------|
| Repositories | 49 |
| Configurations | 35 |
| SPResults | 1 (MatrizPermisosResult) |
| Interfaces | 2 (IAuthRepository, IPasswordRepository) |

### 1.4 PassPlat.Aplicacion (Application Layer)

| Categoría | Cantidad |
|-----------|----------|
| Services BBDD | 35 |
| Services SPro | 12 |
| Services Email | 8 |
| DTOs | ~40 (en Aplicacion.Dtos) |

### 1.5 PassPlat.Dominio (Domain Layer)

| Categoría | Cantidad |
|-----------|----------|
| Entities | ~30 |
| Enums | 10 |

### 1.6 wwwroot Assets

| Archivo | Tamaño | Estado |
|---------|--------|--------|
| `css/app.css` | 2,349 líneas | ✅ Usado activamente |
| `css/iam-pages.css` | ~500 líneas | ✅ Usado activamente |
| `index.html` | Entry point | ✅ |
| `appsettings.json` | Config | ✅ |
| `favicon.png` | Icon | ✅ |
| `icon-192.png` | PWA icon | ✅ |

---

## 2. Archivos Huérfanos / Sin Uso

### 2.1 PassPlat.Web

| Archivo | Tipo | SharpLens | Evaluación |
|---------|------|-----------|------------|
| `Helpers/LocalDateTimeConverter.cs` | Class | ❌ Unused | 🔴 **Huérfano** — converter no referenciado |
| `Services/CustomAuthenticationStateProvider.cs` | Class | — | ⚠️ **Verificar** — puede estar obsoleto si se usa JWT directo |
| `Models/AppSettings.cs` → `AppId` | Property | ❌ Unused | 🟡 Property sin uso |
| `Models/Dtos.cs` → `CrearDispDto` | Class | ❌ Unused | 🟡 DTO sin UI |
| `Models/Dtos.cs` → `CambiarPasswordDto` | Class | ❌ Unused | 🟡 DTO sin UI |
| `Models/Dtos.cs` → `ValidarPasswordDto` | Class | ❌ Unused | 🟡 DTO sin UI |
| `Models/Dtos.cs` → `ValidarMfaRequest` | Class | ❌ Unused | 🟡 DTO sin UI |
| `Models/Dtos.cs` → `PurgeRequest` | Class | ❌ Unused | 🟡 DTO sin UI |

### 2.2 PassPlat.WebAPI — Controllers huérfanos para IAM

Estos controladores existen pero NO son consumidos por el UI de PassPlat (pueden ser para API pública o futuros módulos):

| Controller | Consumido por UI | Estado |
|------------|------------------|--------|
| `EmailAssetsController` | ❌ No | 🟡 Huérfano para IAM |
| `UserAgentsController` | ❌ No | 🟡 Contexto, no IAM |
| `IPsController` | ❌ No | 🟡 Contexto, no IAM |
| `DispController` | ❌ No | 🟡 Contexto, no IAM |
| `EstadosMFAController` | ❌ No | 🟡 Catálogo, no IAM |
| `EstadosUsrController` | ❌ No | 🟡 Catálogo, no IAM |
| `ResultadosAccesoController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposAuditoriaController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposBloqueoController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposCambioPwdController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposDispController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposMFAController` | ❌ No | 🟡 Catálogo, no IAM |
| `TiposModuloController` | ❌ No | 🟡 Catálogo, no IAM |
| `RolesPoliticasPwdController` | ❌ No | 🟡 Catálogo, no IAM |
| `TipoAsignacionPermisoController` | ❌ No | 🟡 Catálogo, no IAM |
| `PasswordSecurityController` | ❌ No | 🟡 Seguridad, no IAM |
| `EmailLogController` | ❌ No | 🟡 Email, no IAM |
| `EmailTemplateHistorialController` | ❌ No | 🟡 Email, no IAM |
| `EmailTemplatePartialsController` | ❌ No | 🟡 Email, no IAM |
| `TokensRestController` | ❌ No | 🟡 Auth, no IAM |

**Total**: 20 controllers sin consumo de UI

### 2.3 PassPlat.Datos — Repositories huérfanos

| Repository | Consumido por UI | Estado |
|------------|------------------|--------|
| `TipoModuloRepository` | ❌ No | 🟡 Catálogo |
| `TipoAuditoriaRepository` | ❌ No | 🟡 Catálogo |
| `TipoBloqueoRepository` | ❌ No | 🟡 Catálogo |
| `TipoCambioPwdRepository` | ❌ No | 🟡 Catálogo |
| `TipoMFARepository` | ❌ No | 🟡 Catálogo |
| `TipoDispRepository` | ❌ No | 🟡 Catálogo |
| `ResultadoAccesoRepository` | ❌ No | 🟡 Catálogo |
| `EstadoMFARepository` | ❌ No | 🟡 Catálogo |
| `EstadoUsrRepository` | ❌ No | 🟡 Catálogo |
| `EmailLogRepository` | ❌ No | 🟡 Email |
| `UserAgentRepository` | ❌ No | 🟡 Contexto |
| `IPRepository` | ❌ No | 🟡 Contexto |
| `DispRepository` | ❌ No | 🟡 Contexto |
| `TipoAsignacionPermisoRepository` | ❌ No | 🟡 Permisos |

**Total**: 14 repositories sin consumo de UI

### 2.4 PassPlat.Aplicacion — Email Subsystem completo

| Archivo | Consumido | Estado |
|---------|-----------|--------|
| `Email/EmailQueue.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/EmailBackgroundService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/EmailTemplateService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/EmailTemplatePartialService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/EmailTemplateStoreService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/PassPlatEmailService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/IPassPlatEmailService.cs` | ❌ No | 🔴 **Sin uso** |
| `Email/IEmailTemplateStoreService.cs` | ❌ No | 🔴 **Sin uso** |

**Total**: 8 archivos Email subsystem sin uso (están registrados en DI pero no consumidos por controladores)

---

## 3. Verificación de DI Registration

### 3.1 Repositories Registrados vs Consumidos

**Registrados en DatosDependencyInjection.cs** (estimado por patrón genérico):
- ~49 repositorios se registran automáticamente vía el patrón `TConcrete`/`TInterface`

**Consumidos por controladores** (54 controllers):
- Cada controller inyecta 1-3 servicios/repos
- Repos de catálogo (14) no tienen controllers que los consuman directamente

### 3.2 Services Registrados vs Consumidos

**Registrados en AplicacionDependencyInjection.cs**:
- `IPassPlatPasswordSecurity` → Singleton
- `IEmailTemplateStoreService` → Singleton
- `IEmailQueue` → Singleton
- `IPassPlatEmailService` → Scoped
- `EmailBackgroundService` → HostedService
- `IMfaCodeStore` → Singleton

**Consumidos**:
- `IPassPlatPasswordSecurity` → Usado por PasswordController
- `IMfaCodeStore` → Usado por AuthController
- `IEmailQueue` → ❌ No consumido por ningún controller
- `IEmailTemplateStoreService` → ❌ No consumido por ningún controller
- `IPassPlatEmailService` → ❌ No consumido por ningún controller
- `EmailBackgroundService` → ❌ No consume nada (BackgroundService huérfano)

---

## 4. Análisis de CSS/Assets

### 4.1 CSS Files

| Archivo | Líneas | Uso | Estado |
|---------|--------|-----|--------|
| `css/app.css` | 2,349 | Layout, componentes, tema | ✅ Activo |
| `css/iam-pages.css` | ~500 | Estilos específicos IAM | ✅ Activo |

### 4.2 Assets Estáticos

| Archivo | Tipo | Estado |
|---------|------|--------|
| `favicon.png` | Icono browser | ✅ |
| `icon-192.png` | PWA icon | ✅ |
| `index.html` | Entry point | ✅ |

**No hay assets huérfanos en wwwroot.**

---

## 5. Estructura de Directorios

```
PassPlat/
├── PassPlat.Web/                    # Blazor WASM
│   ├── Layout/                      # MainLayout, NavMenu, LoginLayout
│   ├── Pages/                       # 72 .razor files
│   │   ├── Dashboard.razor
│   │   ├── Login.razor
│   │   ├── ResetPassword.razor
│   │   ├── NotFound.razor
│   │   ├── Usuarios/               # 1 Index + 8 Components + 2 Dialogs
│   │   ├── Tenants/                 # Index + 3 Dialogs
│   │   ├── Apps/                    # Index + 1 Dialog
│   │   ├── Roles/                   # Index
│   │   ├── RolesPermisos/           # Index + 6 Dialogs
│   │   ├── Permisos/                # Index
│   │   ├── Grupos/                  # Index + 2 Dialogs
│   │   ├── MatrizPermisos/          # Index
│   │   ├── HistorialPwd/            # Index
│   │   ├── IntentosAcceso/          # Index
│   │   ├── AuditoriaPwd/            # Index
│   │   ├── Sesiones/                # Index
│   │   ├── Bloqueos/                # Index
│   │   ├── MFA/                     # Index
│   │   ├── DispConfiables/          # Index
│   │   ├── Notificaciones/          # Index + Dialog
│   │   ├── ConfigApp/               # Index + Dialog
│   │   ├── ConfigTenants/           # Index + Dialog
│   │   ├── DominiosTenant/          # Index + Dialog
│   │   ├── PoliticasPwd/            # Index + Dialog
│   │   ├── Maintenance/             # Index
│   │   ├── Email/                   # 8 files (Accounts, Providers, Dialogs)
│   │   └── EmailTemplates/          # Index + 2 Dialogs
│   ├── Shared/                      # 7 shared components
│   ├── Services/                    # 3 services
│   ├── Models/                      # 2 files
│   ├── Helpers/                     # 1 file
│   └── wwwroot/                     # 6 assets
├── PassPlat.WebAPI/                 # ASP.NET Core
│   ├── Controllers/                 # 54 controllers
│   ├── Auth/                        # PermissionPolicyProvider, JwtTenantContext
│   └── appsettings.json
├── PassPlat.Aplicacion/             # Application Layer
│   ├── Services/
│   │   ├── BBDD/                    # 35 services
│   │   ├── SPro/                    # 12 services (SP-based)
│   │   └── Email/                   # 8 services (UNUSED)
│   └── Validations/                 # FluentValidation
├── PassPlat.Aplicacion.Dtos/        # DTOs
├── PassPlat.Datos/                  # Data Layer
│   ├── Repositories/                # 49 repositories
│   ├── Configurations/              # 35 EF Core configs
│   ├── SPResults/                   # 1 SP result DTO
│   └── Interfaces/                  # 2 interfaces
└── PassPlat.Dominio/                # Domain Layer
    ├── Entities/                    # ~30 entities
    └── Enums/                       # 10 enums
```

---

## 6. Priorización de Correcciones

### P0 — Inmediato

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 1 | Email subsystem completo sin uso (8 archivos + DI) | Dead code, DI innecesaria | Medio |

### P1 — Alta prioridad

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 2 | `LocalDateTimeConverter.cs` huérfano | Dead code | Bajo |
| 3 | `CustomAuthenticationStateProvider.cs` — verificar uso | Posible dead code | Bajo |
| 4 | 5 DTOs sin uso en `Dtos.cs` | Dead code | Bajo |
| 5 | 20 controllers catálogo/contexto sin UI | Dead code | Bajo |
| 6 | 14 repositories catálogo sin UI | Dead code | Bajo |

### P2 — Media prioridad

| # | Issue | Impacto | Esfuerzo |
|---|-------|---------|----------|
| 7 | `AppSettings.AppId` property sin uso | Code smell | Bajo |
| 8 | 3 Email services registrados en DI sin consumo | DI innecesaria | Bajo |

---

## 7. Estadísticas Finales

| Métrica | Valor |
|---------|-------|
| Total archivos .razor | 72 |
| Total shared components | 7 |
| Total controllers | 54 |
| Total repositories | 49 |
| Total services | 60 |
| Total wwwroot assets | 6 |
| Archivos huérfanos (reales) | ~15 (Email subsystem + converter + DTOs) |
| Controllers sin UI | 20 |
| Repositories sin UI | 14 |
| DI registrations huérfanas | 3 (EmailQueue, IEmailTemplateStoreService, IPassPlatEmailService) |
| CSS files | 2 (activos) |
| Assets estáticos | 3 (activos) |
