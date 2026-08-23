# Auditoría del Subsistema Email — PassPlat

**Fecha**: 23-Jun-2026
**Versión**: V1
**Score estimado**: 45/100

---

## Resumen Ejecutivo

El subsistema Email de PassPlat tiene **8 tablas** en SQL Server con esquema correcto, **seeds completos** (5 providers, 1 cuenta SMTP, 22 templates, 2 partials), y un pipeline de envío que **funciona para casos con `ToEmail` explícito** (MFA, Reset Password, Welcome, etc.). Sin embargo, **3 gaps críticos invalidan la arquitectura actual**:

1. **Config SMTP en `ConfigApp` en lugar de `EmailAccounts`** — la resolución de cuenta SMTP ignora las tablas diseñadas para ello.
2. **`EmailLog` nunca se persiste** — no hay trazabilidad de envíos.
3. **Eventos sin `ToEmail` explícito** (`NewDevice`, `NewIp`, `SecurityAlert`) encolan jobs con destinatario vacío.

---

## FASE 1 — Análisis del Modelo de Datos

### Tablas del Subsistema Email (8)

| Tabla | PK | Columnas críticas | Estado |
|-------|----|--------------------|--------|
| `EmailProviders` | `IdProvider` (int) | `Codigo`, `Nombre`, `Activo` | ✅ Correcto |
| `EmailAccounts` | `IdEmailAccount` (int) | `IdProvider` (FK), `Email`, `Password` (encriptado), `SmtpHost`, `SmtpPort`, `UsaSsl`, `Prioridad`, `EsDefault`, `Activo` | ✅ Correcto |
| `TenantEmailAccounts` | `IdTenantEmailAccount` (int) | `IdTenant` (FK), `IdEmailAccount` (FK), `Prioridad`, `Activo` | ✅ Correcto |
| `AppEmailAccounts` | `IdAppEmailAccount` (int) | `IdApp` (FK), `IdEmailAccount` (FK), `Prioridad`, `Activo` | ✅ Correcto |
| `EmailTemplates` | `IdTemplate` (int) | `IdTenant` (FK nullable), `IdApp` (FK nullable), `Codigo` (unique), `Asunto`, `CuerpoHtml`, `EsGlobal`, `Publicado`, `Activo` | ✅ Correcto |
| `EmailTemplateHistorial` | `IdHistorial` (bigint) | `IdTemplate` (FK), `Version`, `CuerpoHtmlAnterior`, `FecCambio` | ✅ Correcto |
| `EmailTemplatePartials` | `IdPartial` (int) | `Codigo` (unique), `ContenidoHtml`, `Activo` | ✅ Correcto |
| `EmailLog` | `IdEmailLog` (bigint) | `IdEmailAccount` (FK nullable), `IdTemplate` (FK nullable), `Para`, `Asunto`, `Estado` (pendiente/enviado/fallido/rebotado), `Intentos`, `FecEnvio`, `FecUltIntento`, `ErrorDetalle`, `MsgIdExterno` | ✅ Correcto |

### Relationships

```
EmailAccounts.IdProvider → EmailProviders.IdProvider (FK)
TenantEmailAccounts.IdTenant → Tenants.IdTenant (FK)
TenantEmailAccounts.IdEmailAccount → EmailAccounts.IdEmailAccount (FK)
AppEmailAccounts.IdApp → Apps.IdApp (FK)
AppEmailAccounts.IdEmailAccount → EmailAccounts.IdEmailAccount (FK)
EmailTemplates.IdTenant → Tenants.IdTenant (FK, nullable)
EmailTemplates.IdApp → Apps.IdApp (FK, nullable)
EmailTemplateHistorial.IdTemplate → EmailTemplates.IdTemplate (FK)
EmailLog.IdEmailAccount → EmailAccounts.IdEmailAccount (FK, nullable)
EmailLog.IdTemplate → EmailTemplates.IdTemplate (FK, nullable)
```

### Filtered Unique Indexes

| Index | Filter | Propósito |
|-------|--------|-----------|
| `UX_EmailAccounts_Default` | `EsDefault = 1 AND Activo = 1` | Una sola cuenta default activa |
| `UX_EmailAccounts_Prioridad_Global` | `Activo = 1 AND EsDefault = 0` | Prioridad única entre cuentas no-default |
| `UX_TenantEmailAccounts_Prioridad` | `Activo = 1` | Prioridad única por tenant |
| `UX_AppEmailAccounts_Prioridad` | `Activo = 1` | Prioridad única por app |
| `UX_EmailTemplates_Codigo` | `Activo = 1` | Código único por template activo |

