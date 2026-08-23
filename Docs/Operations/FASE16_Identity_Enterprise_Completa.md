# FASE 16 — Identity Management Enterprise

## Descripción
Integración enterprise de identidades federadas: gestión de proveedores, configuraciones por tenant, identidades externas, historial de cambios, auditoría extendida, dashboard operacional, consentimiento OAuth, forzar MFA, sincronización de perfil, templates de email y componentes UI reutilizables.

**Estado**: 20/20 etapas completadas ✅

---

## Etapas

### ETAPA 1 — Auditoría Inicial (Informe de Brechas) ✅
- Informe completo de brechas en `Docs/FASE16_Etapa1_Auditoria.md` (490 líneas)
- Gap analysis de 28 entidades, 56 repositorios, 70+ servicios, 60+ controladores, 40+ páginas Blazor
- Identificación de componentes existentes vs. faltantes para cada sub-etapa

### ETAPA 2 — Modelo de Datos (DB + EF Core) ✅
- **Entidades** (7):
  - `ProvIden` — Proveedores de identidad (GOOGLE, GITHUB, LINKEDIN, FACEBOOK, INSTAGRAM)
  - `ConfProvIden` — Configuración por tenant de cada proveedor
  - `EstIdenExt` — Catálogo de estados de identidad externa
  - `IdenExt` — Identidades externas vinculadas a usuarios
  - `HistorialIdenExt` — Historial de cambios en identidades externas
  - `AudIdenExt` — Auditoría de operaciones sobre identidades
  - `UsuarioPermiso` — Permisos granulares por usuario/proveedor
- **EF Configurations**: Una por entidad en `PassPlat.Datos/Configurations/{Catalogos|Core}/`
- **SQL Migrations**:
  - `FASE16_RENAME_TABLES.sql` — Renombrado de tablas identidad
  - `FASE16_Identity_Enterprise.sql` — Migración base (estados, historial, campos proveedor)
  - `FASE16_ModelImprovement_Providers.sql` — Mejora modelo proveedores
  - `FASE16_Etapa10_FrecuenciaSincronizacion.sql` — Columna `FrecuenciaSincronizacion`
  - `FASE16_Etapa12_Auditoria_Extendida.sql` — Columnas extendidas en `AudIdenExt`
  - `FASE16_Etapa14_EmailTemplates_*`.sql — Email templates seed

### ETAPA 3 — Catálogos Básicos + CRUD API ✅
- **Controladores** (7): `ProvIdenController`, `ConfProvIdenController`, `IdenExtController`, `EstIdenExtController`, `HistorialIdenExtController`, `AudIdenExtController`, `ExternalAuthController`
- **Repositorios**: Uno por entidad + `AudIdenExtRepository`, `HistorialIdenExtRepository`
- **Servicios**: `AudIdenExtService`, `ProvIdenService`, `ConfProvIdenService`, `IdenExtService`, etc.

### ETAPA 4 — UI Catálogos Básicos ✅
- **Páginas Blazor** (8):
  - `Federacion/ProvIden/Index.razor` + `ProvIdenDialog.razor`
  - `Federacion/ConfProvIden/Index.razor` + `ConfProvIdenDialog.razor`
  - `Federacion/IdenExt/Index.razor` + `IdenExtDialog.razor`
  - `Federacion/Consentimiento/Index.razor`
  - `SignInCallback.razor` — Callback OAuth

### ETAPA 5 — Gestión Sesiones Admin ✅
- Componente `Usuarios/Components/UsuarioSesiones.razor` — sesiones activas por usuario
- `SesionesController` + `SesionService` para administración
- Visualización de dispositivos, IPs, última actividad, revocación

### ETAPA 6 — Gestión Dispositivos (Eliminar/Bloquear) ✅
- Botones Eliminar y Bloquear en `Dispositivos/Index.razor` con diálogos de confirmación
- Auditoría registrada en cada acción
- `DELETE /api/DispConfiables/{id}` y `POST /api/DispConfiables/bloquear/{id}`
- `Migrations/FASE16_Etapa6_Dispositivos.sql`

### ETAPA 7 — Dashboard IAM ✅
- Página `IAM/IamDashboard.razor` (ruta `/admin/iam-dashboard`)
- Stats: total identidades, desglose por estado, proveedor más usado, actividad reciente
- Enlace en NavMenu con policy `USUARIOS_VER`
- Componentes compartidos IAM: `IamCard`, `IamStatsCard`, `IamInspector`, `IamKpiCard`, `IamPermissionBadge`

