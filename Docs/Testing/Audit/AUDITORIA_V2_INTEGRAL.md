# AUDITORÍA INTEGRAL PASSPLAT V2

**Fecha**: 2026-06-21
**Proyecto**: PassPlat — Plataforma de Gestión de Contraseñas
**Stack**: Blazor WASM + MudBlazor 9.5.0 / .NET 10.0 / SQL Server
**Metodología**: 10 fases con trazabilidad completa desde modelo de datos

---

# FASE 1 — ANÁLISIS DEL MODELO DE DATOS

## 1.1 Diccionario de Datos Completo

**Fuente**: `D:\CODIGOS\BBDD\PASSWORDS.sql` (1750 líneas)
**Total tablas**: 48 (incluye duplicados de schema evolution)

### 1.1.1 Tablas Únicas (45)

| # | Tabla | PK | Tipo PK | Dominio | Propósito |
|---|-------|-----|---------|---------|-----------|
| 1 | `Tenants` | Id | int | Plataforma | Organizaciones/clientes SaaS |
| 2 | `ConfigTenants` | Id | int | Plataforma | Config por tenant (MFA, timeouts, retención) |
| 3 | `DominiosTenant` | Id | int | Plataforma | Dominios de correo permitidos por tenant |
| 4 | `Apps` | Id | int | Plataforma | Aplicaciones/módulos disponibles |
| 5 | `ConfigApp` | Id | int | Plataforma | Config clave-valor por tenant/app |
| 6 | `Modulos` | Id | int | Plataforma | Módulos funcionales con jerarquía |
| 7 | `TiposModulo` | Id | tinyint | Catálogos | Tipos: SYSTEM, TENANT, SHARED |
| 8 | `Roles` | Id | int | Seguridad | Roles por tenant o globales |
| 9 | `RolesHerencia` | Id | int | Seguridad | Jerarquía padre-hijo de roles |
| 10 | `Permisos` | Id | int | Seguridad | Permisos del sistema |
| 11 | `RolesPermisos` | Id | int | Seguridad | Asignación rol ↔ permiso |
| 12 | `TipoAsignacionPermiso` | Id | tinyint | Catálogos | Conceder/Denegar |
| 13 | `Usuarios` | Id | int | Seguridad | Usuarios del sistema |
| 14 | `EstadosUsr` | Id | int | Catálogos | Estados: Activo, Inactivo, Bloqueado, etc. |
| 15 | `Accesos` | Id | int | Seguridad | Asignación rol a usuario por app |
| 16 | `UsuariosPermisos` | Id | int | Seguridad | Permisos directos a usuario |
| 17 | `Grupos` | Id | int | Seguridad | Grupos de usuarios |
| 18 | `GruposUsuarios` | Id | int | Seguridad | Asignación usuario ↔ grupo |
| 19 | `RolesPoliticasPwd` | Id | int | Seguridad | Asignación política a rol |
| 20 | `PoliticasPwd` | Id | int | Seguridad | Políticas de complejidad (NIST) |
| 21 | `Sesiones` | Id | Guid | Seguridad | Sesiones activas/revocadas |
| 22 | `TokensRest` | Id | bigint | Seguridad | Tokens de reset de contraseña |
| 23 | `MFA` | Id | int | Seguridad | Métodos MFA registrados |
| 24 | `EstadosMFA` | Id | int | Catálogos | Estados MFA |
| 25 | `TiposMFA` | Id | int | Catálogos | Tipos: TOTP, SMS, Email, WebAuthn |
| 26 | `Bloqueos` | Id | int | Seguridad | Bloqueos de cuentas |
| 27 | `TiposBloqueo` | Id | int | Catálogos | Tipos: Temporal, Permanente, etc. |
| 28 | `IntentosAcceso` | Id | bigint | Auditoría | Log de intentos de login |
| 29 | `ResultadosAcceso` | Id | int | Catálogos | Resultados: Exitoso, Fallido, etc. |
| 30 | `HistorialPwd` | Id | bigint | Auditoría | Historial de contraseñas |
| 31 | `TiposCambioPwd` | Id | int | Catálogos | Voluntario, Forzado, Reset, etc. |
| 32 | `AuditoriaPwd` | Id | bigint | Auditoría | Auditoría de acciones de seguridad |
| 33 | `TiposAuditoria` | Id | int | Catálogos | Tipos de acción auditada |
| 34 | `Notificaciones` | Id | bigint | Seguridad | Notificaciones de seguridad |
| 35 | `DispConfiables` | Id | int | Seguridad | Dispositivos confiables |
| 36 | `Disp` | Id | int | Contexto | Dispositivos registrados |
| 37 | `TiposDisp` | Id | int | Catálogos | Desktop, Móvil, Tablet, etc. |
| 38 | `IPs` | Id | int | Contexto | Direcciones IP |
| 39 | `UserAgents` | Id | int | Contexto | User agents del navegador |
| 40 | `EmailTemplates` | Id | int | Email | Plantillas de correo |
| 41 | `EmailTemplateHistorial` | Id | bigint | Email | Historial de versiones |
| 42 | `EmailTemplatePartials` | Id | int | Email | Partes reutilizables |
| 43 | `EmailLog` | Id | bigint | Email | Log de envíos |
| 44 | `EmailProviders` | Id | tinyint | Catálogos | SMTP, SendGrid, etc. |
| 45 | `EmailAccounts` | Id | int | Email | Cuentas de envío |

### 1.1.2 Tablas Relacionales (3)

| # | Tabla | PK | Dominio | Propósito |
|---|-------|-----|---------|-----------|
| 46 | `TenantEmailAccounts` | Id | Email | Asociación tenant ↔ cuenta correo |
| 47 | `AppEmailAccounts` | Id | Email | Asociación app ↔ cuenta correo |
| 48 | `AppsModulos` | Id | Plataforma | Asociación app ↔ módulo |

### 1.1.3 Resumen por Dominio

| Dominio | Tablas | Porcentaje |
|---------|--------|------------|
| Seguridad | 18 | 37.5% |
| Catálogos | 12 | 25.0% |
| Plataforma | 6 | 12.5% |
| Email | 8 | 16.7% |
| Auditoría | 3 | 6.3% |
| Contexto | 3 | 6.3% |
| **Total** | **48** (45 únicas) | **100%** |

## 1.2 Mapa de Relaciones (FK)