---

## FASE 2 — Seeds

Extraídos de `SEED_DATA.sql` (líneas 814-1103).

### Providers (5)

| Id | Codigo | Nombre | Activo |
|----|--------|--------|--------|
| 1 | `smtp` | SMTP | ✅ |
| 2 | `sendgrid` | SendGrid | ✅ |
| 3 | `ses` | Amazon SES | ✅ |
| 4 | `graph` | Microsoft Graph | ✅ |
| 5 | `mailgun` | Mailgun | ✅ |

### Cuentas SMTP (1)

| Id | Provider | Email | Host | Puerto | SSL | Default | Activo |
|----|----------|-------|------|--------|-----|---------|--------|
| 1 | SMTP (1) | `cbpnotificaciones@gmail.com` | `smtp.gmail.com` | 587 | ✅ TLS | ✅ | ✅ |

### Templates (22)

| Id | Codigo | Asunto | Global | Publicado |
|----|--------|--------|--------|-----------|
| 1 | `welcome` | Bienvenido a {{AppName}} | ✅ | ✅ |
| 2 | `password-reset` | Restablecimiento de contraseña | ✅ | ✅ |
| 3 | `mfa-code` | Tu código de verificación | ✅ | ✅ |
| 4 | `password-changed` | Contraseña actualizada | ✅ | ✅ |
| 5 | `security-alert` | Alerta de seguridad | ✅ | ✅ |
| 6 | `account-locked` | Cuenta bloqueada | ✅ | ✅ |
| 7 | `account-unlocked` | Cuenta desbloqueada | ✅ | ✅ |
| 8 | `user-activated` | Cuenta activada | ✅ | ✅ |
| 9 | `user-deactivated` | Cuenta desactivada | ✅ | ✅ |
| 10 | `user-unblocked` | Cuenta desbloqueada | ✅ | ✅ |
| 11 | `password-expired` | Contraseña expirada | ✅ | ✅ |
| 12 | `first-login` | Primer inicio de sesión | ✅ | ✅ |
| 13 | `mfa-enabled` | MFA activado | ✅ | ✅ |
| 14 | `mfa-disabled` | MFA desactivado | ✅ | ✅ |
| 15 | `new-device` | Nuevo dispositivo detectado | ✅ | ✅ |
| 16 | `new-ip` | Nueva dirección IP detectada | ✅ | ✅ |
| 17 | `role-assigned` | Rol asignado | ✅ | ✅ |
| 18 | `role-removed` | Rol removido | ✅ | ✅ |
| 19 | `tenant-created` | Tenant creado | ✅ | ✅ |
| 20 | `tenant-suspended` | Tenant suspendido | ✅ | ✅ |
| 21 | `tenant-reactivated` | Tenant reactivado | ✅ | ✅ |
| 22 | `device-revoked` | Dispositivo revocado | ✅ | ✅ |

### Partials (2)

| Id | Codigo | Propósito |
|----|--------|-----------|
| 1 | `header` | Encabezado HTML corporativo |
| 2 | `footer` | Pie HTML corporativo |

### Asignaciones

| Tabla | Id | Id Cuenta | Prioridad |
|-------|----|-----------|-----------|
| `TenantEmailAccounts` | 1 | 1 (SMTP Global) | 10 |
| `AppEmailAccounts` | 1 | 1 (SMTP Global) | 10 |

---

## FASE 3 — Resolución de Cuenta SMTP

### **GAP CRÍTICO P0**

**Archivo**: `PassPlat.Aplicacion\Services\Email\PassPlatEmailService.cs`

**Comportamiento actual**:
- Lee configuración SMTP de la tabla `ConfigApp` usando claves:
  - `SmtpHost` → `smtp.gmail.com`
  - `SmtpPort` → `587`
  - `SmtpUser` → `cbpnotificaciones@gmail.com`
  - `SmtpPassword` → (sin encriptar)
- Usa `ConfigAppRepository` como dependencia

