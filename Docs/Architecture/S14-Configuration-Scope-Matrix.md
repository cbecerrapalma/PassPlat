# S14 — Configuration Scope Matrix

> Sprint S14 · FASE F1 (read-only) · Descubre el alcance (scope) de configuración de cada tabla.
> Fuente columnas `IdApp`/`IdTenant`: `D:\CODIGOS\BBDD\PASSWORDS.sql`.

**Objetivo**: Clasificar las tablas del catálogo/configuración por su scope de resolución
(PLATFORM → APP → TENANT) para no propagar `IdApp` entre capas de forma indiscriminada.
Este documento es **read-only**: no cambia código, define la matriz que usarán F2 (jerarquía) y F3 (fix OAuth).

---

## Clasificación de scope

| # | Tabla | IdApp | IdTenant | Scope | Comentario |
|---|-------|:-----:|:--------:|-------|------------|
| 1 | `Apps` | — | — | **PLATFORM_GLOBAL** | Catálogo de aplicaciones (sistema) |
| 2 | `AppsModulos` | ✓ | — | **APP_GLOBAL** | Módulos por app |
| 3 | `AppEmailAccounts` | ✓ | — | **APP_GLOBAL** | Cuenta SMTP por app |
| 4 | `Tenants` | — | — | PLATFORM_GLOBAL | Catálogo de tenants (sistema) |
| 5 | `ConfigTenants` | — | ✓ | TENANT | Config general del tenant |
| 6 | `DominiosTenant` | — | ✓ | TENANT | Dominios permitidos por tenant |
| 7 | `ConfLdap`, `ConfSaml` | — | ✓ | TENANT | Config federación por tenant |
| 8 | `ConfigApp` | — | ✓ | TENANT | Parámetros de aplicación por tenant |
| 9 | `Usuarios` | — | ✓ | TENANT*(+global sistema)* | Usuarios de pipeline/membership |
| 10 | `UsuarioTenant` | — | ✓ | TENANT | Membresía usuario→tenant |
| 11 | `Grupos` | — | ✓ | TENANT | Grupos por tenant |
| 12 | `GruposUsuarios` | — | — | TENANT*(derivada)* | Relación grupos↔usuarios |
| 13 | `Roles` | — | ✓ | TENANT | Roles por tenant |
| 14 | `RolesPermisos` | — | — | TENANT*(derivada)* | Roles↔Permisos (por rol/tenant) |
| 15 | `RolesHerencia` | — | ✓ | TENANT | Herencia de roles |
| 16 | `RolesPoliticasPwd` | — | ✓ | TENANT | Política de pwd por rol |
| 17 | `PoliticasPwd` | ✓ | ✓ | APP_TENANT | Política por app+tenant (filtrada) |
| 18 | `Permisos` | — | — | PLATFORM_GLOBAL | Catálogo de permisos (sistema) |
| 19 | `TipAsigPermiso` | — | — | CATALOG | Tipo de asignación de permiso |
| 20 | `Modulos` | — | — | PLATFORM_GLOBAL | Catálogo de módulos |
| 21 | `TiposModulo` | — | — | CATALOG | Tipo de módulo |
| 22 | `UsuariosPermisos` | ✓ | ✓ | APP_TENANT | Permisos concedidos usr-app-tenant |
| 23 | `Accesos` | ✓ | ✓ | APP_TENANT | Acceso usr a app en tenant |
| 24 | `Sesiones` | ✓ | ✓ | SESSION | Sesión activa (usr/app/tenant) |
| 25 | `TokensRest` | ✓ | ✓ | SESSION | Token de restablecimiento |
| 26 | `IntentosAcceso` | ✓ | ✓ | AUDIT/RUNTIME | Intentos de acceso |
| 27 | `AuditoriaPwd` | ✓ | ✓ | AUDIT | Auditoría de cambios de pwd |
| 28 | `EmailLog` | ✓ | ✓ | AUDIT | Registro de emails |
| 29 | `MFA` | — | ✓ | TENANT*(por user)* | Métodos MFA del usuario |
| 30 | `Disp` | — | — | CONTEXT | Dispositivo (sistema) |
| 31 | `DispConfiables` | — | ✓ | TENANT*(por user)* | Dispositivos confiables |
| 32 | `IPs` | — | — | CONTEXT | IPs (sistema) |
| 33 | `UserAgents` | — | — | CONTEXT | User agents (sistema) |
| 34 | `HistorialPwd` | — | — | USER | Historial de claves |
| 35 | `ProvIden` | — | — | PLATFORM_GLOBAL | Catálogo de proveedores OAuth (sistema) |
| 36 | `ConfProvIden` | — | ✓ | TENANT+PLATFORM | Config proveedor por tenant (hereda plataforma) |
| 37 | `IdenExt` | — | ✓ | TENANT*(por user)* | Identidades externas vinculadas |
| 38 | `IdenExtTokens` | — | — | PLATFORM_GLOBAL | Tokens del proveedor (cifrado) |
| 39 | `HistorialIdenExt` | — | ✓ | TENANT*(por user)* | Historial de vínculos |
| 40 | `AudIdenExt` | — | ✓ | AUDIT | Auditoría federación |
| 41 | `EmailProviders` | — | — | PLATFORM_GLOBAL | Proveedores SMTP (sistema) |
| 42 | `EmailAccounts` | — | — | PLATFORM_GLOBAL | Cuentas SMTP (sistema) |
| 43 | `TenantEmailAccounts` | — | ✓ | TENANT | Cuenta SMTP por tenant |
| 44 | `EmailTemplates` | — | ✓ | TENANT | Plantillas por tenant |
| 45 | `EmailTemplatePartials` | — | — | PLATFORM_GLOBAL | Partials de plantilla |
| 46 | `EmailTemplateHistorial` | — | — | PLATFORM_GLOBAL | Historial de plantillas |
| 47 | `EstadosUsr` | — | — | CATALOG | Estados de usuario |
| 48 | `EstadosMFA` | — | — | CATALOG | Estados MFA |
| 49 | `ResultadosAcceso` | — | — | CATALOG | Resultados de acceso |
| 50 | `TiposMFA` | — | — | CATALOG | Tipos MFA |
| 51 | `TiposBloqueo` | — | — | CATALOG | Tipos de bloqueo |
| 52 | `TiposCambioPwd` | — | — | CATALOG | Tipos de cambio de pwd |
| 53 | `TiposDisp` | — | — | CATALOG | Tipos de dispositivo |
| 54 | `TiposAuditoria` | — | — | CATALOG | Tipos de auditoría |
| 55 | `Sesiones` | ✓ | ✓ | SESSION | Ver #24 |