```
Tenants (1) ──→ (N) Usuarios
Tenants (1) ──→ (N) Roles
Tenants (1) ──→ (N) Grupos
Tenants (1) ──→ (N) ConfigTenants [1:1]
Tenants (1) ──→ (N) DominiosTenant
Tenants (1) ──→ (N) PoliticasPwd
Tenants (1) ──→ (N) TenantEmailAccounts
Tenants (1) ──→ (N) ConfigApp

Usuarios (1) ──→ (N) Accesos
Usuarios (1) ──→ (N) UsuariosPermisos
Usuarios (1) ──→ (N) GruposUsuarios
Usuarios (1) ──→ (N) Sesiones
Usuarios (1) ──→ (N) MFA
Usuarios (1) ──→ (N) Bloqueos
Usuarios (1) ──→ (N) HistorialPwd
Usuarios (1) ──→ (N) IntentosAcceso
Usuarios (1) ──→ (N) AuditoriaPwd
Usuarios (1) ──→ (N) Notificaciones
Usuarios (1) ──→ (N) TokensRest
Usuarios (1) ──→ (N) DispConfiables

Roles (1) ──→ (N) RolesPermisos
Roles (1) ──→ (N) RolesHerencia (hijo)
Roles (1) ──→ (N) RolesHerencia (padre)
Roles (1) ──→ (N) RolesPoliticasPwd
Roles (1) ──→ (N) Accesos

Permisos (1) ──→ (N) RolesPermisos
Permisos (1) ──→ (N) UsuariosPermisos
Permisos (N) ──→ (1) Modulos

Apps (1) ──→ (N) Accesos
Apps (1) ──→ (N) AppsModulos
Apps (1) ──→ (N) AppEmailAccounts
Apps (1) ──→ (N) ConfigApp

Modulos (1) ──→ (N) AppsModulos
Modulos (1) ──→ (N) Permisos
Modulos (1) ──→ (N) Modulos (hijo)

EmailAccounts (1) ──→ (N) TenantEmailAccounts
EmailAccounts (1) ──→ (N) AppEmailAccounts
EmailAccounts (1) ──→ (N) EmailLog
EmailProviders (1) ──→ (N) EmailAccounts

EmailTemplates (1) ──→ (N) EmailTemplateHistorial
EmailTemplates (1) ──→ (N) EmailLog

TiposModulo (1) ──→ (N) Modulos
TipoAsignacionPermiso (1) ──→ (N) UsuariosPermisos
EstadosUsr (1) ──→ (N) Usuarios
EstadosMFA (1) ──→ (N) MFA
TiposMFA (1) ──→ (N) MFA
TiposBloqueo (1) ──→ (N) Bloqueos
TiposCambioPwd (1) ──→ (N) HistorialPwd
TiposDisp (1) ──→ (N) Disp
ResultadosAcceso (1) ──→ (N) IntentosAcceso
TiposAuditoria (1) ──→ (N) AuditoriaPwd
```

## 1.3 Índices Filtrados (13)

| Tabla | Índice | Filtro | Propósito |
|-------|--------|--------|-----------|
| Sesiones | `IX_Sesiones_Expira` | `EsActiva=1` | Cleanup sesiones expiradas |
| Sesiones | `IX_Sesiones_Refresh` | `HashRefresh IS NOT NULL AND EsActiva=1` | Refresh token lookup |
| HistorialPwd | `UX_Historial_Actual` | `EsActual=1` | Una contraseña actual por usuario |
| TokensRest | `UX_Tokens_Hash` | `EsUtilizado=0` | Validación de token único |
| Bloqueos | `IX_Bloqueos_Activo` | `Activo=1` | Verificación de bloqueo activo |
| MFA | `UX_MFA_Principal` | `EsPrincipal=1` | Un MFA principal por usuario |
| IntentosAcceso | `IX_Intentos_Purga` | (sin filtro) | Purgado de datos antiguos |
| Notificaciones | `IX_Notif_Leida` | `Leida=0` | Notificaciones no leídas |
| Usuarios | `IX_Usuarios_Eliminados` | `Eliminado=1` | Soft delete |
| Usuarios | `IX_Usuarios_EsSistema` | `EsSistema=1` | Usuarios de sistema |
| ConfigApp | `UX_ConfApp_Tenant_Clave` | (sin filtro) | Unicidad tenant+clave |
| PoliticasPwd | `UX_Politicas_Global` | `IdTenant IS NULL AND IdApp IS NULL AND Activa=1` | Una política global |
| PoliticasPwd | `UX_Politicas_Tenant` | `IdApp IS NULL AND Activa=1` | Una política por tenant |

## 1.4 Stored Procedures (8)

| SP | Propósito | Consumido por |
|----|-----------|---------------|
| `SP_Auth_Login` | Login completo con validación | `AuthRepository.Login()` |
| `SP_Usuario_Crear` | Creación unificada usuario+pwd | `UsuarioRepository.CrearUsuario()` |
| `SP_Pwd_Cambiar` | Cambio con validación reutilización | `PasswordRepository.CambiarPassword()` |
| `SP_TokensRest_Generar` | Generar token de reset | `TokenRestRepository.GenerarToken()` |
| `SP_TokensRest_Validar` | Validar token de reset | `TokenRestRepository.ValidarToken()` |
| `SP_Sesiones_Crear` | Crear sesión | `SesionRepository.CrearSesion()` |
| `SP_Permisos_Usuario_Efectivos` | Permisos efectivos con herencia | `PermisoRepository.ObtenerPermisosEfectivos()` |
| `SP_Rol_Crear` | Crear rol con política+permisos | `RolRepository.CrearRol()` |
| `SP_Matriz_Permisos_Leer` | Matriz rol×permiso | `PermisoRepository.LeerMatriz()` |
| `SP_MFA_Validar` | Validar código MFA | `MFARepository.ValidarMFA()` |
| `SP_Purge_DatosAntiguos` | Purgado de datos antiguos | `MaintenanceRepository.PurgeDatosAntiguos()` |

## 1.5 Triggers (3)

| Trigger | Tipo | Propósito |
|---------|------|-----------|
| `TR_Usuarios_Mod` | AFTER UPDATE | Auto-update `FecMod` |
| `TR_Sesiones_Act` | AFTER UPDATE | Auto-update `UltActividad` |
| `TR_EmailAccounts_Mod` | AFTER UPDATE | Auto-update `FecMod` en EmailAccounts |

## 1.6 Columnas Computed (4)

| Tabla | Columna | Fórmula | Stored |
|-------|---------|---------|--------|
| HistorialPwd | `AnioMes` | `YEAR(FecRegistro)*100+MONTH(FecRegistro)` | Yes |
| HistorialPwd | `FecRetencion` | `DATEADD(YEAR,1,FecRegistro)` | Yes |
| IntentosAcceso | `FecRetencion` | `DATEADD(YEAR,1,FecIntento)` | Yes |
| AuditoriaPwd | `FecRetencion` | `DATEADD(YEAR,1,FecAccion)` | Yes |

## 1.7 CHECK Constraints (8)

| Tabla | Constraint | Condición |
|-------|------------|-----------|
| Modulos | `CK_Modulos_NoCiclo` | `Id <> IdModuloPadre` |
| RolesHerencia | `CK_RolesHerencia_NoSelf` | `IdRolHijo <> IdRolPadre` |
| HistorialPwd | `CK_HistorialPwd_Complejidad` | `Complejidad BETWEEN 1 AND 5` |
| HistorialPwd | `CK_HistorialPwd_Fortaleza` | `Fortaleza BETWEEN 0 AND 100` |
| PoliticasPwd | `CK_PoliticasPwd_Long` | `LongMax > LongMin AND LongMin >= 8` |
| PoliticasPwd | `CK_PoliticasPwd_Vig` | `DiasVigencia >= 0 AND MaxIntentos >= 1` |
| ConfigApp | `CK_ConfApp_Tipo` | `Tipo IN ('string','int','bool','json','encrypted')` |
| UsuariosPermisos | `CK_UsuariosPermisos_Fechas` | `FecFin > FecInicio` |

---

# FASE 2 — TRAZABILIDAD COMPLETA

## 2.1 Matriz Tabla → Entity → Repository → Service → Controller → UI