**Comportamiento esperado**:
- Resolver cuenta SMTP desde `EmailAccounts` con orden de prioridad:
  1. `AppEmailAccounts` (default por App)
  2. `TenantEmailAccounts` (default por Tenant)
  3. `EmailAccounts` (default global)
- Usar `EmailAccountRepository` + `AppEmailAccountRepository` + `TenantEmailAccountRepository`
- Desencriptar `EmailAccount.Password` con `IEncryptionService` (context: `"EmailAccount:{Id}"`)

**Consecuencias**:
- La UI funcional (`Accounts.razor`) permite CRUD sobre `EmailAccounts` reales, pero el envío usa otra fuente
- Las credenciales en `ConfigApp` no están encriptadas
- No hay soporte multi-cuenta ni failover entre proveedores

---

## FASE 4 — Pipeline de Envío

### Arquitectura Actual

```
Acción de negocio
    ↓ (EventPublisher.DispatchAsync)
IEmailQueue.EnqueueAsync(EmailJob)
    ↓
Channel<EmailJob> (in-memory, capacity 1024)
    ↓
EmailBackgroundService.ExecuteAsync (single worker)
    ↓
PassPlatEmailService.DispatchAsync(EmailJob)
    ↓ (GetOrCreateEmailServiceAsync)
ConfigAppRepository.GetAsync("SmtpHost|SmtpPort|SmtpUser|SmtpPassword")
    ↓
CBP.Emails.EmailService.SendEmailAsync(message)
    ↓
MailKit.SmtpClient.SendAsync
    ↓
SMTP (Gmail 587 TLS)
```

### Componentes

| Componente | Archivo | Responsabilidad | Estado |
|------------|---------|----------------|--------|
| `IEmailQueue` / `EmailQueue` | `Services\Email\EmailQueue.cs` | Cola in-memory via `Channel<EmailJob>` | ✅ Funcional |
| `EmailBackgroundService` | `Services\Email\EmailBackgroundService.cs` | Consume cola, llama a `IPassPlatEmailService` | ✅ Funcional |
| `PassPlatEmailService` | `Services\Email\PassPlatEmailService.cs` | Resuelve config SMTP, envía via CBP.Emails | ❌ ConfigApp |
| `CBP.Emails.EmailService` | `CBP.Emails\Services\EmailService.cs` | Wrapper sobre MailKit | ✅ Funcional |
| `CBP.Emails.SmtpProvider` | `CBP.Emails\Providers\SmtpProvider.cs` | Conexión SMTP via MailKit | ✅ Funcional |

### EmailJob

```csharp
public class EmailJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public EmailJobKind Kind { get; init; }
    public string ToEmail { get; init; } = "";
    public string UserName { get; init; } = "";
    public string? TenantName { get; init; }
    public string? AppName { get; init; }
    public Dictionary<string, string> Data { get; init; } = [];
}
```

### EmailJobKind (22 valores)

```csharp
Welcome, PasswordReset, PasswordChanged, MfaCode,
AccountLocked, AccountUnlocked, UserActivated, UserDeactivated,
UserUnblocked, PasswordExpired, FirstLogin, MfaEnabled, MfaDisabled,
NewDevice, NewIp, SecurityAlert, RoleAssigned, RoleRemoved,
TenantCreated, TenantSuspended, TenantReactivated, DeviceRevoked
```

### **GAP P0 — EmailLog nunca se escribe**

El método `DispatchAsync` en `PassPlatEmailService`:
1. Toma el `EmailJob`
2. Obtiene config SMTP
3. Crea `EmailMessage`
4. **NO crea registro en `EmailLog`**
5. Envía via CBP.Emails
6. **NO captura resultado ni actualiza `EmailLog`**

---

## FASE 5 — Email Logs

### Tabla `EmailLog`

```sql
CREATE TABLE [Email].[EmailLog] (
    IdEmailLog      bigint IDENTITY(1,1) NOT NULL,
    IdEmailAccount  int NULL,          -- FK → EmailAccounts
    IdTemplate      int NULL,          -- FK → EmailTemplates
    Para            nvarchar(255) NOT NULL,
    Asunto          nvarchar(500) NOT NULL,
    CuerpoHtml      nvarchar(max) NULL,
    Estado          varchar(20) NOT NULL,  -- pendiente|enviado|fallido|rebotado
    Intentos        int NOT NULL DEFAULT 0,
    FecEnvio        datetime2 NULL,
    FecUltIntento   datetime2 NULL,
    ErrorDetalle    nvarchar(max) NULL,
    MsgIdExterno    nvarchar(255) NULL,
    CorrelationId   nvarchar(50) NULL,
    FecCrea         datetime2 NOT NULL DEFAULT sysdatetime(),
    CONSTRAINT PK_EmailLog PRIMARY KEY (IdEmailLog)
);
```