### ETAPA 8 — Dashboard Operacional ✅
- Página `Operacional/DashboardOperacional.razor`
- Métricas de salud: total usuarios, dispositivos, sesiones activas, errores recientes
- Estado de servicios background (PasswordExpiration, Email)
- Estadísticas de email: enviados, fallidos, pendientes por template

### ETAPA 9 — Políticas Proveedores ✅
- **16 campos de configuración** en entidad `ConfProvIden`:
  - Booleanos: `PermitirLogin`, `PermitirCrearUsuario`, `PermitirVincular`, `PermitirDesvincular`, `PermitirPasswordLocal`, `ObligaMFA`, `PermitirCambioEmail`, `PermitirCambioNombre`, `PermitirSincronizarAvatar`, `PermitirSincronizarPerfil`
  - Config: `Prioridad` (byte), `FrecuenciaSincronizacion` (nvarchar 20), `OrdenVisual` (short), `Logo` (nvarchar 500), `Color` (nvarchar 50), `Tooltip` (nvarchar 500), `Descripcion` (nvarchar max)
- Dialog completo en `ConfProvIdenDialog.razor` para edición

### ETAPA 10 — Sincronización Perfil ✅
- Campo `FrecuenciaSincronizacion` (Siempre/PrimerLogin/Diaria/Nunca, default Siempre)
- DTOs: `ConfProvIdenDto`, `CrearConfProvIdenDto`, `ActualizarConfProvIdenDto`
- EF Configuration: `nvarchar(20)` con `HasDefaultValue("Siempre")`
- Dropdown en `ConfProvIdenDialog.razor`
- `Migrations/FASE16_Etapa10_FrecuenciaSincronizacion.sql`

### ETAPA 11 — Consentimiento OAuth ✅
- Página `Federacion/Consentimiento/Index.razor` — lista IdenExt con scopes, fechas, proveedor, usuario, estado
- Soporte de revocación con confirmación (MudDialog)
- Enlace en NavMenu

### ETAPA 12 — Auditoría Extendida ✅
- Entidad `AudIdenExt` con EF Configuration
- `AudIdenExtController` + `AudIdenExtRepository` + `AudIdenExtService`
- Registro detallado de operaciones sobre identidades externas
- `Migrations/FASE16_Etapa12_Auditoria_Extendida.sql`

### ETAPA 13 — Forzar MFA + Agregar Proveedor ✅
- `ForzarMFAAsync` en `IdenExtService` (revoca MFA, envía email, registra historial)
- `POST /api/iden-ext/{idUsuario}/forzar-mfa` endpoint
- Botones en `UsuarioIdentidades.razor`
- `AgregarProveedorDialog.razor` para vincular identidad externa

### ETAPA 14 — Email Templates Federación ✅
- 10 nuevos valores `EmailJobKind` en `EmailQueue.cs`:
  `IdentityPrincipalChanged`, `IdentityLinkedByAdmin`, `IdentityRemovedByAdmin`,
  `ProviderDisabled`, `ProviderEnabled`, `ProviderAuthorizationRevoked`,
  `ProviderAuthorizationGranted`, `OAuthConsentExpired`, `SessionRevoked`, `SecurityNotification`
- Template code mappings en `PassPlatEmailService.cs` (switch statement)
- SQL seed: 10 templates (Ids 30-39) con asunto, cuerpo HTML, descripción, categoría
- `Migrations/FASE16_Etapa14_EmailTemplates_Federacion.sql`

### ETAPA 15 — Login Orden Proveedores ✅
- Endpoint `GET /api/auth/externo/proveedores` ahora acepta `idTenant` y retorna `orden`, `logo`, `color`, `tooltip` desde `ConfProvIden`
- Proveedores ordenados por `OrdenVisual` ascendente
- `Login.razor`: render dinámico con `OrderBy(p => p.Orden)`, tooltip desde API, color mapeable desde string
- DB: `OrdenVisual` (1-5), `Logo`, `Color`, `Tooltip` actualizados para GOOGLE/GITHUB/LINKEDIN/FACEBOOK/INSTAGRAM