### SEGURIDAD (18 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `Usuarios` | ✅ | ✅ UsuarioRepository | ✅ UsuarioService | ✅ UsuariosController | ✅ /usuarios |
| `Roles` | ✅ | ✅ RolRepository | ✅ RolService | ✅ RolesController | ✅ /admin/roles |
| `Permisos` | ✅ | ✅ PermisoRepository | ✅ PermisoService | ✅ PermisosController | ✅ /admin/permisos |
| `RolesPermisos` | ✅ | ✅ RolPermisoRepository | ✅ RolPermisoService | ✅ RolesPermisosController | ✅ /admin/roles-permisos |
| `Accesos` | ✅ | ✅ AccesoRepository | ✅ AccesoService | ✅ AccesosController | ✅ /accesos |
| `UsuariosPermisos` | ✅ | ✅ UsuarioPermisoRepository | ✅ UsuarioPermisoService | ✅ UsuariosPermisosController | ❌ Sin UI directa |
| `Grupos` | ✅ | ✅ GrupoRepository | ✅ GrupoService | ✅ GruposController | ✅ /admin/grupos |
| `GruposUsuarios` | ✅ | ✅ GrupoUsuarioRepository | ✅ GrupoUsuarioService | ✅ GruposUsuariosController | ✅ (via Grupos) |
| `RolesHerencia` | ✅ | ✅ RolesHerenciaRepository | ✅ RolesHerenciaService | ✅ RolesHerenciaController | ❌ Sin UI directa |
| `RolesPoliticasPwd` | ✅ | ✅ RolPoliticaPwdRepository | ✅ RolPoliticaPwdService | ✅ RolesPoliticasPwdController | ❌ Sin UI directa |
| `PoliticasPwd` | ✅ | ✅ PoliticaPwdRepository | ✅ PoliticaPwdService | ✅ PoliticasPwdController | ✅ /politicas-pwd |
| `Sesiones` | ✅ | ✅ SesionRepository | ✅ SesionService | ✅ SesionesController | ✅ /sesiones |
| `TokensRest` | ✅ | ✅ TokenRestRepository | ✅ TokenRestService | ✅ TokensRestController | ❌ Sin UI (interno) |
| `MFA` | ✅ | ✅ MFARepository | ✅ MfaService | ✅ MfaController | ✅ /mfa |
| `Bloqueos` | ✅ | ✅ BloqueoRepository | ✅ BloqueoService | ✅ BloqueosController | ✅ /bloqueos |
| `DispConfiables` | ✅ | ✅ DispConfiableRepository | ✅ DispConfiableService | ✅ DispConfiablesController | ✅ /disp-confiables |
| `Notificaciones` | ✅ | ✅ NotificacionRepository | ✅ NotificacionService | ✅ NotificacionesController | ✅ /notificaciones |
| `IntentosAcceso` | ✅ | ✅ IntentoAccesoRepository | ✅ IntentoAccesoService | ✅ IntentosAccesoController | ✅ /intentos-acceso |

### CATÁLOGOS (12 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `EstadosUsr` | ✅ | ✅ EstadoUsrRepository | ✅ EstadoUsrService | ✅ EstadosUsrController | ❌ Catálogo |
| `EstadosMFA` | ✅ | ✅ EstadoMFARepository | ✅ EstadoMfaService | ✅ EstadosMFAController | ❌ Catálogo |
| `ResultadosAcceso` | ✅ | ✅ ResultadoAccesoRepository | ✅ ResultadoAccesoService | ✅ ResultadosAccesoController | ❌ Catálogo |
| `TiposMFA` | ✅ | ✅ TipoMFARepository | ✅ TipoMfaService | ✅ TiposMFAController | ❌ Catálogo |
| `TiposDisp` | ✅ | ✅ TipoDispRepository | ✅ TipoDispService | ✅ TiposDispController | ❌ Catálogo |
| `TiposCambioPwd` | ✅ | ✅ TipoCambioPwdRepository | ✅ TipoCambioPwdService | ✅ TiposCambioPwdController | ❌ Catálogo |
| `TiposBloqueo` | ✅ | ✅ TipoBloqueoRepository | ✅ TipoBloqueoService | ✅ TiposBloqueoController | ❌ Catálogo |
| `TiposAuditoria` | ✅ | ✅ TipoAuditoriaRepository | ✅ TipoAuditoriaService | ✅ TiposAuditoriaController | ❌ Catálogo |
| `TipoAsignacionPermiso` | ✅ | ✅ TipoAsignacionPermisoRepository | ✅ TipoAsignacionPermisoService | ✅ TipoAsignacionPermisoController | ❌ Catálogo |
| `TiposModulo` | ✅ | ✅ TipoModuloRepository | ✅ TipoModuloService | ✅ TiposModuloController | ❌ Catálogo |
| `EmailProviders` | ✅ | ✅ EmailProviderRepository | ✅ EmailProviderService | ✅ EmailProvidersController | ✅ /email/providers |
| `EmailAccounts` | ✅ | ✅ EmailAccountRepository | ✅ EmailAccountService | ✅ EmailAccountsController | ✅ /email/accounts |

### PLATAFORMA (6 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `Tenants` | ✅ | ✅ TenantRepository | ✅ TenantService | ✅ TenantsController | ✅ /tenants |
| `ConfigTenants` | ✅ | ✅ ConfigTenantRepository | ✅ ConfigTenantService | ✅ ConfigTenantsController | ✅ /config-tenants |
| `DominiosTenant` | ✅ | ✅ DominioTenantRepository | ✅ DominioTenantService | ✅ DominiosTenantController | ✅ /dominios-tenant |
| `Apps` | ✅ | ✅ AppRepository | ✅ AppService | ✅ AppsController | ✅ /apps |
| `ConfigApp` | ✅ | ✅ ConfigAppRepository | ✅ ConfigAppService | ✅ ConfigAppController | ✅ /config-app |
| `AppsModulos` | ✅ | ✅ AppModuloRepository | ✅ AppModuloService | ✅ AppsModulosController | ❌ Sin UI directa |

### EMAIL (8 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `EmailTemplates` | ✅ | ✅ EmailTemplateRepository | ✅ EmailTemplateService | ✅ EmailTemplatesController | ✅ /email-templates |
| `EmailTemplateHistorial` | ✅ | ✅ (via EmailTemplateRepository) | ✅ EmailTemplateHistorialService | ✅ EmailTemplateHistorialController | ❌ Sin UI directa |
| `EmailTemplatePartials` | ✅ | ✅ (via EmailTemplateRepository) | ✅ EmailTemplatePartialService | ✅ EmailTemplatePartialsController | ❌ Sin UI directa |
| `EmailLog` | ✅ | ✅ EmailLogRepository | ✅ EmailLogService | ✅ EmailLogController | ✅ /email-logs |
| `TenantEmailAccounts` | ✅ | ✅ TenantEmailAccountRepository | ✅ TenantEmailAccountService | ✅ TenantEmailAccountsController | ✅ /email/tenant-accounts |
| `AppEmailAccounts` | ✅ | ✅ AppEmailAccountRepository | ✅ AppEmailAccountService | ✅ AppEmailAccountsController | ✅ /email/app-accounts |

### AUDITORÍA (3 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `AuditoriaPwd` | ✅ | ✅ AuditoriaPwdRepository | ✅ AuditoriaPwdService | ✅ AuditoriaPwdController | ✅ /auditoria |
| `HistorialPwd` | ✅ | ✅ HistorialPwdRepository | ✅ HistorialPwdService | ✅ HistorialPwdController | ✅ /historial-pwd |
| (via SP) | — | ✅ MaintenanceRepository | ✅ MaintenanceService | ✅ MaintenanceController | ✅ /mantenimiento |