---

## Tablas con `IdApp` (requieren contexto App)

`IdApp` está presente en **10 tablas**: `AppsModulos`, `AppEmailAccounts`,
`PoliticasPwd`, `UsuariosPermisos`, `Accesos`, `Sesiones`, `TokensRest`,
`IntentosAcceso`, `AuditoriaPwd`, `EmailLog`.

- **APP_GLOBAL** (IdApp, sin IdTenant): `AppsModulos`, `AppEmailAccounts`
- **APP_TENANT** (ambas): `PoliticasPwd`, `UsuariosPermisos`, `Accesos`, `IntentosAcceso`, `AuditoriaPwd`, `EmailLog`
- **SESSION** (ambas): `Sesiones`, `TokensRest`

---

## Reglas de scope (derivadas)

1. **PLATFORM_GLOBAL/SYSTEM**: directorios globales — no se resuelven por app ni tenant (`Apps`, `Tenants`, `Permisos`, `Modulos`, `ProvIden`, `EmailProviders`, `EmailTemplates`).
2. **TENANT**: resolución por tenant únicamente.
3. **APP_TENANT**: resolución **compartida** por app+tenant (políticas, accesos, permisos).
4. **SESSION/AUDIT/RUNTIME**: datos efímeros — no forma parte del scope de configuración estático; dependen del contexto actual (read `Sesiones`, `TokensRest`, `IntentosAcceso`).
5. **F1 no modifica código**: solo define la topología para resolver `IdApp`/`IdTenant` sin vectores (ni confusiones tipo `IdApp=1` hardcodeado en OAuth).

---

*Siguiente paso*: **F2 — Configuration Hierarchy** (cómo resuelves por orden PLATFORM→APP→TENANT).