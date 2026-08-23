# Modelo de Tablas — PassPlat

> Generado: 2026-07-22  
> Base de datos: PassPlat (SQL Server)  
> Total de tablas: 55  
> Versión documento: 2.0 (FASE -1)

## Categorías

| Categoría | Color | Tratamiento en Seed | Tratamiento en RESET |
|-----------|-------|---------------------|----------------------|
| **CATALOGO** | 🔵 | `IF NOT EXISTS` — nunca UPDATE | No se elimina nunca |
| **CONFIGURACION** | 🟢 | `MERGE` por código | No se elimina (configuración permanente) |
| **OPERACIONAL** | 🟡 | No aplica (se genera en ejecución) | `DELETE` siempre |
| **AUDITORIA** | 🔴 | No aplica (se genera en ejecución) | `DELETE` según @RetentionDays |
| **CACHE** | ⚪ | No aplica | `TRUNCATE` |
| **TEMPORAL** | ⚪ | No aplica | `TRUNCATE` |

## Clasificación completa

| # | Tabla | Dominio | Categoría | DependeTenant | DependeApp | Identity | RowVersion | EsSistema | ResetRuntime | TieneSP | TieneRepository | TieneController | TieneBlazor | TieneSeed | SeedOrigen | OrdenSeed | OrdenReset | OrdenValidate | PuedeRegenerarse | PuedeLimpiarse |
|---|-------|---------|-----------|--------------|------------|----------|------------|-----------|-------------|---------|----------------|----------------|-------------|-----------|------------|-----------|------------|--------------|-----------------|---------------|
| 1 | Accesos | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | AccesoRepository (SPro) | AccesosController | Sí | Sí | Tenant/06_Accesos.sql | 21 | 3 | 7 | Sí | Sí |
| 2 | AppEmailAccounts | Correos | CONFIGURACION | Sí | Sí | Sí (int) | No | No | Sí | No | No | No | No | Sí | Tenant/04_EmailTenant.sql | 19 | 6 | — | Sí | Sí |
| 3 | Apps | Aplicaciones | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | AppsController | Sí | Sí | Catalogo/06_Apps.sql | 6 | No | 1 | No | No |
| 4 | AppsModulos | Aplicaciones | CONFIGURACION | No | Sí | Sí (int) | No | Sí | Sí | No | No | No | No | Sí | Configuracion/01_Modulos.sql | 9 | 4 | — | Sí | Sí |
| 5 | AudIdenExt | Federación | AUDITORIA | No | No | Sí (bigint) | No | No | Sí | No | No | No | No | No | — | 30 | 1 | — | Sí | Sí |
| 6 | AuditoriaPwd | Auditoría | AUDITORIA | No | No | Sí (bigint) | No | No | Sí | No | AuditoriaPwdRepository | No | No | No | — | 29 | 1 | — | Sí | Sí |
| 7 | Bloqueos | IAM | OPERACIONAL | Sí | No | Sí (int) | No | No | Sí | No | BloqueoRepository | BloqueosController | Sí | No | Tenant/06_Accesos.sql | 25 | 4 | 7 | Sí | Sí |
| 8 | ConfigApp | Plataforma | CONFIGURACION | No | No | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/04_Infraestructura.sql | 14 | No | 4 | No | No |
| 9 | ConfigTenants | Plataforma | CONFIGURACION | Sí | No | Sí (int) | No | No | No | No | No | No | No | Sí | Tenant/01_DatosGenerales.sql | 15 | No | 4 | No | No |
| 10 | ConfProvIden | Federación | CONFIGURACION | Sí | No | Sí (int) | Sí | No | No | No | ConfProvIdenRepository (SPro) | ConfProvIdenController | Sí | Sí | Configuracion/05_OAuth.sql | 18 | No | 5 | No | No |
| 11 | Disp | Contexto | OPERACIONAL | Sí | No | Sí (int) | No | No | Sí | No | DispRepository | DispController | Sí | No | — | 27 | 4 | 7 | Sí | Sí |
| 12 | DispConfiables | Contexto | OPERACIONAL | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | No | — | 26 | 4 | — | Sí | Sí |
| 13 | DominiosTenant | Plataforma | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | Sí | Tenant/01_DatosGenerales.sql | 16 | 6 | — | Sí | Sí |
| 14 | EmailAccounts | Correos | CONFIGURACION | No | No | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/06_EmailConfig.sql | 12 | No | 3 | No | No |
| 15 | EmailLog | Correos | AUDITORIA | No | No | Sí (bigint) | No | No | Sí | No | No | No | No | No | — | 32 | 1 | — | Sí | Sí |
| 16 | EmailProviders | Correos | CATALOGO | No | No | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/06_EmailConfig.sql | 5 | No | 1 | No | No |
| 17 | EmailTemplateHistorial | Correos | AUDITORIA | No | No | Sí (int) | Sí | No | Sí | No | No | No | No | No | — | 33 | 1 | — | Sí | Sí |
| 18 | EmailTemplatePartials | Correos | CONFIGURACION | No | No | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/06_EmailConfig.sql | 10 | No | 3 | No | No |
| 19 | EmailTemplates | Correos | CONFIGURACION | No | No | Sí (int) | Sí | Sí | No | No | EmailTemplateStoreService | No | No | Sí | Configuracion/06_EmailConfig.sql | 11 | No | 3 | No | No |
| 20 | EstadosMFA | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/01_Estados.sql | 1 | No | 1 | No | No |
| 21 | EstadosUsr | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/01_Estados.sql | 1 | No | 1 | No | No |
| 22 | EstIdenExt | Federación | CATALOGO | No | — | No | No | Sí | No | No | No | No | No | Sí | Catalogo/01_Estados.sql | 1 | No | 1 | No | No |
| 23 | Grupos | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | GruposController | Sí | Sí | Tenant/06_Accesos.sql | 22 | 5 | 7 | Sí | Sí |
| 24 | GruposUsuarios | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | Sí | Tenant/06_Accesos.sql | 23 | 3 | — | Sí | Sí |
| 25 | HistorialIdenExt | Federación | AUDITORIA | No | No | Sí (bigint) | No | No | Sí | No | No | No | No | No | — | 31 | 1 | — | Sí | Sí |
| 26 | HistorialPwd | IAM | AUDITORIA | Sí | No | Sí (bigint) | No | No | Parcial | No | HistorialPwdRepository | No | No | Parcial | Tenant/05_AdminUsuario.sql | 20 | 1 | 7 | Sí | No (admin) |
| 27 | IdenExt | Federación | OPERACIONAL | Sí | No | Sí (bigint) | No | No | Sí | No | IdenExtRepository (SPro) | IdenExtController | Sí | No | — | 28 | 5 | 7 | Sí | Sí |
| 28 | IdenExtTokens | Federación | OPERACIONAL | No | No | Sí (bigint) | Sí | No | Sí | No | IdenExtTokensRepository | No | No | No | — | 34 | 1 | — | Sí | Sí |
| 29 | IntentosAcceso | IAM | AUDITORIA | No | No | Sí (bigint) | No | No | Sí | No | IntentoAccesoRepository | No | No | No | — | 30 | 1 | 7 | Sí | Sí |
| 30 | IPs | Contexto | OPERACIONAL | No | No | Sí (int) | No | No | Sí | No | IPService | IPsController | Sí | No | — | 27 | 4 | 7 | Sí | Sí |
| 31 | MFA | IAM | OPERACIONAL | Sí | No | Sí (int) | No | No | Sí | SP_MFA_Validar | MFARepository | MfaController | Sí | No | — | 26 | 4 | 7 | Sí | Sí |
| 32 | Modulos | IAM | CONFIGURACION | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/01_Modulos.sql | 7 | No | 2 | No | No |
| 33 | Notificaciones | IAM | OPERACIONAL | No | No | Sí (bigint) | No | No | Sí | No | No | No | No | No | — | 31 | 4 | — | Sí | Sí |
| 34 | Permisos | IAM | CONFIGURACION | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Configuracion/02_Permisos.sql | 8 | No | 2 | No | No |
| 35 | PoliticasPwd | IAM | CONFIGURACION | No | No | Sí (int) | Sí | Sí | No | No | No | No | No | Sí | Configuracion/04_Infraestructura.sql | 13 | No | 4 | No | No |
| 36 | ProvIden | Federación | CATALOGO | No | — | Sí (int) | Sí | Sí | No | No | ProvIdenRepository (SPro) | ProvIdenController | Sí | Sí | Catalogo/05_ProvIden.sql | 5 | No | 1 | No | No |
| 37 | ResultadosAcceso | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/03_Resultados.sql | 2 | No | 1 | No | No |
| 38 | Roles | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | No | No | No | RolesController | Sí | Sí | Configuracion/03_RolesGlobales.sql | 17 | No | 2 | Sí (demo) | No |
| 39 | RolesHerencia | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | No | Tenant/06_Accesos.sql | 24 | 3 | — | Sí | Sí |
| 40 | RolesPermisos | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | Sí | Configuracion/03_RolesGlobales.sql | 20 | 3 | 7 | Sí | No |
| 41 | RolesPoliticasPwd | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | Sí | Tenant/01_DatosGenerales.sql | 22 | 3 | — | Sí | Sí |
| 42 | Sesiones | IAM | OPERACIONAL | No | No | No (Guid PK) | No | No | Sí | SP_Sesiones_Crear, SP_Sesiones_RevocarTodas | SesionRepository | SesionesController | Sí | No | — | 28 | 1 | 7 | Sí | Sí |
| 43 | TenantEmailAccounts | Correos | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | Sí | Tenant/04_EmailTenant.sql | 19 | 6 | — | Sí | Sí |
| 44 | Tenants | Plataforma | CONFIGURACION | — | No | Sí (int) | No | Sí | No | No | TenantRepository (SPro) | TenantsController | Sí | Sí | Configuracion/04_Infraestructura.sql | 15 | No | 4 | No | No |
| 45 | TipAsigPermiso | IAM | CATALOGO | No | — | No | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 3 | No | 1 | No | No |
| 46 | TiposAuditoria | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 4 | No | 1 | No | No |
| 47 | TiposBloqueo | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 4 | No | 1 | No | No |
| 48 | TiposCambioPwd | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 4 | No | 1 | No | No |
| 49 | TiposDisp | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 4 | No | 1 | No | No |
| 50 | TiposMFA | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/02_Tipos.sql | 4 | No | 1 | No | No |
| 51 | TiposModulo | IAM | CATALOGO | No | — | Sí (int) | No | Sí | No | No | No | No | No | Sí | Catalogo/04_TiposModulo.sql | 5 | No | 1 | No | No |
| 52 | TokensRest | IAM | TEMPORAL | No | No | Sí (bigint) | No | No | Sí | SP_TokensRest_Generar, SP_TokensRest_Validar | TokenRestRepository | No | No | No | — | 28 | 1 | 7 | Sí | Sí |
| 53 | UserAgents | Contexto | OPERACIONAL | No | No | Sí (int) | Sí | No | Sí | No | No | No | No | No | — | 27 | 4 | — | Sí | Sí |
| 54 | Usuarios | IAM | CONFIGURACION | Sí | No | Sí (int) | No | Parcial | No | SP_Usuario_Crear, SP_Auth_LoginExterno | UsuarioRepository | UsuariosController | Sí | Parcial | Configuracion/07_Usuarios.sql | 19 | No | 6 | No | No (admin) |
| 55 | UsuariosPermisos | IAM | CONFIGURACION | Sí | No | Sí (int) | No | No | Sí | No | No | No | No | No | Tenant/06_Accesos.sql | 25 | 3 | — | Sí | Sí |

