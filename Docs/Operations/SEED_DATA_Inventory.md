# SEED DATA Inventory

> Generado: 2026-07-22  
> Base de datos: PassPlat (SQL Server)  
> Total de tablas: 55

## Clasificación funcional

| # | Tabla | EsCatálogo | EsConfig | EsOperac | EsTenant | EsSistema | TieneIdentity | TieneRowVersion | Filas | TieneSeed | ScriptResponsable | PuedeResetear | PuedeEliminar | PrioridadCarga |
|---|-------|-----------|----------|----------|----------|-----------|--------------|----------------|-------|-----------|------------------|--------------|--------------|---------------|
| 1 | Accesos | No | Sí | No | Sí | No | Sí | No | 12 | Sí | Tenant/06_Accesos.sql | Sí | Sí | 21 |
| 2 | AppEmailAccounts | No | Sí | No | Sí | No | Sí | No | 1 | Sí | Tenant/04_EmailTenant.sql | Sí | Sí | 19 |
| 3 | Apps | Sí | No | No | No | Sí | Sí | No | 17 | Sí | Catalogo/06_Apps.sql | No | No | 6 |
| 4 | AppsModulos | No | Sí | No | No | Sí | Sí | No | 29 | Sí | Configuracion/01_Modulos.sql | Sí | Sí | 9 |
| 5 | AudIdenExt | No | No | Sí | No | No | Sí | No | 3 | No | Ninguno (auto) | Sí | Sí | 30 |
| 6 | AuditoriaPwd | No | No | Sí | No | No | Sí | No | 831 | No | Ninguno (auto) | Sí | Sí | 29 |
| 7 | Bloqueos | No | No | Sí | Sí | No | Sí | No | 17 | No | Tenant/06_Accesos.sql | Sí | Sí | 25 |
| 8 | ConfigApp | No | Sí | No | No | Sí | Sí | No | 5 | Sí | Configuracion/04_Infraestructura.sql | No | No | 14 |
| 9 | ConfigTenants | No | Sí | No | Sí | No | Sí | No | 1 | Sí | Tenant/01_DatosGenerales.sql | No | No | 15 |
| 10 | ConfProvIden | No | Sí | No | Sí | No | Sí | Sí | 1 | Sí | Configuracion/05_OAuth.sql | No | No | 18 |
| 11 | Disp | No | No | Sí | Sí | No | Sí | No | 2 | No | Ninguno (auto) | Sí | Sí | 27 |
| 12 | DispConfiables | No | No | Sí | Sí | No | Sí | No | 0 | No | Ninguno (auto) | Sí | Sí | 26 |
| 13 | DominiosTenant | No | Sí | No | Sí | No | Sí | No | 1 | Sí | Tenant/01_DatosGenerales.sql | Sí | Sí | 16 |
| 14 | EmailAccounts | No | Sí | No | No | Sí | Sí | No | 1 | Sí | Configuracion/06_EmailConfig.sql | No | No | 12 |
| 15 | EmailLog | No | No | Sí | No | No | Sí | No | 81 | No | Ninguno (auto) | Sí | Sí | 32 |
| 16 | EmailProviders | Sí | No | No | No | Sí | Sí | No | 5 | Sí | Catalogo/06_Apps.sql (o Email) | No | No | 11 |
| 17 | EmailTemplateHistorial | No | No | Sí | No | No | Sí | Sí | 0 | No | Ninguno (auto) | Sí | Sí | 33 |
| 18 | EmailTemplatePartials | No | Sí | No | No | Sí | Sí | No | 2 | Sí | Configuracion/06_EmailConfig.sql | No | No | 10 |
| 19 | EmailTemplates | No | Sí | No | No | Sí | Sí | Sí | 36 | Sí | Configuracion/06_EmailConfig.sql | No | No | 11 |
| 20 | EstadosMFA | Sí | No | No | No | Sí | Sí | No | 4 | Sí | Catalogo/01_Estados.sql | No | No | 1 |
| 21 | EstadosUsr | Sí | No | No | No | Sí | Sí | No | 6 | Sí | Catalogo/01_Estados.sql | No | No | 1 |
| 22 | EstIdenExt | Sí | No | No | No | Sí | No | No | 7 | No (debe) | Catalogo/01_Estados.sql | No | No | 1 |
| 23 | Grupos | No | No | No | Sí | No | Sí | No | 17 | Sí | Tenant/06_Accesos.sql | Sí | Sí | 22 |
| 24 | GruposUsuarios | No | No | No | Sí | No | Sí | No | 3 | Sí | Tenant/06_Accesos.sql | Sí | Sí | 23 |
| 25 | HistorialIdenExt | No | No | Sí | No | No | Sí | No | 0 | No | Ninguno (auto) | Sí | Sí | 31 |
| 26 | HistorialPwd | No | No | Sí | Sí | No | Sí | No | 379 | Parcial | Tenant/05_AdminUsuario.sql | No | No | 20 |
| 27 | IdenExt | No | No | Sí | Sí | No | Sí | No | 1 | No | Ninguno (auto) | Sí | Sí | 28 |
| 28 | IdenExtTokens | No | No | Sí | No | No | Sí | Sí | 0 | No | Ninguno (auto) | Sí | Sí | 34 |
| 29 | IntentosAcceso | No | No | Sí | No | No | Sí | No | 868 | No | Ninguno (auto) | Sí | Sí | 30 |
| 30 | IPs | No | No | Sí | No | No | Sí | No | 1 | No | Ninguno (auto) | Sí | Sí | 27 |
| 31 | MFA | No | No | Sí | Sí | No | Sí | No | 3 | No | Ninguno (auto) | Sí | Sí | 26 |
| 32 | Modulos | No | Sí | No | No | Sí | Sí | No | 29 | Sí | Configuracion/01_Modulos.sql | No | No | 7 |
| 33 | Notificaciones | No | No | Sí | No | No | Sí | No | 0 | No | Ninguno (auto) | Sí | Sí | 31 |
| 34 | Permisos | No | Sí | No | No | Sí | Sí | No | 74 | Sí | Configuracion/02_Permisos.sql | No | No | 8 |
| 35 | PoliticasPwd | No | Sí | No | No | Sí | Sí | Sí | 1 | Sí | Configuracion/04_Infraestructura.sql | No | No | 13 |
| 36 | ProvIden | Sí | No | No | No | Sí | Sí | Sí | 4 | Sí | Catalogo/05_ProvIden.sql | No | No | 5 |
| 37 | ResultadosAcceso | Sí | No | No | No | Sí | Sí | No | 18 | Sí | Catalogo/03_Resultados.sql | No | No | 2 |
| 38 | Roles | No | Sí | No | Sí | No | Sí | No | 2 | Sí | Configuracion/03_RolesGlobales.sql | Sí (demo) | No | 17 |
| 39 | RolesHerencia | No | Sí | No | Sí | No | Sí | No | 0 | No | Tenant/06_Accesos.sql | Sí | Sí | 24 |
| 40 | RolesPermisos | No | Sí | No | Sí | No | Sí | No | 69 | Sí | Configuracion/03_RolesGlobales.sql | Sí | No | 20 |
| 41 | RolesPoliticasPwd | No | Sí | No | Sí | No | Sí | No | 0 | No | Tenant/01_DatosGenerales.sql | Sí | Sí | 22 |
| 42 | Sesiones | No | No | Sí | No | No | No | No | 714 | No | Ninguno (auto) | Sí | Sí | 28 |
| 43 | TenantEmailAccounts | No | Sí | No | Sí | No | Sí | No | 1 | Sí | Tenant/04_EmailTenant.sql | Sí | Sí | 19 |
| 44 | Tenants | No | Sí | No | Sí | Sí | Sí | No | 2 | Sí | Configuracion/04_Infraestructura.sql | No | No | 15 |
| 45 | TipAsigPermiso | Sí | No | No | No | Sí | No | No | 2 | Sí | Catalogo/02_Tipos.sql | No | No | 3 |
| 46 | TiposAuditoria | Sí | No | No | No | Sí | Sí | No | 10 | Sí | Catalogo/02_Tipos.sql | No | No | 4 |
| 47 | TiposBloqueo | Sí | No | No | No | Sí | Sí | No | 4 | Sí | Catalogo/02_Tipos.sql | No | No | 4 |
| 48 | TiposCambioPwd | Sí | No | No | No | Sí | Sí | No | 6 | Sí | Catalogo/02_Tipos.sql | No | No | 4 |
| 49 | TiposDisp | Sí | No | No | No | Sí | Sí | No | 5 | Sí | Catalogo/02_Tipos.sql | No | No | 4 |
| 50 | TiposMFA | Sí | No | No | No | Sí | Sí | No | 6 | Sí | Catalogo/02_Tipos.sql | No | No | 4 |
| 51 | TiposModulo | Sí | No | No | No | Sí | Sí | No | 3 | Sí | Catalogo/04_TiposModulo.sql | No | No | 5 |
| 52 | TokensRest | No | No | Sí | No | No | Sí | No | 3 | No | Ninguno (auto) | Sí | Sí | 28 |
| 53 | UserAgents | No | No | Sí | No | No | Sí | Sí | 0 | No | Ninguno (auto) | Sí | Sí | 27 |
| 54 | Usuarios | No | Sí | No | Sí | Parcial | Sí | No | 386 | Parcial | Configuracion/07_Usuarios.sql | No (sistema) | No (sistema) | 19 |
| 55 | UsuariosPermisos | No | Sí | No | Sí | No | Sí | No | 0 | No | Tenant/06_Accesos.sql | Sí | Sí | 25 |