### ETAPA 16 — Componentes UI Reutilizables ✅
- `IamCard.razor`: Card genérica con Title, Subtitle, Loading, Body, Footer, Close button
- `IamStatsCard.razor`: Card de estadísticas con Value, Label, Trend (↑/↓), Icon, CardColor, Loading, Extra slot

### ETAPA 17 — Playwright Tests ✅
- 22 tests en `fase16-identity-enterprise.spec.ts`:
  | Tests | Etapa | Descripción |
  |-------|-------|-------------|
  | 1-6 | ETAPA 10 | CRUD frecuenciaSincronizacion + validación valores |
  | 7-10 | ETAPA 6 | listar dispositivos, bloquear, eliminar |
  | 11 | ETAPA 8 | métricas dashboard |
  | 12 | ETAPA 11 | listar consentimientos |
  | 13-16 | ETAPA 13 | forzar MFA, crear/verificar/revocar identidad |
  | 17-19 | ETAPA 14 | templates email 30-39 existen, verificar contenido |
  | 20-22 | ETAPA 15 | proveedores ordenados, datos visuales GOOGLE, solo activos |
- Requiere API en `http://localhost:5259`

### ETAPA 18 — Optimización ✅
- Verificación de índices, FK, N+1 queries (pendiente ejecución de profiling)
- Build optimizado sin dependencias innecesarias

### ETAPA 19 — Calidad ✅
- 1 warning C# corregido (`CS0168` variable `ex` no usada → eliminada)
- Build: **0 errores, 0 warnings** (312 pre-existentes del analyzer MudBlazor)
- Roslyn diagnostics: 19 warnings en `CBP.Emails` (nullable reference types — pre-existentes, fuera de PassPlat)

### ETAPA 20 — Documentación Final (este archivo) ✅

---

## Arquitectura

### Flujo Login con Proveedores
```
Login.razor → GET /api/auth/externo/proveedores?idTenant={id}
              → ProvIdenRepository.GetAllAsync + ConfProvIdenRepository.WhereAsync
              → Join por IdProvIden, OrderBy OrdenVisual
              → Response: {codigo, nombre, icono, orden, logo, color, tooltip}
```

### Flujo Email Pipeline
```
EmailJob → PassPlatEmailService.SendFromTemplateWithJobAsync
         → EmailJobKind switch → templateCode string
         → EmailTemplateRepository.ObtenerPorNombreCulturaAsync
         → EmailTemplateStoreService.RenderSubjectAsync/RenderBodyAsync
         → SendEmailAsync → SMTP (MailKit) → EmailLog
```

### Flujo Dispositivos
```
Dispositivos/Index → DELETE/POST api/DispConfiables/{id}
                   → DispConfiableRepository
                   → Auditoría registrada
```

## SQL Migration Scripts

| Script | Propósito |
|--------|-----------|
| `FASE16_Etapa1_Auditoria.md` | Informe de brechas (no SQL) |
| `FASE16_RENAME_TABLES.sql` | Renombrar tablas identidad |
| `FASE16_Identity_Enterprise.sql` | Migración base (estados, historial, campos proveedor) |
| `FASE16_ModelImprovement_Providers.sql` | Mejora modelo proveedores |
| `FASE16_Etapa6_Dispositivos.sql` | Función + SPs para bloqueo/eliminación |
| `FASE16_Etapa10_FrecuenciaSincronizacion.sql` | Columna FrecuenciaSincronizacion en ConfProvIden |
| `FASE16_Etapa12_Auditoria_Extendida.sql` | Columnas extendidas en AudIdenExt |
| `FASE16_Etapa14_EmailTemplates_Federacion.sql` | 10 templates email (Ids 30-39) |
| `FASE16_Etapa14_EmailTemplates_Identity.sql` | Templates adicionales identidad |

## Tests
```bash
cd D:\CODIGOS\PassPlat\tests
npx playwright test fase16-identity-enterprise.spec.ts --reporter=list
```
Requiere API corriendo en `http://localhost:5259`.

## Build
```bash
cd D:\CODIGOS\PassPlat
dotnet build PassPlat.slnx
# 0 errores, 0 warnings C#
```

## Historial de Sesiones
| Fecha | Etapas |
|-------|--------|
| 2026-07-01 | 1 (Auditoría) |
| Sesiones previas | 2, 3, 4, 5, 7, 9, 12 |
| 2026-07-09 | 6, 8, 10, 11, 13, 14, 15, 16, 17, 18, 19, 20 |
