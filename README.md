# PassPlat

**Identity & Access Management Platform — Work in Progress**

Plataforma en desarrollo para gestión de identidad, acceso y permisos para múltiples aplicaciones y tenants.

## Estado

**Proyecto en desarrollo (WIP)** — no production-ready. Enfocado en arquitectura limpia, seguridad y multi-tenancy.

## Objetivo

Resolver la gestión centralizada de usuarios, aplicaciones, roles, permisos y auditoría para un ecosistema de aplicaciones con organizaciones/tenants aislados, con autenticación moderna y correo transaccional.

## Características

- Usuarios, aplicaciones, tenants, roles, permisos, matriz de permisos
- Autenticación (login, refresh token, bloqueo, recuperación password), sesiones, dispositivos confiables
- Password management (políticas, historial, expiración, `PasswordService`)
- MFA (TOTP, `MfaService` `AppName=PassPlat`)
- Sesiones y bloqueo (`SesionService`, `BloqueoService`, `IntentoAccesoService`)
- Email subsystem (`EmailTemplate` → `EmailJob` → `Queue` → `Background` → `SMTP`)
- Eventos y Outbox (`EventBase`, `OutboxProcessor`, `DispConfiableEventHandlers`)
- Auditoría (`AuditoriaPwd`, `AudIdenExt`, `DashboardEnterprise`)
- Dashboards operacional y enterprise
- Background jobs (`PasswordExpirationBackgroundService`, `IdenExtTokensRotacionJob`)

## Multi-tenancy

- **Platform scope** (`is_system`) vs **Tenant scope** (`IdTenant`)
- `UsuarioTenant` (usuario pertenece a tenant), `TenantSwitcher`, `ITenantContext`
- Aislamiento contextual `IdTenant` FK `Restrict`, `ICollection<T> = []`, filtered indexes, `DeleteBehavior.Restrict`

## Seguridad

- `JWT` (HS256, `JwtTokenService`, `CBP.Authentication.JwtBearer`, `kid` ambiental documentado)
- `Argon2id` (pepper externo/versionado), `PBKDF2` soportado por `CBP` (`HashingService` `100k` floor)
- `MFA` local tras OAuth externo
- `OAuth2`/`OIDC` `Authorization Code + PKCE S256`, `state`/`nonce`, replay protection, callback server-side `ConfProvIden.Callback` (HTTPS)
- `AES-256` para secrets/refresh tokens, `CorrelationId` W3C (`EmailQueue` choke point), `SQL` sin `ConcurrentDictionary` en prod

## Arquitectura

```
PassPlat.Web (Blazor WASM, MudBlazor 9.8.0, Monaco)
  ↓
PassPlat.WebAPI (HTTP, JWT, Middleware, DI)
  ↓
PassPlat.Aplicacion (DTOs, Validators, Services, Security, Email)
  ↓
PassPlat.Datos (EF Core, DbContext, Repositories, SP/RawQuery)
  ↓
PassPlat.Dominio (Entities, Factories, Enums, Domain Rules)
```

- `Aplicacion → Datos → Dominio` (Dominio nunca depende de `WebAPI`/`UI`/`SQL`)
- `RepositoryAsync<TEntity>` `Task<Result<T>>` `DB_ERROR`, `BaseApiController` `FromResult`, `EventBase` dispatch
- `CBP` desacoplado, compatible `InventaNet`

## Email Subsystem

`EmailTemplate` (`Asunto`/`CuerpoHtml`/`CuerpoTexto`) → `EmailJob` (`EmailJobKind`) → `EmailQueue.EnqueueAsync` (CorrelationId) → `EmailBackgroundService`/`OutboxProcessor` → `PassPlatEmailService` (Fluid `{{AppName}}`) → `MailKit`/`MimeKit` → `SMTP` (Gmail) → `EmailLog` (`W3C` `CorrelationId`)

## OAuth

Providers implementados: **Google**, **Microsoft**, **GitHub**, **LinkedIn**, **Apple**, **Facebook** (+ `ExternalAuthService` DI factory, `PKCE S256`, `state`/`nonce`, `JWKS`/`issuer`/`audience`/`lifetime`, `clock skew 5m`, `IdenExt`/`IdenExtTokens`).

## Framework CBP

PassPlat utiliza **CBP**, framework reutilizable local `D:\CODIGOS\CBP` (11 módulos):

`Core` (`CBP`, `Results`, `Events`), `Data` (`Abstractions`, `Asynchronous`, `Specifications`, `Synchronous`, `Utilities`), `Services` (`Abstractions`, `Async`, `Sync`), `Security.Cryptography` (`Argon2`, `Password`), `Logging`, `Caching` (`Abstractions`, `Memory`, `Redis`, `NCache`), `MultiTenant`, `Emails`, `Excel` (`EPPlus`), `Authentication` (`JwtBearer`), `WebApi`.

**CBP será publicado posteriormente en su propio repositorio** (`https://github.com/cbecerrapalma/CBP` futuro).

## Tecnologías

`C#` · `.NET 10` · `ASP.NET Core` · `Blazor WebAssembly` · `MudBlazor 9.8.0` · `SQL Server` · `T-SQL` · `Entity Framework Core` · `OAuth2/OIDC` · `JWT` · `Argon2id` · `MailKit`/`MimeKit` · `Fluid` · `Monaco`/`BlazorMonaco 3.5.0`

## Solución

```
PassPlat.slnx (29 proyectos)
├── PassPlat.Dominio
├── PassPlat.Datos
├── PassPlat.Aplicacion
├── PassPlat.Aplicacion.Dtos
├── PassPlat.WebAPI
├── PassPlat.Web (Blazor WASM)
├── PassPlat.Consola
├── PassPlat.Aplicacion.Test
└── PassPlat.CBP.Architecture.Test
```

## Aviso técnico CBP

PassPlat depende del framework **CBP** externo en `D:\CODIGOS\CBP` (47 `ProjectReference` `../CBP/`). El repositorio `PassPlat` no contiene `CBP`. Clonar `PassPlat` requiere disponer de `CBP` en la ruta esperada actualmente — no indica repositorio roto.

## Estado del proyecto

**WIP / In Development** — S47 publicado en GitHub (`master` `9e800ef`), S45 `CBP 25 proj` `56/56`, S46 `EmailTemplates` Monaco `500px` + `iframe sandbox`.

## Autor

**Claudio Becerra Palma** — .NET / C# Backend Developer
