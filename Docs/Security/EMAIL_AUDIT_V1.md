# Auditoría Completa Email Subsystem V1

**Fecha**: 2026-06-23
**Proyecto**: PassPlat
**Versión**: 1.0
**Score subsistema Email**: ~45/100

---

## Índice

1. [FASE 1 — Modelo de Datos Email](#fase-1--modelo-de-datos-email)
2. [FASE 2 — Seeds Email](#fase-2--seeds-email)
3. [FASE 3 — Resolución de Cuenta Email](#fase-3--resolucion-de-cuenta-email)
4. [FASE 4 — Pipeline Real de Envío](#fase-4--pipeline-real-de-envio)
5. [FASE 5 — Email Logs](#fase-5--email-logs)
6. [FASE 6 — Eventos de Negocio](#fase-6--eventos-de-negocio)
7. [FASE 7 — Validación Playwright](#fase-7--validacion-playwright)
8. [FASE 8 — CBP.Emails](#fase-8--cbpemails)
9. [FASE 9 — Correcciones Requeridas](#fase-9--correcciones-requeridas)
10. [FASE 10 — Entregable Final](#fase-10--entregable-final)

---

## FASE 1 — Modelo de Datos Email

### 1.1 Tablas Existentes (8)

| # | Tabla | Schema SQL | Entidad C# | PK | Tipo |
|---|-------|-----------|------------|----|------|
| 1 | `EmailProviders` | `PASSWORDS.sql` | `EmailProvider` | `tinyint` | Catálogo |
| 2 | `EmailAccounts` | `PASSWORDS.sql` | `EmailAccount` | `int` | Core |
| 3 | `TenantEmailAccounts` | `PASSWORDS.sql` | `TenantEmailAccount` | `int` | Core (join) |
| 4 | `AppEmailAccounts` | `PASSWORDS.sql` | `AppEmailAccount` | `int` | Core (join) |
| 5 | `EmailTemplates` | `PASSWORDS.sql` | `EmailTemplate` | `int` | Core |
| 6 | `EmailTemplateHistorial` | `PASSWORDS.sql` | `EmailTemplateHistorial` | `bigint` | Core |
| 7 | `EmailTemplatePartials` | `PASSWORDS.sql` | `EmailTemplatePartial` | `int` | Core |
| 8 | `EmailLog` | `PASSWORDS.sql` | `EmailLog` | `bigint` | Core |

### 1.2 Tablas NO Existentes

| Tabla | Status | Nota |
|-------|--------|------|
| `EmailQueue` | ❌ No existe | `EmailLog` (Estado='pendiente') funciona como cola |
| `EmailTemplateVariables` | ❌ No existe | Las variables se documentan en `VariablesDoc` (texto libre) |
| `EmailTemplateTags` | ❌ No existe | Sin sistema de etiquetado |

### 1.3 Relaciones FK

```
EmailProviders (tinyint PK)
    │
    ├── 1:N ── EmailAccounts.IdProvider
    │
EmailAccounts (int PK) ── N:1 ── Usuarios (IdUsrMod)
    │
    ├── 1:N ── TenantEmailAccounts.IdEmailAccount
    ├── 1:N ── AppEmailAccounts.IdEmailAccount
    ├── 1:N ── EmailLog.IdEmailAccount
    │
TenantEmailAccounts
    ├── N:1 ── Tenants (IdTenant)
    └── N:1 ── EmailAccounts (IdEmailAccount)

AppEmailAccounts
    ├── N:1 ── Apps (IdApp)
    └── N:1 ── EmailAccounts (IdEmailAccount)

EmailTemplates
    ├── N:1 ── Tenants (IdTenant) — NULLABLE (global)
    ├── N:1 ── Usuarios (IdUsrMod)
    └── 1:N ── EmailTemplateHistorial (IdTemplate)

EmailLog
    ├── N:1 ── Tenants (IdTenant) — nullable
    ├── N:1 ── Usuarios (IdUsuario) — nullable
    ├── N:1 ── Apps (IdApp) — nullable
    ├── N:1 ── EmailTemplates (IdTemplate) — nullable
    └── N:1 ── EmailAccounts (IdEmailAccount) — nullable
```

### 1.4 Indexes de Prioridad (Filtered Unique)

| Índice | Tabla | Filtro | Propósito |
|--------|-------|--------|-----------|
| `IX_EmailAccounts_Predet` | EmailAccounts | `EsPredeterminada=1 AND Activo=1` | Una cuenta default global |
| `UX_TenantEmailAcct_Predet` | TenantEmailAccounts | `EsPredeterminada=1 AND Activo=1` | Una cuenta default por tenant |
| `UX_AppEmailAcct_Predet` | AppEmailAccounts | `EsPredeterminada=1 AND Activo=1` | Una cuenta default por app |

### 1.5 Estados

| Tabla | Columna | Valores |
|-------|---------|---------|
| EmailProviders | `Activo` | `bit` |
| EmailAccounts | `Activo`, `EsPredeterminada` | `bit` |
| TenantEmailAccounts | `Activo`, `EsPredeterminada` | `bit` |
| AppEmailAccounts | `Activo`, `EsPredeterminada` | `bit` |
| EmailTemplates | `Estado` | `borrador`, `publicado`, `desactivado` |
| EmailTemplatePartials | `Activo` | `bit` |
| EmailLog | `Estado` | `pendiente`, `enviado`, `fallido`, `rebotado` |

### 1.6 Discrepancias EF vs SQL

| Issue | SQL | EF | Impacto |
|-------|-----|-----|---------|
| EmailTemplate index `Estado` | ❌ No existe | `IX_EmailTpl_Estado` | EF-only, no crítico |
| EmailTemplate index `IdTenant` | ❌ No existe | `IX_EmailTpl_Tenant` | EF-only, no crítico |
| EmailTemplateHistorial DESC index | ❌ No existe | `IsDescending(false, true)` | EF-only |

---

## FASE 2 — Seeds Email

### 2.1 Origen

Los seeds se encuentran en **`SEED_DATA.sql`** (líneas 814-1103). NO hay `HasData()` en EF Configurations.

### 2.2 EmailProviders

| Id | Codigo | Nombre | Activo |
|----|--------|--------|--------|
| 1 | SMTP | SMTP | ✅ |
| 2 | SENDGRID | SendGrid | ✅ |
| 3 | SES | Amazon SES | ✅ |
| 4 | GRAPH | Microsoft Graph | ✅ |
| 5 | MAILGUN | Mailgun | ✅ |

**5/5 Activos**

### 2.3 EmailAccounts

| Id | IdProvider | Nombre | Host | Puerto | Usuario | TLS | Default | Activo |
|----|------------|--------|------|--------|---------|-----|---------|--------|
| 1 | 1 (SMTP) | SMTP Global | smtp.gmail.com | 587 | cbpnotificaciones@gmail.com | ✅ | ✅ | ✅ |

**1 cuenta activa.** Password encriptado AES-256: `hDXUFy3InT4umM9PnuR314lTq2ofMa4eaW9Cvizq3ZRr0PmNNf8FqjrFbwYp1DM=`

### 2.4 TenantEmailAccounts

| Id | IdTenant | IdEmailAccount | EsPredeterminada | Activo |
|----|----------|----------------|------------------|--------|
| 1 | 1 (Plataforma) | 1 (SMTP Global) | ✅ | ✅ |

### 2.5 AppEmailAccounts

| Id | IdApp | IdEmailAccount | EsPredeterminada | Activo |
|----|-------|----------------|------------------|--------|
| 1 | 1 (PassPlat) | 1 (SMTP Global) | ✅ | ✅ |

### 2.6 EmailTemplatePartials

| Id | Nombre | Activo |
|----|--------|--------|
| 1 | button | ✅ |
| 2 | card-alert | ✅ |

### 2.7 EmailTemplates (22 templates, todos globales)

| Id | Nombre | Categoria | Estado | Version |
|----|--------|-----------|--------|---------|
| 1 | `_layout` | sistema | publicado | 1 |
| 2 | `password-reset` | transaccional | publicado | 1 |
| 3 | `mfa-code` | transaccional | publicado | 1 |
| 4 | `welcome` | transaccional | publicado | 1 |
| 5 | `security-alert` | alerta | publicado | 1 |
| 6 | `account-locked` | alerta | publicado | 1 |
| 7 | `password-changed` | alerta | publicado | 1 |
| 8 | `user-activated` | transaccional | publicado | 1 |
| 9 | `user-deactivated` | alerta | publicado | 1 |
| 10 | `user-unblocked` | transaccional | publicado | 1 |
| 11 | `password-expired` | alerta | publicado | 1 |
| 12 | `first-login` | transaccional | publicado | 1 |
| 13 | `mfa-enabled` | seguridad | publicado | 1 |
| 14 | `mfa-disabled` | seguridad | publicado | 1 |
| 15 | `new-device` | seguridad | publicado | 1 |
| 16 | `new-ip` | seguridad | publicado | 1 |
| 17 | `role-assigned` | permisos | publicado | 1 |
| 18 | `role-removed` | alerta | publicado | 1 |
| 19 | `tenant-created` | plataforma | publicado | 1 |
| 20 | `tenant-suspended` | plataforma | publicado | 1 |
| 21 | `tenant-reactivated` | plataforma | publicado | 1 |
| 22 | `user-unblocked-2` | — | — | — |

**22/22 publicados, versión 1, globales (IdTenant=NULL), Cultura='es'**

---

## FASE 3 — Resolución de Cuenta Email

### 3.1 Estado Actual (GAP CRÍTICO)

**`PassPlatEmailService`** (`PassPlat.Aplicacion/Services/Email/PassPlatEmailService.cs:286` líneas):

```csharp
// Line 14: Usa ConfigAppRepository, NO EmailAccountRepository
private readonly ConfigAppRepository _configAppRepo;

// Line 185: Obtiene config desde ConfigApp grupo "Email"
var configResult = await _configAppRepo.ObtenerPorGrupoAsync(accountGroup, ct);

// Lines 213-258: BuildEmailSettings lee claves "SmtpHost", "SmtpPort", "SmtpUser", "SmtpPassword", "FromAddress", "FromName"
```

**NO usa** las tablas:
- ❌ `EmailAccounts` (host, puerto, credenciales, TLS/SSL, from)
- ❌ `TenantEmailAccounts` (cuenta default por tenant)
- ❌ `AppEmailAccounts` (cuenta default por app)
- ❌ `EmailProviders` (tipo de provider, configuración por defecto)

### 3.2 Resolución Correcta (DEBERÍA SER)

```
1. ¿Hay AppEmailAccount con EsPredeterminada=1 para IdApp?
   ├── Sí → Usar EmailAccount.Id
   └── No → 2

2. ¿Hay TenantEmailAccount con EsPredeterminada=1 para IdTenant?
   ├── Sí → Usar EmailAccount.Id
   └── No → 3

3. ¿Hay EmailAccount con EsPredeterminada=1 (global)?
   ├── Sí → Usar esa cuenta
   └── No → EMAIL_CONFIG_ERROR
```

### 3.3 Encriptación

| Aspecto | ConfigApp (actual) | EmailAccounts (requerido) |
|---------|--------------------|---------------------------|
| Context key | `ConfigApp:{Clave}` | `EmailAccount:{Id}` |
| Método | `IEncryptionService.Decrypt(config.Valor, ctx)` | `IEncryptionService.Decrypt(entity.Password, ctx)` |
| Algoritmo | AES-256 | AES-256 |

### 3.4 ¿Hay bypass/hardcode?

**NO.** No se lee `appsettings.json`, no hay variables de entorno, no hay valores hardcodeados. El pipeline valida estrictamente: sin configuración → `EMAIL_CONFIG_ERROR`.

---

## FASE 4 — Pipeline Real de Envío

### 4.1 Diagrama de Flujo (Actual)

```
Evento de Negocio (AuthService, BloqueoService, IPEvents, etc.)
    │
    ▼
IEmailQueue.EnqueueAsync(EmailJob)  ← Channel<EmailJob> (in-memory, capacidad 1024)
    │
    ▼
EmailBackgroundService.ExecuteAsync()  ← BackgroundService, SingleReader
    │
    ▼
EmailBackgroundService.DispatchAsync(EmailJob)
    │   ├── Crea DI scope
    │   └── Switch (28 EmailJobKind)
    │
    ▼
PassPlatEmailService.SendXxxAsync(toEmail, userName, ...)
    │
    ▼
PassPlatEmailService.SendFromTemplateAsync(templateCode, toEmail, variables)
    │   ├── RenderSubjectAsync (Fluid + DB templates)
    │   ├── RenderBodyAsync (Fluid + DB templates)
    │   ├── EnrichWithBrandingAsync (ConfigApp: LogoUrl)
    │   └── SendEmailAsync(toEmail, subject, body, isHtml, group)
    │
    ▼
PassPlatEmailService.GetOrCreateEmailServiceAsync(group)
    │   ├── ConfigAppRepository.ObtenerPorGrupoAsync("Email")  ← GAP: debe usar EmailAccounts
    │   ├── BuildEmailSettings(group, configs)
    │   └── Crea/Cachea CBP.Emails.EmailService
    │
    ▼
CBP.Emails.EmailService.SendEmailAsync(toEmail, subject, body, isHtml)
    │   ├── SendWithProviderSelectionAsync (RoundRobin/Priority/Random)
    │   └── SmtpProvider.SendAsync (MailKit)
    │       ├── ConnectAsync (STARTTLS/SSL)
    │       ├── AuthenticateAsync (user/pass)
    │       ├── SendAsync (MimeMessage)
    │       └── DisconnectAsync
    │
    ▼
[Resultado] → EmailResult.Success o Failure
    │
    ├── Éxito: log info, retorna Result<EmailResult>.Success
    └── Falla: log error, retorna Result<EmailResult>.Failure, JOB PERDIDO
         EmailLog NUNCA se escribe
```

### 4.2 Gaps Identificados en Pipeline

| # | Gap | Severidad | Descripción |
|---|-----|-----------|-------------|
| **G1** | **Resolución SMTP desde EmailAccounts** | 🔴 CRÍTICO | PassPlatEmailService usa ConfigApp, no EmailAccounts/TenantEmailAccounts/AppEmailAccounts |
| **G2** | **EmailLog nunca se escribe** | 🔴 CRÍTICO | Tabla EmailLog con estructura completa pero 0 registros generados |
| **G3** | **Sin retry mechanism** | 🔴 CRÍTICO | Fallo SMTP = job perdido. Sin backoff, sin maxAttempts, sin dead letter |
| **G4** | **Queue in-memory** | 🔴 CRÍTICO | App restart = todos los jobs pendientes perdidos |
| **G5** | **NewDevice/NewIp/SecurityAlert sin destinatario** | 🟡 ALTO | `ToEmail=""`, `UserName=""` — el email nunca llega |
| **G6** | **CorrelationId no propagado** | 🟡 ALTO | EventBase.CorrelationId se pierde en EmailJob. No hay trazabilidad evento→email |
| **G7** | **Sin paralelismo** | 🟡 ALTO | SingleReader = procesamiento secuencial |
| **G8** | **Sin dead letter** | 🟡 ALTO | Emails permanentemente fallidos no tienen visibilidad |
| **G9** | **EmailLog.Intentos/FecUltIntento/MsgIdExterno no usados** | 🟡 ALTO | Campos existen pero nunca se escriben |
| **G10** | **Rate limiting sin persistencia** | 🟢 MEDIO | DailySendLimit configurado pero contador resetea en cada restart |

---

## FASE 5 — Email Logs

### 5.1 Tabla EmailLog — Estructura

| Columna | Tipo | Uso Actual |
|---------|------|------------|
| `Id` | `bigint` PK | Sin uso |
| `IdTenant` | `int` nullable | Sin uso |
| `IdUsuario` | `int` nullable | Sin uso |
| `IdApp` | `int` nullable | Sin uso |
| `IdTemplate` | `int` nullable | Sin uso |
| `IdEmailAccount` | `int` nullable | Sin uso |
| `Destinatario` | `nvarchar(255)` | Sin uso |
| `Asunto` | `nvarchar(500)` | Sin uso |
| `Estado` | `varchar(20)` CHECK | Sin uso — nunca se escribe |
| `Proveedor` | `varchar(50)` | Sin uso |
| `MsgIdExterno` | `nvarchar(200)` | Sin uso |
| `Intentos` | `tinyint` (0-255) | Sin uso |
| `FecEnvio` | `datetime2(3)` | Sin uso |
| `FecUltIntento` | `datetime2(3)` | Sin uso |
| `ErrorDetalle` | `nvarchar(500)` | Sin uso |
| `FecCrea` | `datetime2(3)` | Sin uso |

### 5.2 Estado Actual

**0 registros.** La tabla EmailLog está vacía porque el pipeline de envío nunca la escribe.

### 5.3 Estados Definidos (CHECK constraint)

| Estado | Descripción | ¿Se usa? |
|--------|-------------|----------|
| `pendiente` | Cola / pendiente de procesar | ❌ |
| `enviado` | Envío exitoso | ❌ |
| `fallido` | Error de envío | ❌ |
| `rebotado` | Rechazado por servidor destino | ❌ |

### 5.4 Indexes de EmailLog

| Index | Filtro | Propósito |
|-------|--------|-----------|
| `IX_EmailLog_Estado` | `Estado='pendiente'` | Polling de cola |
| `IX_EmailLog_Purga` | `Estado IN ('enviado','fallido','rebotado')` | Mantenimiento/purga |
| `IX_EmailLog_Tenant` | `IdTenant IS NOT NULL` | Consultas por tenant |
| `IX_EmailLog_Usuario` | `IdUsuario IS NOT NULL` | Consultas por usuario |
| `IX_EmailLog_App` | `IdApp IS NOT NULL` | Consultas por app |
| `IX_EmailLog_EmailAccount` | `IdEmailAccount IS NOT NULL` | Consultas por cuenta |

---

## FASE 6 — Eventos de Negocio

### 6.1 Matriz Evento → Template → Email

| Página/Acción | Evento | Template | Código | EmailJobKind | ToEmail | Estado |
|---------------|--------|----------|--------|--------------|---------|--------|
| Login MFA | MFA requerido | `mfa-code` (3) | `AuthService.EnviarCodigoMFAAsync` | `MfaCode` | ✅ Explícito | ✅ |
| Reset Password | Token generado | `password-reset` (2) | `TokenRestService.NotificarResetPasswordAsync` | `PasswordReset` | ✅ Explícito | ✅ |
| Cambio Password | Password cambiado | `password-changed` (7) | `PasswordService.CambiarPasswordAsync` | `PasswordChanged` | ✅ Explícito | ✅ |
| Admin Cambio Password | Password forzado | `password-changed` (7) | `PasswordService.AdminCambiarPasswordAsync` | `FirstLogin` | ✅ Explícito | ✅ |
| Primer Login | Password primer uso | `first-login` (12) | `PasswordService.CambiarPasswordAsync` | `FirstLogin` | ✅ Explícito | ✅ |
| Bloqueo Cuenta | Cuenta bloqueada | `account-locked` (6) | `BloqueoService.CrearBloqueoAsync` | `AccountLocked` | ✅ Explícito | ✅ |
| MFA Activado | MFA registrado | `mfa-enabled` (13) | `MfaService.NotificarMFAAsync` | `MfaEnabled` | ✅ Explícito | ✅ |
| MFA Desactivado | MFA revocado | `mfa-disabled` (14) | `MfaService.NotificarMFAAsync` | `MfaDisabled` | ✅ Explícito | ✅ |
| **Nuevo Dispositivo** | Dispositivo confiado | `new-device` (15) | `DispConfiableEventPublisher.PublishNewDeviceAsync` | `NewDevice` | ❌ `""` | ⚠️ |
| **Dispositivo Revocado** | Dispositivo revocado | *(ninguno)* | `DispConfiableEventPublisher.PublishDeviceRevokedAsync` | `DeviceRevoked` | ❌ `""` | ⚠️ |
| **Nueva IP** | IP detectada | `new-ip` (16) | `IPEventPublisher.PublishNewIpAsync` | `NewIp` | ❌ `""` | ⚠️ |
| **Alerta Seguridad** | Actividad sospechosa | `security-alert` (5) | `IPEventPublisher.PublishSecurityAlertAsync` | `SecurityAlert` | ❌ `""` | ⚠️ |
| Rol Asignado | Permiso asignado | `role-assigned` (17) | `AccesoService.NotificarAccesoAsync` | `RoleAssigned` | ✅ Explícito | ✅ |
| Rol Removido | Permiso removido | `role-removed` (18) | `AccesoService.NotificarAccesoAsync` | `RoleRemoved` | ✅ Explícito | ✅ |
| Usuario Activado | Cuenta activada | `user-activated` (8) | `UsuarioService.NotificarEstadoAsync` | `UserActivated` | ✅ Explícito | ✅ |
| Usuario Desactivado | Cuenta desactivada | `user-deactivated` (9) | `UsuarioService.NotificarEstadoAsync` | `UserDeactivated` | ✅ Explícito | ✅ |
| Usuario Desbloqueado | Cuenta desbloqueada | `user-unblocked` (10) | `UsuarioService.NotificarEstadoAsync` | `UserUnblocked` | ✅ Explícito | ✅ |
| Password Expirado | Expiración detectada | `password-expired` (11) | `PasswordExpirationBackgroundService.ProcesarUsuarioAsync` | `PasswordExpired` | ✅ Explícito | ✅ |
| Bienvenida | Usuario creado | `welcome` (4) | `UsuarioService.NotificarEstadoAsync` | `Welcome` | ✅ Explícito | ✅ |
| Tenant Creado | Nuevo tenant | `tenant-created` (19) | `TenantService.NotificarTenantAsync` | `TenantCreated` | ✅ Explícito | ✅ |
| Tenant Suspendido | Tenant suspendido | `tenant-suspended` (20) | `TenantService.NotificarTenantAsync` | `TenantSuspended` | ✅ Explícito | ✅ |
| Tenant Reactivado | Tenant reactivado | `tenant-reactivated` (21) | `TenantService.NotificarTenantAsync` | `TenantReactivated` | ✅ Explícito | ✅ |

### 6.2 Eventos Sin Template

| Evento | EmailJobKind | Template |
|--------|-------------|----------|
| DeviceRevoked | `DeviceRevoked` | ❌ Ninguno — `DispatchAsync` no tiene case para `DeviceRevoked` |

### 6.3 Templates Sin Evento

| Template Id | Nombre | Evento que lo dispara |
|-------------|--------|----------------------|
| — | `user-unblocked-2` (22) | ❌ No identificado — posible duplicado del 10 |

### 6.4 Eventos Sin Envío (ToEmail vacío)

| Evento | Causa Raíz |
|--------|-----------|
| `NewDevice` | `DispConfiableEventPublisher` no resuelve email del usuario |
| `DeviceRevoked` | Igual que NewDevice + sin case en DispatchAsync |
| `NewIp` | `IPEventPublisher` no tiene acceso al usuario que originó el evento |
| `SecurityAlert` | Mismo problema que NewIp |

---

## FASE 7 — Validación Playwright

### 7.1 Tests Existentes

| Archivo | Tests | Cobertura |
|---------|-------|-----------|
| `tests/e2e.spec.ts` | 34 | Navegación página, componentes, health API |
| `tests/crud-validation.spec.ts` | 13 | CRUD API (Apps, Grupos, Permisos) |

**Total: 47 tests**

### 7.2 ¿Cobertura Email?

**NINGUNA.** Los tests Playwright no verifican:
- ❌ EmailLogs (tabla vacía no verificada)
- ❌ EmailQueue procesado
- ❌ Template rendering
- ❌ SMTP ejecutado
- ❌ AuditoriaPwd
- ❌ IntentosAcceso
- ❌ Notificaciones

### 7.3 Acciones Ejecutadas en Tests vs Email Esperado

| Acción | ¿En test? | Email esperado | ¿Verificado? |
|--------|-----------|----------------|--------------|
| Crear usuario | ❌ No | `welcome` | ❌ |
| Reset password | ❌ No | `password-reset` | ❌ |
| Bloquear usuario | ❌ No | `account-locked` | ❌ |
| Login MFA | ❌ No | `mfa-code` | ❌ |
| Nueva IP | ❌ No | `new-ip` | ❌ |
| Nuevo dispositivo | ❌ No | `new-device` | ❌ |

### 7.4 Conclusión

Los 47 tests Playwright **NO validan el subsistema Email**. Si se ejecutaron flujos que deberían disparar correos, las tablas `EmailLog`, `AuditoriaPwd`, `IntentosAcceso`, `Notificaciones` permanecieron vacías. **La validación E2E no comprobó el comportamiento real del subsistema Email.**

---

## FASE 8 — CBP.Emails

### 8.1 Auditoría de Librería

| Componente | Archivo | Estado |
|------------|---------|--------|
| `EmailService` | `CBP.Emails/Services/EmailService.cs` | ✅ |
| `SmtpProvider` | `CBP.Emails/Providers/SmtpProvider.cs` | ✅ (MailKit) |
| `EmailSettings` | `CBP.Emails/Configuration/EmailSettings.cs` | ✅ |
| `SmtpAccountConfig` | `CBP.Emails/Configuration/SmtpAccountConfig.cs` | ✅ |
| `EmailResult` | `CBP.Emails/Core/Models/EmailResult.cs` | ✅ |
| `EmailMessage` | `CBP.Emails/Core/Models/EmailMessage.cs` | ✅ |

### 8.2 Capacidades Soportadas

| Feature | Soportado | Detalle |
|---------|-----------|---------|
| SMTP (MailKit) | ✅ | `SmtpClient` de MailKit |
| STARTTLS | ✅ | `SecureSocketOptions.StartTls` |
| SSL | ✅ | `SecureSocketOptions.SslOnConnect` |
| Auth user/pass | ✅ | `AuthenticateAsync` |
| OAuth/App Password | ✅ | Vía user/pass (Gmail App Passwords) |
| Tracking ID | ✅ | Header `X-Tracking-ID: trk-{Guid:N}` |
| Provider Selection | ✅ | RoundRobin / Priority / Random |
| Daily quota | ⚠️ | Configurable pero sin persistencia |
| Attachments | ✅ | `EmailMessage.Attachments` |
| Retry policy | ❌ | No implementado en CBP.Emails |
| Templating | ❌ | No es responsabilidad de CBP.Emails (PassPlat usa Fluid) |
| Bypass en PassPlat | ❌ | No hay — PassPlat siempre usa `CBP.Emails.Services.EmailService` |

---

## FASE 9 — Correcciones Requeridas

### 9.1 Prioridad P0 — Pipeline Roto

#### P0.1: Reescribir resolución SMTP desde EmailAccounts

**Archivos**: `PassPlatEmailService.cs`
**Descripción**: Reemplazar `ConfigAppRepository` por resolución desde `EmailAccounts` usando jerarquía App→Tenant→Global.
**Cambios**:
- Inyectar `IEmailAccountRepository`, `IAppEmailAccountRepository`, `ITenantEmailAccountRepository`
- Nuevo método `ResolveEmailAccountAsync(int? idApp, int? idTenant)`
- Jerarquía: `AppEmailAccount` (default) → `TenantEmailAccount` (default) → `EmailAccount` (global default)
- Desencriptar `EmailAccount.Password` con `_encryption.Decrypt(entity.Password, $"EmailAccount:{entity.Id}")`
- Construir `SmtpAccountConfig` desde `EmailAccount` properties

**Estimación**: 4-6 horas

#### P0.2: Persistir EmailLog en pipeline

**Archivos**: `EmailBackgroundService.cs`, `PassPlatEmailService.cs`
**Descripción**: Cada envío debe escribir en `EmailLog`:
- Antes: `EmailLog.Crear(Estado='pendiente', Destinatario, Asunto, IdTemplate, IdEmailAccount, IdTenant, IdUsuario, IdApp)`
- Éxito: `Estado='enviado'`, `FecEnvio=now`, `MsgIdExterno=result.TrackingId`, `Proveedor=result.ProviderUsed`
- Fallo: `Estado='fallido'`, `Intentos++`, `ErrorDetalle`, `FecUltIntento=now`

**Estimación**: 3-4 horas

#### P0.3: Implementar retry mechanism

**Archivos**: `EmailBackgroundService.cs`
**Descripción**:
- Reintentar hasta 3 veces con backoff exponencial (30s, 2min, 5min)
- Dead letter tras agotar reintentos (`Estado='fallido_permanente'`)
- NO perder job en excepción — capturar, loguear, actualizar EmailLog

**Estimación**: 2-3 horas

#### P0.4: Queue persistente

**Archivos**: `EmailBackgroundService.cs`, `EmailQueue.cs`
**Descripción**: Reemplazar `Channel<EmailJob>` por polling sobre `EmailLog WHERE Estado='pendiente'`:
- BackgroundService poll cada 5-15 segundos
- Batch size configurable (10-50)
- Procesar en orden FIFO por `FecCrea`
- **Eliminar `IEmailQueue` (in-memory)** o mantenerlo como fallback rápido + persistir en EmailLog inmediatamente

**Estimación**: 4-6 horas

### 9.2 Prioridad P1 — Funcional

#### P1.1: Resolver destinatario en NewDevice/NewIp/SecurityAlert

**Archivos**: `DispConfiableEventPublisher.cs`, `IPEventPublisher.cs`, `EmailBackgroundService.DispatchAsync`
**Descripción**: 
- `EmailJob` debe incluir `IdUsuario` (int?)
- `DispatchAsync` resuelve `ToEmail` desde `UsuarioRepository.ObtenerEmailPorIdAsync(IdUsuario)`
- Si no se resuelve, loguear warning y continuar (no fallar)

**Estimación**: 2-3 horas

#### P1.2: Propagar CorrelationId

**Archivos**: `EmailJob`, `EmailLog`, `EmailBackgroundService.cs`, `PassPlatEmailService.cs`
**Descripción**:
- `EmailJob` agrega `CorrelationId` (string)
- Eventos base ya tienen `CorrelationId` — propagar desde publisher→job
- `EmailLog` agrega columna `CorrelationId` (nvarchar(50)) o usar `MsgIdExterno`
- SMTP header `X-Correlation-ID`

**Estimación**: 2-3 horas

#### P1.3: DeviceRevoked → template + dispatch

**Archivos**: `EmailBackgroundService.DispatchAsync`
**Descripción**: Agregar case `DeviceRevoked` → `SendNotificationAsync("new-device" | nuevo template, ...)`

**Estimación**: 0.5 horas

### 9.3 Prioridad P2 — Mejora

#### P2.1: Integrar EmailProviders

**Archivos**: `PassPlatEmailService.cs`
**Descripción**: Usar `EmailAccount.IdProvider` para seleccionar estrategia SMTP (Gmail, SendGrid API, SES, etc.)

**Estimación**: 3-4 horas

#### P2.2: Paralelismo en BackgroundService

**Archivos**: `EmailBackgroundService.cs`
**Descripción**: Múltiples consumidores (3-5 workers) particionando por `EmailLog.Id % N`

**Estimación**: 2-3 horas

#### P2.3: Rate limiting persistente

**Archivos**: `PassPlatEmailService.cs`
**Descripción**: Tracking de DailySendLimit con persistencia (Redis o tabla)

**Estimación**: 3-4 horas

---

## FASE 10 — Entregable Final

### 10.1 Estado Real del Subsistema Email

| Componente | Estado | Evidencia |
|------------|--------|-----------|
| EmailProviders seed | ✅ | 5 providers activos en SEED_DATA.sql |
| EmailAccounts seed | ✅ | 1 cuenta activa (cbpnotificaciones@gmail.com) |
| TenantEmailAccounts | ✅ | Plataforma → SMTP Global |
| AppEmailAccounts | ✅ | PassPlat → SMTP Global |
| EmailTemplates | ✅ | 22 templates publicados |
| EmailTemplatePartials | ✅ | 2 partials activos |
| Pipeline Event→Queue | ⚠️ | Funciona para 18/22 eventos (4 con ToEmail vacío) |
| Queue→BackgroundService | ✅ | EmailBackgroundService ejecutándose |
| **Resolución SMTP** | ❌ | **Usa ConfigApp, NO EmailAccounts** |
| **EmailLogs** | ❌ | **0 registros — no se escribe nunca** |
| **Retry** | ❌ | Ninguno |
| **CorrelationId** | ❌ | No propagado |
| **Queue persistence** | ❌ | In-memory Channel |
| **CBP.Emails** | ✅ | Integración correcta |
| **Playwright cobertura email** | ❌ | 0 tests de email |
| **EmailAccounts UI** | ✅ | CRUD funcional vía API (ver sesión anterior) |

### 10.2 Score Subsistema Email

| Categoría | Peso | Score | Ponderado |
|-----------|------|-------|-----------|
| Modelo de datos | 15% | 90 | 13.5 |
| Seeds | 10% | 85 | 8.5 |
| Pipeline (event→queue) | 20% | 60 | 12.0 |
| Resolución SMTP | 20% | 10 | 2.0 |
| EmailLogs | 10% | 0 | 0.0 |
| Retry/Durabilidad | 10% | 0 | 0.0 |
| CBP.Emails integración | 10% | 90 | 9.0 |
| Testing (Playwright) | 5% | 0 | 0.0 |

**Score total**: **45/100**

### 10.3 Problemas Encontrados (10)

| ID | Problema | Severidad | Archivo(s) |
|----|----------|-----------|-----------|
| E1 | PassPlatEmailService usa ConfigApp en vez de EmailAccounts para SMTP | 🔴 Crítico | `PassPlatEmailService.cs:14,183-258` |
| E2 | EmailLog nunca se escribe durante pipeline | 🔴 Crítico | `EmailBackgroundService.cs` |
| E3 | Sin retry — fallo SMTP pierde el job | 🔴 Crítico | `EmailBackgroundService.cs:35-42` |
| E4 | Queue in-memory — restart pierde todos los jobs | 🔴 Crítico | `EmailQueue.cs` |
| E5 | NewDevice/NewIp/SecurityAlert con ToEmail vacío | 🟡 Alto | `DispConfiableEvents.cs`, `IPEvents.cs` |
| E6 | CorrelationId no propagado a EmailJob | 🟡 Alto | `EmailJob.cs`, `DispatchAsync` |
| E7 | Sin paralelismo en procesamiento | 🟡 Alto | `EmailBackgroundService.cs` |
| E8 | Sin dead letter para fallos permanentes | 🟡 Alto | `EmailBackgroundService.cs` |
| E9 | DeviceRevoked sin template ni dispatch | 🟡 Alto | `EmailBackgroundService.DispatchAsync` |
| E10 | Playwright sin tests de email | 🟢 Medio | `tests/*.spec.ts` |

### 10.4 Archivos Modificados en Sesiones Anteriores

| Archivo | Cambio |
|---------|--------|
| `PassPlat.Aplicacion.Dtos/Core/EmailAccountDto.cs` | +ActualizarEmailAccountDto |
| `PassPlat.Aplicacion/Mapping/AplicacionProfile.cs` | +Mapping ActualizarEmailAccountDto→EmailAccount |
| `PassPlat.Aplicacion/Services/BBDD/EmailAccountService.cs` | +IEncryptionService, +ActualizarAsync |
| `PassPlat.WebAPI/Controllers/EmailAccountsController.cs` | +PUT /api/emailaccounts/{id} |
| `PassPlat.Web/Pages/Email/Accounts.razor` | Rewrite: mock→API real con loading/empty states |
| `PassPlat.Web/Pages/Email/EmailAccountDialog.razor` | Rewrite: mock→API real + POST/PUT + providers |

### 10.5 Scripts SQL Requeridos

```sql
-- Index faltante para EmailTemplate.Estado
CREATE INDEX IX_EmailTpl_Estado ON dbo.EmailTemplates (Estado) WHERE Estado IS NOT NULL;

-- Index faltante para EmailTemplate.IdTenant
CREATE INDEX IX_EmailTpl_Tenant ON dbo.EmailTemplates (IdTenant) WHERE IdTenant IS NOT NULL;

-- Index descendente para EmailTemplateHistorial
CREATE INDEX IX_EmailTplHist_Template ON dbo.EmailTemplateHistorial (IdTemplate ASC, Version DESC);
```

### 10.6 Estimación Esfuerzo Correcciones

| Prioridad | Tarea | Horas |
|-----------|-------|-------|
| P0.1 | Resolución SMTP desde EmailAccounts | 6 |
| P0.2 | Persistir EmailLog | 4 |
| P0.3 | Retry mechanism | 3 |
| P0.4 | Queue persistente | 5 |
| P1.1 | Fix NewDevice/NewIp ToEmail | 3 |
| P1.2 | CorrelationId propagation | 2 |
| P1.3 | DeviceRevoked dispatch | 0.5 |
| P2.1 | EmailProviders integration | 3 |
| P2.2 | Paralelismo | 2 |
| P2.3 | Rate limiting persistente | 3 |
| **Total** | | **31.5 horas** |

### 10.7 Score Proyectado Tras Correcciones

| Componente | Actual | Post-fix |
|------------|--------|----------|
| Resolución SMTP | 10/100 | 90/100 |
| EmailLogs | 0/100 | 90/100 |
| Retry | 0/100 | 80/100 |
| Queue durabilidad | 0/100 | 85/100 |
| NewDevice/NewIp | 0/100 | 90/100 |
| CorrelationId | 0/100 | 80/100 |
| Playwright | 0/100 | 70/100 |

**Score proyectado**: **~85/100** (mejora de +40 puntos)

### 10.8 Conclusión

El subsistema Email de PassPlat tiene una **arquitectura sólida** (8 tablas con FKs correctas, filtered unique indexes, 22 templates, 5 providers, integración con CBP.Emails) pero el **pipeline operativo está roto en 3 puntos críticos**:

1. **Resolución SMTP** — usa `ConfigApp` (deprecated) en vez de `EmailAccounts`
2. **EmailLogs** — nunca se escriben (0 registros)
3. **Durabilidad** — sin retry, sin persistencia, sin dead letter

Además, **4 eventos de seguridad** (`NewDevice`, `DeviceRevoked`, `NewIp`, `SecurityAlert`) no pueden enviar correo porque llegan a la cola sin destinatario.

**Archivos clave para corrección**:
- `PassPlat.Aplicacion/Services/Email/PassPlatEmailService.cs` (resolución SMTP)
- `PassPlat.Aplicacion/Services/Email/EmailBackgroundService.cs` (retry + EmailLog + queue)
- `PassPlat.Aplicacion/Services/Email/EmailQueue.cs` (persistencia)
- `PassPlat.Aplicacion/Services/Security/DispConfiableEvents.cs` (ToEmail)
- `PassPlat.Aplicacion/Services/Security/IPEvents.cs` (ToEmail + IdUsuario)

**Score actual**: 45/100
**Score proyectado post-correcciones**: 85/100
**Esfuerzo estimado**: ~31.5 horas