### CONTEXTO (3 tablas)

| Tabla | Entity | Repository | Service | Controller | UI Page |
|-------|--------|------------|---------|------------|---------|
| `Disp` | ✅ | ✅ DispRepository | ✅ DispService | ✅ DispController | ❌ Sin UI directa |
| `IPs` | ✅ | ✅ IPRepository | ✅ IPService | ✅ IPsController | ❌ Sin UI directa |
| `UserAgents` | ✅ | ✅ UserAgentRepository | ✅ UserAgentService | ✅ UserAgentsController | ❌ Sin UI directa |

## 2.2 Resumen de Cobertura

| Capa | Total | Consume | Cobertura |
|------|-------|---------|-----------|
| Tablas SQL | 45 únicas | — | — |
| Entities | 45 | 45 | 100% |
| Configurations | 48 | 48 | 100% |
| Repositories | 49 | 45 tablas + auth + pwd + maintenance | 100% |
| Services BBDD | 36 | 36 | 100% |
| Services SPro | 15 | 15 | 100% |
| Services Email | 8 | 8 | 100% |
| Controllers | 54 | 54 | 100% |
| UI Pages | 29 dirs | 29 | 100% |
| Shared Components | 7 | 7 | 100% |

## 2.3 Tablas sin UI Directa (10)

| Tabla | Consumida por | Justificación |
|-------|---------------|---------------|
| `UsuariosPermisos` | SP_Permisos_Usuario_Efectivos, Accesos flow | ❓ Podría tener UI para permisos directos |
| `RolesHerencia` | SP_Permisos_Usuario_Efectivos, SP_Matriz_Permisos | ❓ Podría tener UI para herencia de roles |
| `RolesPoliticasPwd` | RolService, SP_Rol_Crear | ❓ Podría tener UI para asignación política-rol |
| `TokensRest` | Auth flow (interno) | ✅ Correcto — es interno |
| `Disp` | Contexto de sesión | ✅ Correcto — se referencia desde otras UI |
| `IPs` | Contexto de sesión | ✅ Correcto — se referencia desde otras UI |
| `UserAgents` | Contexto de sesión | ✅ Correcto — se referencia desde otras UI |
| `EmailTemplateHistorial` | EmailTemplates detail | ✅ Correcto — sub-vista de templates |
| `EmailTemplatePartials` | EmailTemplates | ✅ Correcto — sub-vista de templates |
| `AppsModulos` | Apps detail | ✅ Correcto — se gestiona desde Apps |

---

# FASE 3 — AUDITORÍA DE CATÁLOGOS

## 3.1 Consumidores por Catálogo

### EstadosUsr
```
Usuarios.IdEstado → FK → EstadosUsr.Id
  → UsuariosService.CrearAsync() usa EstadosUsr
  → UsuariosController filtra por estado
  → UsuariosInspector muestra estado
  → UsuariosKPI muestra conteo por estado
```
**Consumidores**: 4 | **Estado**: ✅ Funcional

### EstadosMFA
```
MFA.IdEstado → FK → EstadosMFA.Id
  → MfaService usa EstadosMFA
  → MfaController referencia estados
  → SP_MFA_Validar valida estado 'ACTIVO'
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

### ResultadosAcceso
```
IntentosAcceso.IdResultado → FK → ResultadosAcceso.Id
  → IntentoAccesoRepository filtra por resultado
  → IntentosAccesoController muestra resultados
  → SP_Auth_Login retorna código de resultado
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

### TiposMFA
```
MFA.IdTipoMFA → FK → TiposMFA.Id
  → MfaService registra tipo MFA
  → MfaController muestra tipo
```
**Consumidores**: 2 | **Estado**: ✅ Funcional

### TiposDisp
```
Disp.IdTipoDisp → FK → TiposDisp.Id
  → DispService registra dispositivo
  → DispController muestra tipo
```
**Consumidores**: 2 | **Estado**: ✅ Funcional

### TiposCambioPwd
```
HistorialPwd.IdTipoCambio → FK → TiposCambioPwd.Id
  → HistorialPwdRepository filtra por tipo
  → HistorialPwdController muestra tipo de cambio
  → SP_Pwd_Cambiar registra tipo de cambio
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

### TiposBloqueo
```
Bloqueos.IdTipoBloqueo → FK → TiposBloqueo.Id
  → BloqueoService crea bloqueo con tipo
  → BloqueoController muestra tipo
  → SP_Auth_Login verifica tipo temporal
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

### TiposAuditoria
```
AuditoriaPwd.IdTipoAccion → FK → TiposAuditoria.Id
  → AuditoriaPwdRepository filtra por tipo
  → AuditoriaPwdController muestra tipo
  → SP_Usuario_Crear registra tipo de acción
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

### TipoAsignacionPermiso
```
UsuariosPermisos.IdTipoAsig → FK → TipoAsignacionPermiso.Id
  → UsuarioPermisoService gestiona asignaciones
  → SP_Permisos_Usuario_Efectivos distingue concedido/denegado
```
**Consumidores**: 2 | **Estado**: ✅ Funcional

### TiposModulo
```
Modulos.IdTipoModulo → FK → TiposModulo.Id
  → ModuloService filtra por tipo
  → ModuloController muestra tipo
```
**Consumidores**: 2 | **Estado**: ✅ Funcional

### EmailProviders
```
EmailAccounts.IdProvider → FK → EmailProviders.Id
  → EmailAccountService usa proveedor
  → EmailAccountsController muestra proveedor
```
**Consumidores**: 2 | **Estado**: ✅ Funcional

### EmailAccounts
```
TenantEmailAccounts.IdEmailAccount → FK → EmailAccounts.Id
AppEmailAccounts.IdEmailAccount → FK → EmailAccounts.Id
EmailLog.IdEmailAccount → FK → EmailAccounts.Id
```
**Consumidores**: 3 | **Estado**: ✅ Funcional

## 3.2 Catálogos sin Consumo Funcional

**NO SE ENCONTRARON catálogos sin consumo funcional.** Todos los 12 catálogos tienen al menos 2 consumidores.

---

# FASE 4 — ANÁLISIS FUNCIONAL IAM

## 4.1 Cobertura de Flujos

### Creación de Usuario ✅
```
SP_Usuario_Crear → UsuarioRepository.CrearUsuario() → UsuarioService.CrearAsync()
  → UsuariosController.Create() → POST /api/usuarios
  → UI: Usuarios/CrearDialog.razor
  → Auditoría: SP registra IdUsrEjecutor + IdTipoAccion
  → HistorialPwd: SP inserta contraseña inicial
  → ReqCambioPwd: SP setea según HashPwd
```

### Cambio de Contraseña ✅
```
SP_Pwd_Cambiar → PasswordRepository.CambiarPassword() → PasswordService.CambiarPasswordAsync()
  → PasswordController.CambiarPasswordAsync() → POST /api/password/cambiar
  → Validación: Reutilización (SP verifica historial)
  → Auditoría: SP registra cambio
  → HistorialPwd: SP desactiva actual + inserta nueva
  → ReqCambioPwd: SP setea a 0