## Resumen por clasificación

| Clasificación | Cantidad | Tablas |
|--------------|----------|--------|
| **Catálogo** (13) | 13 | EstadosUsr, EstadosMFA, EstIdenExt, TiposMFA, TiposDisp, TiposCambioPwd, TiposBloqueo, TiposAuditoria, TipAsigPermiso, ResultadosAcceso, TiposModulo, ProvIden, Apps |
| **Configuración** (18) | 18 | Modulos, Permisos, Roles, RolesPermisos, RolesHerencia, RolesPoliticasPwd, Accesos, UsuariosPermisos, PoliticasPwd, ConfigApp, ConfigTenants, DominiosTenant, ConfProvIden, EmailProviders, EmailAccounts, EmailTemplates, EmailTemplatePartials, AppEmailAccounts, TenantEmailAccounts |
| **Operacional** (17) | 17 | Sesiones, TokensRest, AuditoriaPwd, AudIdenExt, IntentosAcceso, Bloqueos, MFA, Disp, DispConfiables, IPs, UserAgents, Notificaciones, EmailLog, HistorialPwd, HistorialIdenExt, IdenExt, IdenExtTokens |
| **Tenant** (5) | 5 | Tenants, Usuarios, Grupos, GruposUsuarios, EmailTemplateHistorial |

## Pendientes del seed actual

| Tabla | Estado | Acción requerida |
|-------|--------|-----------------|
| EstIdenExt | Sin seed | Agregar a Catalogo/01_Estados.sql (7 registros) |
| ProvIden | Solo 4 proveedores | Expandir a 7 (faltan MICROSOFT, APPLE, INSTAGRAM) |
| Permisos | 74 registros | Expandir a ~100+ desde controllers |
| Roles | Solo 2 | Expandir a 4 globales |
| ConfProvIden | Solo 1 registro | Mantener solo PLATFORM |
| EmailTemplates | 36 registros | Normalizar a 26 funcionales |