## Resumen por categoría

| Categoría | Cantidad | % del total |
|-----------|----------|-------------|
| **CATALOGO** | 14 | 25.5% |
| **CONFIGURACION** | 18 | 32.7% |
| **OPERACIONAL** | 12 | 21.8% |
| **AUDITORIA** | 6 | 10.9% |
| **CACHE** | 0 | 0% |
| **TEMPORAL** | 1 | 1.8% |
| **Total** | **55** | **100%** |

## Resumen por dominio

| Dominio | Cantidad | Tablas |
|---------|----------|--------|
| IAM | 24 | EstadosUsr, EstadosMFA, TiposMFA, TiposDisp, TiposCambioPwd, TiposBloqueo, TiposAuditoria, TipAsigPermiso, TiposModulo, ResultadosAcceso, Modulos, Permisos, Roles, RolesPermisos, RolesHerencia, RolesPoliticasPwd, Accesos, UsuariosPermisos, Usuarios, HistorialPwd, Sesiones, IntentosAcceso, Bloqueos, MFA, Notificaciones, Grupos, GruposUsuarios |
| Federación | 7 | ProvIden, EstIdenExt, ConfProvIden, IdenExt, IdenExtTokens, HistorialIdenExt, AudIdenExt |
| Correos | 8 | EmailProviders, EmailAccounts, EmailTemplates, EmailTemplatePartials, EmailTemplateHistorial, EmailLog, AppEmailAccounts, TenantEmailAccounts |
| Plataforma | 3 | Tenants, ConfigTenants, ConfigApp |
| Contexto | 3 | Disp, IPs, UserAgents |
| Aplicaciones | 2 | Apps, AppsModulos |
| Auditoría | 2 | AuditoriaPwd, DominiosTenant |

## Notas

- **CATALOGO**: 14 tablas que NUNCA se modifican vía seed (solo correcciones excepcionales).
- **CONFIGURACION**: 18 tablas que definen cómo funciona PassPlat (plataforma) o cómo funciona un tenant.
- **OPERACIONAL**: 12 tablas con datos generados durante la ejecución. Se limpian completamente en RESET.
- **AUDITORIA**: 6 tablas con datos históricos. Se limpian según @RetentionDays en RESET.
- **TEMPORAL**: 1 tabla (TokensRest) con datos efímeros. Se limpia siempre en RESET.
- **EsSistema**: Tablas marcadas como sistema no deben eliminarse nunca.
- **ResetRuntime**: Tablas que pueden limpiarse en RESET (operacional + auditoría + temporal).