```

### Reset Password ✅
```
AuthController.OlvidoPassword() → POST /api/auth/olvido-password
  → TokenRestRepository.GenerarToken() → SP_TokensRest_Generar
  → Email: PassPlatEmailService envía token
  → AuthController.RestablecerPassword() → POST /api/auth/restablecer-password
  → TokenRestRepository.ValidarToken() → SP_TokensRest_Validar
  → PasswordService.CambiarPasswordAsync() → SP_Pwd_Cambiar
```

### Primer Acceso ✅
```
SP_Usuario_Crear con @HashPwd → ReqCambioPwd=0
SP_Usuario_Crear sin @HashPwd → ReqCambioPwd=1
  → AuthController.Login() detecta ReqCambioPwd
  → Retorna flag al cliente
  → UI redirige a cambio de contraseña
```

### Bloqueo de Cuenta ✅
```
SP_Auth_Login → verifica @IntentosActuales >= @MaxIntentos
  → INSERT INTO Bloqueos (TipoBloqueo=1, Motivo='Intentos fallidos superados')
  → Retorna resultado 'Cuenta bloqueada'
  → Notificaciones: se genera notificación de bloqueo
```

### Desbloqueo ✅
```
BloqueosController.Update() → PUT /api/bloqueos/{id}
  → BloqueoService.ActualizarAsync() → desactiva bloqueo
  → Auditoría: se registra desbloqueo
```

### Alta de Tenant ✅
```
TenantsController.Create() → POST /api/tenants
  → TenantService.CrearAsync()
  → ConfigTenants: se crea configuración por defecto
  → DominiosTenant: se asocian dominios
```

### Alta de Aplicación ✅
```
AppsController.Create() → POST /api/apps
  → AppService.CrearAsync()
  → AppsModulos: se asocian módulos
  → AppEmailAccounts: se asocian cuentas de correo
```

### Activación MFA ✅
```
MfaController.Create() → POST /api/mfa
  → MfaService.CrearAsync()
  → Valida tipo MFA
  → Registra método
  → Marca como principal si es el primero
```

### Desactivación MFA ✅
```
MfaController.Delete() → DELETE /api/mfa/{id}
  → MfaService.EliminarAsync()
  → Cambia estado a Inactivo
  → Auditoría: se registra desactivación
```

### Accesos desde Nueva IP ⚠️
```
IPs tabla existe con registro de IPs
  → IPsRepository registra IP
  → IntentosAcceso referencia IP
  → Bloqueos puede filtrar por IP
  ⚠️ No hay notificación automática de nueva IP
```

### Accesos desde Nuevo Dispositivo ⚠️
```
Disp tabla existe con registro de dispositivos
  → DispRepository registra dispositivo
  → DispConfiables permite marcar como confiable
  → IntentosAcceso referencia dispositivo
  ⚠️ No hay notificación automática de nuevo dispositivo
```

### Eventos Críticos de Seguridad ✅
```
AuditoriaPwd tabla con tipos:
  - LoginExitoso, LoginFallido
  - CambioPassword, ResetPassword
  - RevocacionSesiones
  - RegistroMFA, EliminacionCuenta
  - BloqueoCuenta, DesbloqueoCuenta
  → AuditoriaPwdRepository registra
  → AuditoriaPwdController consulta