### **GAP P0 — 0 registros en producción**

- El pipeline **nunca inserta** en `EmailLog`
- No hay trazabilidad de:
  - Cuándo se intentó enviar
  - Si el envío fue exitoso
  - Cuántos intentos se realizaron
  - Cuál fue el error exacto
- No hay `EmailQueue` persistente — la cola in-memory (`Channel<EmailJob>`) se pierde al reiniciar la app

### Lo que debería pasar

```
Antes de enviar:
  INSERT EmailLog (Estado='pendiente', Intentos=0, ...)

Después de enviar (éxito):
  UPDATE EmailLog SET Estado='enviado', FecEnvio=GETUTCDATE(), MsgIdExterno=...

Después de enviar (fallo):
  UPDATE EmailLog SET Estado='fallido', Intentos+=1, FecUltIntento=GETUTCDATE(), ErrorDetalle=...
```

---

## FASE 6 — Eventos de Negocio → Email

### Matriz Acción → Evento → Template → EmailJobKind

| Acción | Origen | Template | EmailJobKind | ToEmail | Estado |
|--------|--------|----------|--------------|---------|--------|
| Login MFA | `AuthService.EnviarCodigoMFAAsync` | `mfa-code` (Id=3) | `MfaCode` | ✅ Usuario.Email | ✅ |
| Reset Password | `TokenRestService.NotificarResetPasswordAsync` | `password-reset` (Id=2) | `PasswordReset` | ✅ Usuario.Email | ✅ |
| Cambio Password | `PasswordService.CambiarPasswordAsync` | `password-changed` (Id=7) | `PasswordChanged` | ✅ Usuario.Email | ✅ |
| Bloqueo Cuenta | `BloqueoService.CrearBloqueoAsync` | `account-locked` (Id=6) | `AccountLocked` | ✅ Usuario.Email | ✅ |
| Primer Login | `PasswordService` | `first-login` (Id=12) | `FirstLogin` | ✅ Usuario.Email | ✅ |
| MFA Activado | `MfaService` | `mfa-enabled` (Id=13) | `MfaEnabled` | ✅ Usuario.Email | ✅ |
| MFA Desactivado | `MfaService` | `mfa-disabled` (Id=14) | `MfaDisabled` | ✅ Usuario.Email | ✅ |
| Welcome | `UsuarioService.CrearAsync` | `welcome` (Id=1) | `Welcome` | ✅ Usuario.Email | ✅ |
| Cuenta Activada | `UsuarioService.ActivarAsync` | `user-activated` (Id=8) | `UserActivated` | ✅ Usuario.Email | ✅ |
| Cuenta Desactivada | `UsuarioService.DesactivarAsync` | `user-deactivated` (Id=9) | `UserDeactivated` | ✅ Usuario.Email | ✅ |
| Cuenta Desbloqueada | `UsuarioService.DesbloquearAsync` | `user-unblocked` (Id=10) | `UserUnblocked` | ✅ Usuario.Email | ✅ |
| Password Expirado | `PasswordExpirationBackgroundService` | `password-expired` (Id=11) | `PasswordExpired` | ✅ Usuario.Email | ✅ |
| Rol Asignado | `AccesoService` | `role-assigned` (Id=17) | `RoleAssigned` | ✅ Usuario.Email | ✅ |
| Rol Removido | `AccesoService` | `role-removed` (Id=18) | `RoleRemoved` | ✅ Usuario.Email | ✅ |
| Tenant Creado | `TenantService` | `tenant-created` (Id=19) | `TenantCreated` | ✅ Admin email | ✅ |
| Tenant Suspendido | `TenantService` | `tenant-suspended` (Id=20) | `TenantSuspended` | ✅ Admin email | ✅ |
| Tenant Reactivado | `TenantService` | `tenant-reactivated` (Id=21) | `TenantReactivated` | ✅ Admin email | ✅ |
| **Nuevo Dispositivo** | `DispConfiableService` | `new-device` (Id=15) | `NewDevice` | ❌ `ToEmail=""` | ❌ |
| **Nueva IP** | `IntentoAccesoService` | `new-ip` (Id=16) | `NewIp` | ❌ `ToEmail=""` | ❌ |
| **Alerta Seguridad** | `IntentoAccesoService` | `security-alert` (Id=5) | `SecurityAlert` | ❌ `ToEmail=""` | ❌ |
| Dispositivo Revocado | `DispConfiableService` | `device-revoked` (Id=22) | `DeviceRevoked` | ❌ `ToEmail=""` | ❌ |
| Cuenta Desbloqueada | `BloqueoService` | `account-unlocked` (Id=7) | `AccountUnlocked` | ❌ `ToEmail=""` | ❌ |

