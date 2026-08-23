# Database Object Classification

> Generado: 2026-07-22  
> Propósito: Clasificación funcional de todas las tablas de PassPlat para determinar su ciclo de vida y responsabilidad en los scripts SEED.

## 1. Catálogos (13 tablas)

Nunca cambian por instalación. Idempotentes (IF NOT EXISTS / MERGE).

| Tabla | PK | Identity | Registros | Dependencias |
|-------|-----|----------|-----------|-------------|
| EstadosUsr | Id (int) | Sí | 6 | Referenciado por Usuarios |
| EstadosMFA | Id (int) | Sí | 4 | Referenciado por MFA |
| EstIdenExt | Id (int) | No | 7 | Referenciado por IdenExt |
| TiposMFA | Id (int) | Sí | 6 | - |
| TiposDisp | Id (int) | Sí | 5 | - |
| TiposCambioPwd | Id (int) | Sí | 6 | - |
| TiposBloqueo | Id (int) | Sí | 4 | - |
| TiposAuditoria | Id (int) | Sí | 10 | - |
| TipAsigPermiso | Id (tinyint) | No | 2 | Referenciado por UsuariosPermisos |
| ResultadosAcceso | Id (int) | Sí | 18 | - |
| TiposModulo | Id (int) | Sí | 3 | Referenciado por Modulos |
| ProvIden | Id (int) | Sí | 4 → 7 | Referenciado por ConfProvIden, IdenExt |
| Apps | Id (int) | Sí | 17 | Referenciado por Accesos, AppsModulos, etc. |

**Script**: `Catalogo/01_Estados.sql`..`Catalogo/06_Apps.sql`  
**Estrategia**: `IF NOT EXISTS (...)`

## 2. Configuración (19 tablas)

Cambian por instalación. Deben actualizarse sin eliminar datos existentes.

| Tabla | PK | Identity | RowVersion | Registros | Dependencias |
|-------|-----|----------|------------|-----------|-------------|
| Modulos | Id (int) | Sí | No | 29 | Self-ref + TiposModulo |
| Permisos | Id (int) | Sí | No | 74 | Modulos |
| Roles | Id (int) | Sí | No | 2 | Tenants (nullable) |
| RolesPermisos | Id (int) | Sí | No | 69 | Roles + Permisos |
| RolesHerencia | Id (int) | Sí | No | 0 | Roles (x2) |
| Accesos | Id (int) | Sí | No | 12 | Usuarios + Roles + Apps + Tenants |
| UsuariosPermisos | Id (int) | Sí | No | 0 | Usuarios + Permisos + Apps + Tenants |
| PoliticasPwd | Id (int) | Sí | Sí | 1 | Tenants, Apps (nullable) |
| ConfigApp | Id (int) | Sí | No | 5 | - |
| ConfigTenants | Id (int) | Sí | No | 1 | Tenants |
| DominiosTenant | Id (int) | Sí | No | 1 | Tenants |
| ConfProvIden | Id (int) | Sí | Sí | 1 | Tenants + ProvIden + Roles |
| EmailProviders | Id (int) | Sí | No | 5 | - |
| EmailAccounts | Id (int) | Sí | No | 1 | EmailProviders |
| EmailTemplates | Id (int) | Sí | Sí | 36 | Tenants (nullable) |
| EmailTemplatePartials | Id (int) | Sí | No | 2 | - |
| AppEmailAccounts | Id (int) | Sí | No | 1 | Apps + EmailAccounts |
| TenantEmailAccounts | Id (int) | Sí | No | 1 | Tenants + EmailAccounts |
| RolesPoliticasPwd | Id (int) | Sí | No | 0 | Roles + PoliticasPwd |

**Script**: `Configuracion/01_Modulos.sql`..`Configuracion/07_Usuarios.sql`  
**Estrategia**: `MERGE` / `UPDATE` / `IF NOT EXISTS`