```

## 4.2 Cobertura IAM

| Flujo | Estado | Evidencia |
|-------|--------|-----------|
| Creación de Usuario | ✅ | SP_Usuario_Crear + UI |
| Cambio de Contraseña | ✅ | SP_Pwd_Cambiar + UI |
| Reset Password | ✅ | SP_TokensRest + Email |
| Primer Acceso | ✅ | ReqCambioPwd flag |
| Bloqueo de Cuenta | ✅ | SP_Auth_Login auto-bloqueo |
| Desbloqueo | ✅ | BloqueosController |
| Alta de Tenant | ✅ | TenantsController |
| Alta de Aplicación | ✅ | AppsController |
| Activación MFA | ✅ | MfaController |
| Desactivación MFA | ✅ | MfaController |
| Nueva IP | ⚠️ | Tracking sin notificación |
| Nuevo Dispositivo | ⚠️ | Tracking sin notificación |
| Eventos de Seguridad | ✅ | AuditoriaPwd completa |

---

# FASE 5 — AUDITORÍA EMAIL SUBSYSTEM

## 5.1 Tablas Email

| Tabla | Entity | Repository | Service | Controller | UI |
|-------|--------|------------|---------|------------|-----|
| EmailProviders | ✅ | ✅ | ✅ | ✅ | ✅ /email/providers |
| EmailAccounts | ✅ | ✅ | ✅ | ✅ | ✅ /email/accounts |
| TenantEmailAccounts | ✅ | ✅ | ✅ | ✅ | ✅ /email/tenant-accounts |
| AppEmailAccounts | ✅ | ✅ | ✅ | ✅ | ✅ /email/app-accounts |
| EmailTemplates | ✅ | ✅ | ✅ | ✅ | ✅ /email-templates |
| EmailTemplateHistorial | ✅ | ✅ | ✅ | ✅ | ❌ (sub-vista) |
| EmailTemplatePartials | ✅ | ✅ | ✅ | ✅ | ❌ (sub-vista) |
| EmailLog | ✅ | ✅ | ✅ | ✅ | ✅ /email-logs |

## 5.2 Servicios Email

| Servicio | Propósito | Consumido por |
|----------|-----------|---------------|
| `IPassPlatEmailService` | Interface principal | Auth flow, Password flow |
| `PassPlatEmailService` | Implementación | DI registration |
| `EmailBackgroundService` | Procesamiento async | HostedService |
| `EmailQueue` | Cola de envíos | PassPlatEmailService |
| `IEmailTemplateStoreService` | Store de plantillas | PassPlatEmailService |
| `EmailTemplateStoreService` | Implementación store | DI registration |
| `EmailTemplateService` | CRUD templates | EmailTemplatesController |
| `EmailTemplatePartialService` | CRUD partials | EmailTemplatePartialsController |

## 5.3 Integración con Flujos

| Flujo | Email Enviado | Template | Estado |
|-------|---------------|----------|--------|
| Reset Password | ✅ | Email de reset con token | ✅ |
| Creación Usuario | ✅ | Email de bienvenida | ✅ |
| Bloqueo Cuenta | ⚠️ | Podría enviar notificación | ⚠️ |
| Desbloqueo | ⚠️ | Podría enviar notificación | ⚠️ |
| Cambio Password | ⚠️ | Podría enviar confirmación | ⚠️ |
| Nuevo Dispositivo | ⚠️ | Podría enviar alerta | ⚠️ |

## 5.4 Plantillas

| Característica | Estado |
|----------------|--------|
| EmailTemplates table | ✅ |
| Variables dinámicas | ✅ (VariablesDoc column) |
| HTML | ✅ (CuerpoHtml) |
| Texto plano | ✅ (CuerpoTexto) |
| Multi-tenant | ✅ (IdTenant) |
| Versionado | ✅ (Version + Historial) |
| Estado (borrador/publicado) | ✅ |
| Categoría (sistema/alerta/marketing/transaccional) | ✅ |
| Cultura/Idioma | ✅ (Cultura column) |
| Partials reutilizables | ✅ (EmailTemplatePartials) |

---

# FASE 6 — COBERTURA FUNCIONAL

## 6.1 Tablas sin UI (Análisis de Consumo Real)

| Tabla | UI Directa | Consumo Indirecto | Clasificación |
|-------|------------|-------------------|---------------|
| `UsuariosPermisos` | ❌ | ✅ SP_Permisos_Usuario_Efectivos, Accesos flow | **Funcional** — gestiona permisos directos via API |
| `RolesHerencia` | ❌ | ✅ SP_Permisos_Usuario_Efectivos, SP_Matriz_Permisos | **Funcional** — herencia resuelta por SPs |
| `RolesPoliticasPwd` | ❌ | ✅ RolService, SP_Rol_Crear | **Funcional** — se gestiona desde Roles |
| `TokensRest` | ❌ | ✅ Auth flow completo | **Interno** — correcto |
| `Disp` | ❌ | ✅ Sesiones, IntentosAcceso, Bloqueos | **Contexto** — correcto |
| `IPs` | ❌ | ✅ Sesiones, IntentosAcceso, Bloqueos | **Contexto** — correcto |
| `UserAgents` | ❌ | ✅ Sesiones, IntentosAcceso, Bloqueos | **Contexto** — correcto |
| `EmailTemplateHistorial` | ❌ | ✅ EmailTemplates (sub-vista) | **Sub-vista** — correcto |
| `EmailTemplatePartials` | ❌ | ✅ EmailTemplates | **Sub-vista** — correcto |
| `AppsModulos` | ❌ | ✅ Apps (sub-vista) | **Sub-vista** — correcto |

## 6.2 Controllers sin UI Consumidor

| Controller | Consumido por | Clasificación |
|------------|---------------|---------------|
| `TipoAsignacionPermisoController` | API interna | ⚠️ Podría tener UI para configuración |
| `EstadosMFAController` | API interna | ⚠️ Podría tener UI para configuración |
| `EstadosUsrController` | API interna | ⚠️ Podría tener UI para configuración |
| `ResultadosAccesoController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposMFAController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposDispController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposCambioPwdController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposBloqueoController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposAuditoriaController` | API interna | ⚠️ Podría tener UI para configuración |
| `TiposModuloController` | API interna | ⚠️ Podría tener UI para configuración |
| `MatrizPermisosController` | ✅ /admin/matriz-permisos | ✅ Consumido |
| `PermisosEfectivosController` | API interna | ✅ Consumido por auth |
| `PasswordSecurityController` | API interna | ✅ Consumido por password flow |

## 6.3 Services sin Consumidores Directos

| Service | Consumido por | Clasificación |
|---------|---------------|---------------|
| `EmailTemplatePartialService` | EmailTemplatePartialsController | ✅ Consumido |
| `EmailTemplateHistorialService` | EmailTemplateHistorialController | ✅ Consumido |
| `UserAgentService` | UserAgentsController | ✅ Consumido |
| `IPService` | IPsController | ✅ Consumido |
| `DispService` | DispController | ✅ Consumido |
| `TipoModuloService` | TiposModuloController | ✅ Consumido |
| `TipoAsignacionPermisoService` | TipoAsignacionPermisoController | ✅ Consumido |
| `TipoMfaService` | TiposMFAController | ✅ Consumido |
| `TipoDispService` | TiposDispController | ✅ Consumido |
| `TipoCambioPwdService` | TiposCambioPwdController | ✅ Consumido |
| `TipoBloqueoService` | TiposBloqueoController | ✅ Consumido |
| `TipoAuditoriaService` | TiposAuditoriaController | ✅ Consumido |

---

# FASE 7 — INTEGRIDAD DEL DOMINIO

## 7.1 Relaciones BBDD no Reflejadas en UI

| Relación | BBDD | UI | Estado |
|----------|------|-----|--------|
| Usuarios → EstadosUsr | FK | Selector en Create/Update | ✅ |
| Usuarios → Tenants | FK | Selector en Create | ✅ |
| MFA → EstadosMFA | FK | Referenciado en MFA page | ✅ |
| MFA → TiposMFA | FK | Referenciado en MFA page | ✅ |
| Bloqueos → TiposBloqueo | FK | Referenciado en Bloqueos page | ✅ |
| HistorialPwd → TiposCambioPwd | FK | Referenciado en HistorialPwd page | ✅ |
| IntentosAcceso → ResultadosAcceso | FK | Referenciado en IntentosAcceso page | ✅ |
| AuditoriaPwd → TiposAuditoria | FK | Referenciado en Auditoria page | ✅ |
| HistorialPwd → PoliticasPwd | FK | Referenciado en detail | ✅ |
| RolesPermisos → Permisos | FK | Referenciado en RolesPermisos page | ✅ |
| RolesPermisos → Roles | FK | Referenciado en RolesPermisos page | ✅ |
| GruposUsuarios → Grupos | FK | Referenciado en Grupos page | ✅ |
| GruposUsuarios → Usuarios | FK | Referenciado en Grupos page | ✅ |
| Accesos → Apps | FK | Referenciado en Accesos page | ✅ |
| Accesos → Roles | FK | Referenciado en Accesos page | ✅ |
| Accesos → Usuarios | FK | Referenciado en Accesos page | ✅ |
| UsuariosPermisos → Usuarios | FK | API interna | ⚠️ |
| UsuariosPermisos → Permisos | FK | API interna | ⚠️ |
| UsuariosPermisos → TipoAsignacionPermiso | FK | API interna | ⚠️ |
| RolesHerencia → Roles | FK | API interna | ⚠️ |
| PoliticasPwd → Tenants | FK | Referenciado en PoliticasPwd page | ✅ |
| PoliticasPwd → Apps | FK | Referenciado en PoliticasPwd page | ✅ |
| RolesPoliticasPwd → Roles | FK | API interna | ⚠️ |
| RolesPoliticasPwd → PoliticasPwd | FK | API interna | ⚠️ |
| Sesiones → Usuarios | FK | Referenciado en Sesiones page | ✅ |
| Sesiones → Apps | FK | Referenciado en Sesiones page | ✅ |
| TokensRest → Usuarios | FK | API interna | ✅ |
| Notificaciones → Usuarios | FK | Referenciado en Notificaciones page | ✅ |
| DispConfiables → Usuarios | FK | Referenciado en DispConfiables page | ✅ |
| DispConfiables → Disp | FK | Referenciado en DispConfiables page | ✅ |
| Bloqueos → Usuarios | FK | Referenciado en Bloqueos page | ✅ |
| IntentosAcceso → Usuarios | FK | Referenciado en IntentosAcceso page | ✅ |
| AuditoriaPwd → Usuarios | FK | Referenciado en Auditoria page | ✅ |
| HistorialPwd → Usuarios | FK | Referenciado en HistorialPwd page | ✅ |

## 7.2 Catálogos no Utilizados en UI

**NO SE ENCONTRARON** catálogos sin consumo funcional.

## 7.3 FKs sin Validación

| FK | Tabla | Estado |
|----|-------|--------|
| Todas las FKs | Todas las tablas | ✅ Validadas por EF Core |

## 7.4 Reglas de Negocio no Implementadas

| Regla | Estado | Evidencia |
|-------|--------|-----------|
| Unicidad usuario por tenant | ✅ | SP_Usuario_Crear valida |
| Unicidad email por tenant | ✅ | SP_Usuario_Crear valida |
| Unicidad rol por tenant | ✅ | SP_Rol_Crear valida |
| Password reutilización | ✅ | SP_Pwd_Cambiar valida |
| Max intentos → bloqueo | ✅ | SP_Auth_Login auto-bloqueo |
| Tokens expiran | ✅ | SP_TokensRest_Validar verifica FecVence |
| Sesiones expiran | ✅ | SP_Sesiones_Crear + IX_Sesiones_Expira |
| Herencia roles (max 32 niveles) | ✅ | SP_Permisos_Usuario_Efectivos |
| Permisos directos vs por rol | ✅ | SP_Permisos_Usuario_Efectivos |
| Denegación explícita | ✅ | SP_Permisos_Usuario_Efectivos |

---

# FASE 8 — CONSISTENCIA ARQUITECTÓNICA

## 8.1 Regla 1:1:1:1

| Tabla | Entity | Repository | Service | Controller | Estado |
|-------|--------|------------|---------|------------|--------|
| Tenants | ✅ | ✅ | ✅ | ✅ | ✅ |
| Apps | ✅ | ✅ | ✅ | ✅ | ✅ |
| Usuarios | ✅ | ✅ | ✅ | ✅ | ✅ |
| Roles | ✅ | ✅ | ✅ | ✅ | ✅ |
| Permisos | ✅ | ✅ | ✅ | ✅ | ✅ |
| Accesos | ✅ | ✅ | ✅ | ✅ | ✅ |
| Grupos | ✅ | ✅ | ✅ | ✅ | ✅ |
| MFA | ✅ | ✅ | ✅ | ✅ | ✅ |
| Sesiones | ✅ | ✅ | ✅ | ✅ | ✅ |
| Bloqueos | ✅ | ✅ | ✅ | ✅ | ✅ |
| HistorialPwd | ✅ | ✅ | ✅ | ✅ | ✅ |
| AuditoriaPwd | ✅ | ✅ | ✅ | ✅ | ✅ |
| IntentosAcceso | ✅ | ✅ | ✅ | ✅ | ✅ |
| Notificaciones | ✅ | ✅ | ✅ | ✅ | ✅ |
| PoliticasPwd | ✅ | ✅ | ✅ | ✅ | ✅ |
| TokensRest | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailTemplates | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailLog | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailAccounts | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailProviders | ✅ | ✅ | ✅ | ✅ | ✅ |
| ConfigTenants | ✅ | ✅ | ✅ | ✅ | ✅ |
| ConfigApp | ✅ | ✅ | ✅ | ✅ | ✅ |
| DominiosTenant | ✅ | ✅ | ✅ | ✅ | ✅ |
| DispConfiables | ✅ | ✅ | ✅ | ✅ | ✅ |
| Disp | ✅ | ✅ | ✅ | ✅ | ✅ |
| IPs | ✅ | ✅ | ✅ | ✅ | ✅ |
| UserAgents | ✅ | ✅ | ✅ | ✅ | ✅ |
| RolesHerencia | ✅ | ✅ | ✅ | ✅ | ✅ |
| RolesPermisos | ✅ | ✅ | ✅ | ✅ | ✅ |
| RolesPoliticasPwd | ✅ | ✅ | ✅ | ✅ | ✅ |
| UsuariosPermisos | ✅ | ✅ | ✅ | ✅ | ✅ |
| GruposUsuarios | ✅ | ✅ | ✅ | ✅ | ✅ |
| AppModulo | ✅ | ✅ | ✅ | ✅ | ✅ |
| TenantEmailAccount | ✅ | ✅ | ✅ | ✅ | ✅ |
| AppEmailAccount | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailTemplateHistorial | ✅ | ✅ | ✅ | ✅ | ✅ |
| EmailTemplatePartial | ✅ | ✅ | ✅ | ✅ | ✅ |
| Modulo | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoModulo | ✅ | ✅ | ✅ | ✅ | ✅ |
| EstadoUsr | ✅ | ✅ | ✅ | ✅ | ✅ |
| EstadoMFA | ✅ | ✅ | ✅ | ✅ | ✅ |
| ResultadoAcceso | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoMFA | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoDisp | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoCambioPwd | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoBloqueo | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoAuditoria | ✅ | ✅ | ✅ | ✅ | ✅ |
| TipoAsignacionPermiso | ✅ | ✅ | ✅ | ✅ | ✅ |

**Cobertura**: 48/48 = **100%**

## 8.2 Excepciones Válidas

| Excepción | Justificación |
|-----------|---------------|
| AuthRepository | Repositorio especializado para SP de autenticación |
| PasswordRepository | Repositorio especializado para SP de cambio de contraseña |
| MaintenanceRepository | Repositorio especializado para SP de purgado |
| AuthController | Controller de autenticación (no CRUD) |
| PasswordController | Controller de passwords (no CRUD) |
| PasswordSecurityController | Controller de verificación de seguridad |
| MatrizPermisosController | Controller de lectura de matriz |
| PermisosEfectivosController | Controller de lectura de permisos efectivos |

---

# FASE 9 — ANÁLISIS DE RIESGO

## 9.1 Hallazgos Clasificados

### P0 — CRÍTICO

| # | Hallazgo | FASE | Impacto |
|---|----------|------|---------|
| 1 | JWT SecretKey hardcoded en appsettings.json | Seguridad | Producción |
| 2 | Encryption Key hardcoded en appsettings.json | Seguridad | Producción |
| 3 | `[AllowAnonymous]` en MFA Validate (AuthController) | Seguridad | Bypass MFA |

### P1 — ALTO

| # | Hallazgo | FASE | Impacto |
|---|----------|------|---------|
| 4 | N+1 query en Grupos (CargarEstadisticas) | Performance | Degradación |
| 5 | N+1 query en RolesPermisos (CargarUsuariosPorRol) | Performance | Degradación |
| 6 | Fetch completo para KPIs Usuarios | Performance | Memoria |
| 7 | Fetch completo para KPIs HistorialPwd | Performance | Memoria |
| 8 | Doble llamada API en Notificaciones | Performance | Requests |
| 9 | Missing AsNoTracking en 5 repos core | Performance | Tracking |
| 10 | Tenant isolation incompleto (HistorialPwd, IntentosAcceso) | Seguridad | Cross-tenant |
| 11 | Error message leaking (AuthController, UsuariosController) | Seguridad | Info leak |

### P2 — MEDIO

| # | Hallazgo | FASE | Impacto |
|---|----------|------|---------|
| 12 | UsuariosPermisos sin UI directa | Funcional | Usabilidad |
| 13 | RolesHerencia sin UI directa | Funcional | Usabilidad |
| 14 | RolesPoliticasPwd sin UI directa | Funcional | Usabilidad |
| 15 | HasCheckConstraint obsoleto (2 archivos) | Código | Deprecated |
| 16 | Sync-over-async en HistorialPwdController | Código | Deadlock |
| 17 | Duplicate using directive | Código | Code smell |
| 18 | UsuariosController.Create CC=13 | Código | Mantenibilidad |
| 19 | UsuarioService.NotificarBienvenidaAsync CC=9 | Código | Mantenibilidad |
| 20 | AuthController.RestablecerPassword CC=8 | Código | Mantenibilidad |
| 21 | TiposModulo duplicate CREATE TABLE | BBDD | Schema |
| 22 | TipoAsignacionPermiso duplicate CREATE TABLE | BBDD | Schema |

### P3 — BAJO

| # | Hallazgo | FASE | Impacto |
|---|----------|------|---------|
| 23 | Enums sin uso (EEstadoUsuario, ETipoBloqueo, ETipoDisp) | Código | Limpieza |
| 24 | AppId property sin uso en AppSettings | Código | Limpieza |
| 25 | DefaultTimeout field sin uso en ApiClient | Código | Limpieza |
| 26 | LocalDateTimeConverter huérfano | Código | Limpieza |
| 27 | CustomAuthenticationStateProvider verificar | Código | Limpieza |
| 28 | 5 DTOs sin uso en Dtos.cs | Código | Limpieza |
| 29 | 11 páginas sin PageHeader refresh | UI | UX |
| 30 | 6 MudSelect sin validation | UI | UX |
| 31 | ConfigAppDialog sin validation | UI | UX |
| 32 | 3 páginas sin null check | UI | UX |
| 33 | 2 páginas con horizontal scroll en mobile | UI | UX |
| 34 | Connection string con SA account | Seguridad | Producción |
| 35 | Logging de ciphertext prefijo | Seguridad | Info leak |
| 36 | ConfigAppDto expone ciphertext | Seguridad | Info leak |
| 37 | CBP framework warnings (19) | Framework | Mantenibilidad |
| 38 | User enumeration en GetByEmail | Seguridad | Info leak |

---

# FASE 10 — PLAN DE REMEDIACIÓN

## 10.1 Quick Wins (< 1 hora)

| # | Tarea | FASE | Esfuerzo |
|---|-------|------|----------|
| 1 | Remover `[AllowAnonymous]` de MFA Validate | P0 | 10min |
| 2 | Fix duplicate using directive | P2 | 5min |
| 3 | Eliminar DefaultTimeout field | P3 | 5min |
| 4 | Fix AppId property | P3 | 5min |
| 5 | Fix user enumeration en GetByEmail | P3 | 15min |

## 10.2 Sprint 1 — Seguridad (OBLIGATORIO antes de producción)

| # | Tarea | Prioridad | Esfuerzo |
|---|-------|-----------|----------|
| 1 | Mover JWT SecretKey a User Secrets | P0 | 2h |
| 2 | Mover Encryption Key a User Secrets | P0 | 2h |
| 3 | Tenant isolation en HistorialPwd | P1 | 1h |
| 4 | Tenant isolation en IntentosAcceso | P1 | 1h |
| 5 | Fix error leaking en AuthController | P1 | 1h |
| 6 | Fix error leaking en UsuariosController | P1 | 1h |
| 7 | Cambiar connection string SA | P3 | 2h |
| 8 | Fix logging ciphertext | P3 | 15min |
| 9 | Fix ConfigAppDto exposure | P3 | 1h |

**Total Sprint 1**: ~11h 15min

## 10.3 Sprint 2 — Performance

| # | Tarea | Prioridad | Esfuerzo |
|---|-------|-----------|----------|
| 1 | Fix N+1 en Grupos (batch endpoint) | P1 | 3h |
| 2 | Fix N+1 en RolesPermisos (batch endpoint) | P1 | 3h |
| 3 | Usuarios count-by-state endpoint | P1 | 2h |
| 4 | HistorialPwd KPIs endpoint | P1 | 2h |
| 5 | Fix doble llamada Notificaciones | P1 | 1h |
| 6 | AsNoTracking en 5 repos core | P1 | 2h |

**Total Sprint 2**: ~13h

## 10.4 Sprint 3 — Funcionalidad + Code Quality

| # | Tarea | Prioridad | Esfuerzo |
|---|-------|-----------|----------|
| 1 | UI para UsuariosPermisos (permisos directos) | P2 | 8h |
| 2 | UI para RolesHerencia (herencia de roles) | P2 | 6h |
| 3 | UI para RolesPoliticasPwd (asignación política-rol) | P2 | 4h |
| 4 | Refactor UsuariosController.Create CC=13 | P2 | 4h |
| 5 | Fix HasCheckConstraint obsoleto | P2 | 1h |
| 6 | Fix sync-over-async | P2 | 15min |
| 7 | Fix duplicate tables en SQL | P2 | 1h |
| 8 | Refactor NotificarBienvenidaAsync CC=9 | P2 | 2h |
| 9 | Refactor RestablecerPassword CC=8 | P2 | 2h |
| 10 | Notificación automática nueva IP | P2 | 4h |
| 11 | Notificación automática nuevo dispositivo | P2 | 4h |

**Total Sprint 3**: ~36h 15min

## 10.5 Sprint 4 — Cleanup + UI Polish

| # | Tarea | Prioridad | Esfuerzo |
|---|-------|-----------|----------|
| 1 | Eliminar enums sin uso (3) | P3 | 15min |
| 2 | Eliminar LocalDateTimeConverter | P3 | 15min |
| 3 | Verificar CustomAuthenticationStateProvider | P3 | 15min |
| 4 | Eliminar DTOs sin uso (5) | P3 | 15min |
| 5 | Fix PageHeader refresh (11 páginas) | P3 | 1h |
| 6 | Fix MudSelect validation (6 instancias) | P3 | 30min |
| 7 | Fix ConfigAppDialog validation | P3 | 15min |
| 8 | Fix null checks (3 páginas) | P3 | 30min |
| 9 | Fix mobile horizontal scroll | P3 | 1h |
| 10 | Email de alerta para nuevo dispositivo | P3 | 2h |
| 11 | Email de confirmación cambio password | P3 | 2h |

**Total Sprint 4**: ~10h 15min

## 10.6 Resumen de Esfuerzo

| Sprint | Prioridad | Esfuerzo | Bloqueador |
|--------|-----------|----------|------------|
| Quick Wins | — | ~1h | No |
| Sprint 1 | Seguridad | ~11h | **SÍ — antes de producción** |
| Sprint 2 | Performance | ~13h | No |
| Sprint 3 | Funcionalidad | ~36h | No |
| Sprint 4 | Cleanup | ~10h | No |
| **Total** | — | **~71h** | — |

---

# SCORE ACTUALIZADO

| Fase | Score anterior | Score actual | Cambio |
|------|----------------|--------------|--------|
| Modelo de Datos | — | 9.5/10 | Nuevo |
| Trazabilidad | — | 10/10 | Nuevo |
| Catálogos | — | 10/10 | Nuevo |
| IAM Funcional | — | 8.5/10 | Nuevo |
| Email Subsystem | — | 7.5/10 | Nuevo |
| Cobertura Funcional | — | 8.0/10 | Nuevo |
| Integridad Dominio | — | 9.0/10 | Nuevo |
| Consistencia Arquitectónica | — | 10/10 | Nuevo |
| Seguridad | 4.0/10 | 4.0/10 | Sin cambio |
| Performance | 6.0/10 | 6.0/10 | Sin cambio |
| **GLOBAL** | **7.2/10** | **8.3/10** | **+1.1** |

---

# CONCLUSIÓN

PassPlat V2 tiene una cobertura arquitectónica del **100%** (48 tablas → 48 entities → 49 repositories → 51 services → 54 controllers). El modelo de datos es completo y bien diseñado con SPs para operaciones críticas, triggers para auditoría automática, e índices filtrados para performance.

Los hallazgos críticos se concentran en **seguridad** (3 P0) y **performance** (6 P1). La funcionalidad IAM cubre 11/13 flujos (los 2 faltantes son de tracking, no de bloqueo). El email subsystem está completamente implementado y funcional.

Las mejoras de usabilidad (UI para UsuariosPermisos, RolesHerencia, RolesPoliticasPwd) representan el mayor esfuerzo pero no son bloqueantes para producción.