### **GAP P1 — Eventos sin destinatario**

Los eventos `NewDevice`, `NewIp`, `SecurityAlert`, `DeviceRevoked`, `AccountUnlocked` se encolan con `ToEmail=""` y `UserName=""`. El método `DispatchAsync` en `PassPlatEmailService` no tiene lógica de resolución de destinatario — el email **nunca llega a un usuario real**.

### Causa raíz

Los servicios (`DispConfiableService`, `IntentoAccesoService`, `BloqueoService`) crean el `EmailJob` sin pasar el email del usuario porque la información de destinatario no está disponible en el punto de dispatch.

### Solución Requerida

```
EmailJob debe incluir IdUsuario → DispatchAsync busca Usuario.Email
                                    → usa ese valor como ToEmail
```

---

## FASE 7 — Pipeline de Templates

### Renderizado de Templates

```
EmailJob con Kind + Data (Dictionary<string,string>)
    ↓
PassPlatEmailService.DispatchAsync
    ↓
Busca Template por Codigo (según kind)
    ↓ (solo carga — NO renderiza)
Obtiene Asunto + CuerpoHtml
    ↓
Sustitución manual: body.Replace("{{Key}}", value)
    ↓
EmailMessage con Asunto + Body
    ↓
Envía
```

### **GAP — No usa Fluid templating**

El sistema actual sustituye `{{Key}}` con `string.Replace`. Aunque `EmailTemplates` contiene `CuerpoHtml` con sintaxis `{{variable}}`, no hay un motor de templates real (Fluid, Scriban, Razor). El `PassPlatEmailService` itera sobre `Data` y hace reemplazos secuenciales.

### Problemas con `string.Replace`:

1. **Orden de reemplazo** — si un valor contiene `{{`, se corrompe
2. **Sin condicionales** — no se pueden tener bloques `{{#if}}`
3. **Sin loops** — no se pueden iterar colecciones
4. **Sin encoding** — los valores HTML deben escaparse
5. **Sin layouts** — `EmailTemplatesPartials` (header/footer) no se ensamblan

---

## FASE 8 — CBP.Emails

### Biblioteca de Envío

**Path**: `D:\CODIGOS\CBP\CBP.Emails\`

| Componente | Estado | Notas |
|------------|--------|-------|
| `EmailService` | ✅ | Wrapper sobre MailKit |
| `IEmailService` | ✅ | Interface |
| `EmailMessage` | ✅ | DTO con To, CC, BCC, Subject, Body, Attachments |
| `EmailAttachment` | ✅ | Soportado |
| `SmtpProvider` | ✅ | MailKit SmtpClient |
| `SendGridProvider` | ❌ No implementado | Provider existe en schema pero no en CBP.Emails |
| `SESProvider` | ❌ No implementado | Provider existe en schema pero no en CBP.Emails |
| `GraphProvider` | ❌ No implementado | Provider existe en schema pero no en CBP.Emails |
| `MailgunProvider` | ❌ No implementado | Provider existe en schema pero no en CBP.Emails |
| Retry policy | ❌ No implementado | Debe implementarse en PassPlat |
| Timeouts | ✅ | Configurable |

### Soporte SMTP

| Característica | Soporte |
|----------------|---------|
| STARTTLS | ✅ (vía SmtpClient.Connect options) |
| SSL | ✅ (vía puerto 465) |
| Auth PLAIN/LOGIN | ✅ |
| OAuth 2.0 | ❌ (solo user/pass) |
| App Passwords | ✅ (como user/pass normal) |
| Pooling de conexiones | ❌ Crea/cierra conexión por envío |
| Asynchronous | ✅ |

---

## FASE 9 — Retry & Resiliencia

### **GAP P0 — Sin retry**

El `EmailBackgroundService` actual:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var job = await _channel.Reader.ReadAsync(stoppingToken);
    try
    {
        await _service.DispatchAsync(job, stoppingToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending email {JobId}", job.Id);
        // ❌ Job se pierde — no se reintenta, no va a dead letter
    }
}
```