## 3. Operacionales (17 tablas)

Datos transitorios. Se regeneran por uso. Limpiables por RESET_OPERACIONAL.

| Tabla | PK | Identity | Registros | Criticidad |
|-------|-----|----------|-----------|------------|
| Sesiones | Id (uniqueid) | No | 714 | Baja |
| TokensRest | Id (bigint) | Sí | 3 | Baja |
| AuditoriaPwd | Id (bigint) | Sí | 831 | Baja |
| AudIdenExt | Id (bigint) | Sí | 3 | Baja |
| IntentosAcceso | Id (bigint) | Sí | 868 | Baja |
| Bloqueos | Id (int) | Sí | 17 | Media (solo demo) |
| MFA | Id (int) | Sí | 3 | Media (solo demo) |
| Disp | Id (int) | Sí | 2 | Baja |
| DispConfiables | Id (int) | Sí | 0 | Baja |
| IPs | Id (int) | Sí | 1 | Baja |
| UserAgents | Id (int) | Sí | 0 | Baja |
| Notificaciones | Id (bigint) | Sí | 0 | Baja |
| EmailLog | Id (int) | Sí | 81 | Baja |
| HistorialPwd | Id (bigint) | Sí | 379 | **ALTA** (passwords) |
| HistorialIdenExt | Id (int) | Sí | 0 | Baja |
| IdenExt | Id (int) | Sí | 1 | Media |
| IdenExtTokens | Id (int) | Sí | 0 | Media |

**Script**: Ninguno (autogenerados). `RESET_OPERACIONAL.sql` los limpia.  
**Excepción**: HistorialPwd debe preservar hashes del usuario sistema.

## 4. Tenant (5 tablas)

Datos específicos de cada inquilino. Creados por SEED_Tenant.

| Tabla | PK | Identity | Registros | Criticidad |
|-------|-----|----------|-----------|------------|
| Tenants | Id (int) | Sí | 2 | Alta |
| Usuarios | Id (int) | Sí | 386 | Alta (sistema intocable) |
| Grupos | Id (int) | Sí | 17 | Media |
| GruposUsuarios | Id (int) | Sí | 3 | Media |
| EmailTemplateHistorial | Id (int) | Sí | 0 | Baja |

**Script**: `Tenant/01_DatosGenerales.sql`..`Tenant/06_Accesos.sql`  
**Estrategia**: `IF NOT EXISTS`

## Resumen de estrategias por tipo

| Tipo | Creación | Actualización | Eliminación | Idempotencia |
|------|----------|---------------|-------------|--------------|
| Catálogo | `IF NOT EXISTS` | Nunca | Nunca | Sí |
| Configuración Plataforma | `IF NOT EXISTS` | `MERGE` | Solo reset manual | Sí |
| Configuración Tenant | `IF NOT EXISTS` | `UPDATE` | Solo al eliminar tenant | Sí |
| Operacional | Autogenerado | Autogenerado | `DELETE` en RESET | N/A |
| Demo | `DELETE FROM + INSERT` | No aplica | `DELETE` en RESET | No necesario |

## Dependencias de carga (topológico simplificado)

```
Nivel 0: Estados (ninguna FK saliente)
Nivel 1: Tipos, Resultados, Apps, ProvIden
Nivel 2: TiposModulo, EmailProviders
Nivel 3: Modulos (→ TiposModulo), EmailTemplates
Nivel 4: Permisos (→ Modulos), EmailAccounts (→ EmailProviders)
Nivel 5: Roles, ConfigApp, PoliticasPwd, DominiosTenant
Nivel 6: RolesPermisos (→ Roles + Permisos), Accesos (→ 4 tablas)
Nivel 7: Tenants, Usuarios
Nivel 8: Grupos, ConfigTenants, ConfProvIden, RolesHerencia
Nivel 9: GruposUsuarios, RolesPoliticasPwd, UsuariosPermisos
```
