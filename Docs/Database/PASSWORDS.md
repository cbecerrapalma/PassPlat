# PASSWORDS.sql — Database Reference

**Location**: `D:\CODIGOS\BBDD\PASSWORDS.sql`

## Entity-Relationship Summary

### Catalogos (13 tables)

| Table | PK Type | Key Relationships |
|-------|---------|-------------------|
| `Tenants` | int | Parent of all tenant-scoped entities |
| `ConfigTenants` | int | 1:1 with Tenants (IdTenant UNIQUE) |
| `DominiosTenant` | int | M:1 with Tenants |
| `Apps` | int | Parent of application-scoped entities |
| `EstadosUsr` | int | Lookup for Usuario.IdEstado |
| `Roles` | int | M:1 with Tenants (nullable for global roles) |
| `ResultadosAcceso` | int | Lookup for IntentoAcceso.IdResultado |
| `TiposMFA` | int | Lookup for MFA.IdTipoMFA |
| `EstadosMFA` | int | Lookup for MFA.IdEstado |
| `TiposDisp` | int | Lookup for Disp.IdTipoDisp |
| `TiposCambioPwd` | int | Lookup for HistorialPwd.IdTipoCambio |
| `TiposBloqueo` | int | Lookup for Bloqueo.IdTipoBloqueo |
| `TiposAuditoria` | int | Lookup for AuditoriaPwd.IdTipoAccion |

### Contexto (3 tables)

| Table | PK Type | Key Relationships |
|-------|---------|-------------------|
| `Disp` | int | M:1 with TiposDisp |
| `IPs` | int | Referenced by Sesion, TokenRest, IntentoAcceso, Bloqueo, AuditoriaPwd |
| `UserAgents` | int | Referenced by TokenRest, IntentoAcceso, Bloqueo, AuditoriaPwd, DispConfiable |

### Core (13 tables)

| Table | PK Type | Key Relationships |
|-------|---------|-------------------|
| `Usuarios` | int | M:1 with Tenants, EstadosUsr; parent of most core entities |
| `Accesos` | int | M:1 with Usuario, Tenant, App, Rol (unique per Usr+App+Rol) |
| `PoliticasPwd` | int | M:1 with Tenant, App (nullable); parent of HistorialPwd |
| `RolesPoliticasPwd` | int | M:1 with Tenant, Rol, Politica (unique per Tenant+Rol when active) |
| `HistorialPwd` | bigint | M:1 with Usuario, Politica, Disp, TipoCambio |
| `Sesiones` | uniqueidentifier | M:1 with Usuario, Tenant, App, Disp, IP; self-ref (IdSesionPadre) |
| `TokensRest` | bigint | M:1 with Usuario, Tenant, App, Disp, Agente, IP |
| `IntentosAcceso` | bigint | M:1 with Usuario, Tenant, App, Resultado, Disp, Agente, IP |
| `Bloqueos` | int | M:1 with Usuario, Tenant, TipoBloqueo, Agente, IP, UsrBloqueador |
| `MFA` | int | M:1 with Usuario, Tenant, TipoMFA, EstadoMFA |
| `AuditoriaPwd` | bigint | M:1 with Usuario, Tenant, App, TipoAuditoria, Disp, Agente, IP, HistPwd |
| `DispConfiables` | int | M:1 with Usuario, Tenant, Disp, Agente |
| `Notificaciones` | bigint | M:1 with Usuario, Tenant |

## Stored Procedures

| SP | Parameters | Returns | Transaction |
|----|------------|---------|-------------|
| `SP_Auth_Login` | @NomUsuario, @Email, @IdApp, @HashPwdCalculado, @IdDisp, @IdIP, @IdAgente | Resultado, Mensaje, IdUsuario, IdTenant, etc. | Yes (manual) |
| `SP_Pwd_Cambiar` | @IdUsuario, @IdTenant, @HashPwdNuevo, @PepperVersion, @IdTipoCambio, @IdDisp, @IdIP, @IdAgente | Exito, Mensaje | Yes (BEGIN/COMMIT/ROLLBACK) |
| `SP_TokensRest_Generar` | @IdUsuario, @IdTenant, @IdApp, @HashToken, @FecVence, @IdDisp, @IdIP, @IdAgente | IdToken | No (single insert) |
| `SP_TokensRest_Validar` | @HashToken, @IdApp | Exito, IdUsuario, IdTenant | No (update + select) |
| `SP_Sesiones_Crear` | @IdUsuario, @IdTenant, @IdApp, @IdTokenExt, @HashRefresh, @FecExpira, @IdDisp, @IdIP, @IdSesionPadre | IdSesion | No |
| `SP_Sesiones_RevocarTodas` | @IdUsuario, @IdTenant, @IdSesionExcluir | SesionesRevocadas | No |
| `SP_MFA_Validar` | @IdUsuario, @IdTenant, @IdTipoMFA, @IdMFA | Exito, EsPrincipal | No |
| `SP_Purge_DatosAntiguos` | @DiasRetencion | FilasEliminadas, FechaCorte | No (multiple DELETEs) |

## Computed Columns (PERSISTED)

| Table | Column | Formula | Purpose |
|-------|--------|---------|---------|
| `HistorialPwd` | `AnioMes` | `YEAR(FecRegistro)*100+MONTH(FecRegistro)` | Partitioning / reporting |
| `HistorialPwd` | `FecRetencion` | `DATEADD(YEAR, 1, FecRegistro)` | Data retention policy |
| `IntentoAcceso` | `FecRetencion` | `DATEADD(YEAR, 1, FecIntento)` | Data retention policy |
| `AuditoriaPwd` | `FecRetencion` | `DATEADD(YEAR, 1, FecAccion)` | Data retention policy |

## Triggers

| Trigger | Table | Event | Action |
|---------|-------|-------|--------|
| `TR_Usuarios_Mod` | Usuarios | AFTER UPDATE | Sets `FecMod = SYSUTCDATETIME()` when Nombre, Apellido, IdEstado, Email, or IdTenant changes |
| `TR_Sesiones_Act` | Sesiones | AFTER UPDATE | Sets `UltActividad = SYSUTCDATETIME()` when UltActividad or EsActiva changes |
| `TR_Accesos_ValidarTenant` | Accesos | INSTEAD OF INSERT, UPDATE | Validates IdTenant matches Usuario.IdTenant; rejects if mismatch |