### Comportamiento actual vs esperado

| Aspecto | Actual | Esperado |
|---------|--------|----------|
| Intento fallido | Log + olvido | Re-queue con backoff |
| Máx intentos | Infinito (0) | 3-5 intentos |
| Dead letter | No existe | Tabla o flag `Estado=abandonado` |
| Queue durable | No (in-memory) | Polling sobre `EmailLog WHERE Estado='pendiente'` |
| Reintento tras restart | No (in-memory) | Sí (base de datos) |

### **GAP P1 — Sin CorrelationId**

El `CorrelationId` del evento original (`EventBase.CorrelationId`) no se propaga a:
- `EmailJob`
- `EmailLog.CorrelationId`
- Cabecera SMTP `X-Correlation-ID`

---

## FASE 10 — Gaps, Prioridades y Plan de Corrección

### Resumen de Gaps

| ID | Prioridad | Gap | Impacto | Archivo(s) |
|----|-----------|-----|---------|------------|
| G1 | **P0** | Config SMTP desde `ConfigApp` en lugar de `EmailAccounts` | Credenciales incorrectas, sin multi-cuenta, sin failover | `PassPlatEmailService.cs` |
| G2 | **P0** | `EmailLog` nunca se persiste | Sin trazabilidad, sin auditoría, sin queue durable | `PassPlatEmailService.cs` |
| G3 | **P0** | Sin retry ni queue durable | Pérdida de emails en fallo de red/SMTP | `EmailBackgroundService.cs` |
| G4 | **P1** | Eventos con `ToEmail=""` (NewDevice, NewIp, SecurityAlert, DeviceRevoked, AccountUnlocked) | Notificaciones de seguridad perdidas | `DispConfiableService.cs`, `IntentoAccesoService.cs`, `BloqueoService.cs` |
| G5 | **P1** | Sin CorrelationId propagation | Imposible correlacionar evento → email | `PassPlatEmailService.cs`, `EmailJob.cs` |
| G6 | **P2** | `string.Replace` en lugar de Fluid/Scriban | Sin condicionales, layouts, encoding | `PassPlatEmailService.cs` |
| G7 | **P2** | `EmailTemplatesPartials` (header/footer) no se renderizan | Sin branding corporativo en emails | `PassPlatEmailService.cs` |
| G8 | **P2** | Solo proveedor SMTP implementado en CBP.Emails | Sin soporte SendGrid/SES/Graph/Mailgun | `CBP.Emails` |
| G9 | **P3** | Worker single-threaded | Cuello de botella con alta carga | `EmailBackgroundService.cs` |
| G10 | **P3** | Sin connection pooling | Overhead por envío | `CBP.Emails.SmtpProvider` |

### Score por Fase

| Fase | Score | Estado |
|------|-------|--------|
| FASE 1 — Modelo de datos | 95/100 | ✅ Correcto (8 tablas, FKs, índices) |
| FASE 2 — Seeds | 90/100 | ✅ Completos (5 providers, 1 cuenta, 22 templates) |
| FASE 3 — Resolución cuenta SMTP | **10/100** | ❌ Usa ConfigApp, ignora EmailAccounts |
| FASE 4 — Pipeline envío | **40/100** | ❌ Sin EmailLog, sin queue durable, sin retry |
| FASE 5 — Email Logs | **0/100** | ❌ Tabla vacía — no se persiste nada |
| FASE 6 — Eventos → Email | **60/100** | ⚠️ 17/22 funcionan, 5/22 sin destinatario |
| FASE 7 — Templates | **30/100** | ❌ string.Replace, sin partials, sin layout |
| FASE 8 — CBP.Emails | **50/100** | ⚠️ Solo SMTP, sin retry, sin pool |
| FASE 9 — Retry & Resiliencia | **10/100** | ❌ Sin retry, sin dead letter, sin queue durable |
| **Global** | **45/100** | ❌ Arquitectura existe, pipeline roto en 3 puntos críticos |

### Plan de Corrección

#### P0 — Inmediato (debe hacerse antes de cualquier release)

| # | Tarea | Archivos | Esfuerzo |
|---|-------|----------|----------|
| 1 | Crear `EmailAccountResolverService` con prioridad App→Tenant→Global | Nuevo: `Services\Email\EmailAccountResolverService.cs` | 4h |
| 2 | Reescribir `PassPlatEmailService.GetOrCreateEmailServiceAsync` para usar resolver + desencriptar password | `PassPlatEmailService.cs` | 3h |
| 3 | Persistir `EmailLog` en cada envío (Pending→Processing→Sent/Failed) | `PassPlatEmailService.cs` | 4h |
| 4 | Implementar retry en `EmailBackgroundService` con backoff 1m/5m/15m + dead letter | `EmailBackgroundService.cs` | 3h |

#### P1 — Corto Plazo

| # | Tarea | Archivos | Esfuerzo |
|---|-------|----------|----------|
| 5 | Propagar `CorrelationId` a EmailJob → EmailLog → SMTP header | `EmailJob.cs`, `EventPublisher.cs` | 2h |
| 6 | Resolver destinatario en `DispatchAsync` (buscar Usuario.Email cuando ToEmail vacío) | `PassPlatEmailService.cs` | 2h |
| 7 | Reemplazar `Channel<EmailJob>` por polling sobre `EmailLog WHERE Estado='pendiente'` | `EmailBackgroundService.cs`, `EmailQueue.cs` | 6h |

#### P2 — Mediano Plazo

| # | Tarea | Archivos | Esfuerzo |
|---|-------|----------|----------|
| 8 | Integrar Fluid templating con soporte de partials (header/footer) | `PassPlatEmailService.cs` | 8h |
| 9 | Implementar SendGrid/SES/Graph/Mailgun providers en CBP.Emails | `CBP.Emails` | 16h |
| 10 | Connection pooling para SMTP | `CBP.Emails.SmtpProvider` | 4h |

#### P3 — Largo Plazo

| # | Tarea | Archivos | Esfuerzo |
|---|-------|----------|----------|
| 11 | Workers paralelos (PartitionedChannel o N instancias) | `EmailBackgroundService.cs` | 4h |
| 12 | Tests Playwright: verificar EmailLogs, AuditoriaPwd, IntentosAcceso, Notificaciones | Tests E2E | 8h |
| 13 | Dashboard de monitoreo de emails (tasa éxito/fallo, tiempos) | UI + API | 12h |

**Esfuerzo total estimado**: ~76 horas

---

## Diagrama de Estado Actual vs Deseado

### Actual

```
Evento → EmailJob (Channel in-memory)
                             ↓
                  EmailBackgroundService
                             ↓
                  PassPlatEmailService
                             ↓
                  ConfigApp (SmtpHost, SmtpPort, ...)
                             ↓
                  CBP.Emails → SMTP → (sin log, sin tracking)
```

### Deseado

```
Evento → EmailJob (con CorrelationId, IdUsuario)
                             ↓
                  EmailLog INSERT (Estado=pendiente)
                             ↓
                  EmailBackgroundService (retry-enabled)
                             ↓
                  EmailAccountResolver (App→Tenant→Global)
                             ↓
                  EmailAccounts (desencriptado)
                             ↓
                  EmailTemplates + Partials (Fluid render)
                             ↓
                  CBP.Emails → SMTP/SendGrid/SES/Graph
                             ↓
                  EmailLog UPDATE (Estado=enviado/fallido, MsgIdExterno)
                + EmailQueue DELETE (o marca como procesado)
```

---

## Conclusión

El subsistema Email de PassPlat tiene **una arquitectura de datos sólida** (8 tablas bien diseñadas con FKs, índices, y seeds completos) pero **el pipeline de envío está desacoplado de esta arquitectura**. Las correcciones P0 son requisito para cualquier release que necesite notificaciones por email confiables, auditables, y con trazabilidad.

**Score actual: 45/100**
**Score objetivo post-corrección P0: 70/100**
**Score objetivo post-corrección completa: 90/100**
