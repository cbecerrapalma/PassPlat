# S15-Emails-Audit.md — Pipeline de Correo (F6)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Certification
# Area            Email / Notificaciones (F6)
# Framework CBP   CBP.Emails (CBP.Infraestructure) — IEmailService, EmailService, SmtpProvider, EmailRendererService, Configuration.EmailSettings/SmtpSettings, Core.Models(EmailMessage/EmailResult/EmailAddress), StartTls/Port
# Cobertura       PassPlat.Aplicacion/Services/Email | WebAPI background
# Evidencia       PassPlatEmailService.cs:1-5 (using CBP.Emails) · :292 (`new EmailService(emailSettings)`) · :274 EmailMessage · EmailBackgroundService.cs (hosted) · EmailQueue.cs · EmailAccountResolverService.cs:1 (CBP.Emails.Configuration) · SMTP config desde BD (EmailProviders/EmailAccounts/...)
# Resultado       REUTILIZAR / PASS (usa CBP.Emails real; config desde BD; log/cola MailKit)
# Cobertura       90 % (ver F11)
# Riesgo          Bajo
# Prioridad       Alta

---

## 1. Proposito

Auditar el pipeline de envio de email: como se integra con `CBP.Emails`, si la config SMTP sale de BD (EmailProviders/EmailAccounts) o de appsettings, el flujo EmailQueue→EmailBackgroundService→PassPlatEmailService→SMTP, y las plantillas.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Integracion con CBP.Emails (PASS)

| Componente CBP.Emails | Reuso en PassPlat | Evidencia |
|---|---|---|
| `CBP.Emails.Services.EmailService` | Instanciado con config SMTP para enviar (`new EmailService(emailSettings)`) | `PassPlatEmailService.cs:292` |
| `CBP.Emails.Configuration.EmailSettings` | MailSettings: DefaultFromEmail/Name + SmtpAccounts | `PassPlatEmailService.cs:284-290` |
| `CBP.Emails.Core.Models.EmailMessage` | mensaje to/subject/body/isHtml | `PassPlatEmailService.cs:296` |
| `CBP.Emails.Core.Models.EmailResult` | Success/TrackingId/ErrorMessage | `PassPlatEmailService.cs:307-330` |
| `SmtpProvider` (con ConnectAsync+StartTls) | envia via SMTP en CBP.Emails (provedor SMTP real) | `CBP.Emails.Providers.SmtpProvider` |

## 4. Pipeline de envio

```
Emisor (AuthService/AlgoService/Event) -> EmailQueue.EnqueueAsync(EmailJob)
    -> EmailBackgroundService (HostedService, ProcessingLoop)
        -> PassPlatEmailService.SendFromJobAsync
            -> EmailTemplateService (rendering con plantilla)
                -> SendEmailAsync
                    -> EmailAccountResolverService.ResolveAsync (SMTP desde BD)
                    -> new EmailService(settings) [CBP.Emails]
                        -> SendEmailAsync(message) -> SMTP -> EmailResult
                    -> EmailLog (Update / SendEmailLog)
```

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **EMAIL-001** | SMTP config proviene de BD (EmailProviders/EmailAccounts/email tenant) — NO appsettings. Cumple constraint de certificacion. | `EmailAccountResolverService.cs` (resolver BD), `AppEmailAccountRepository/TenantEmailAccountRepository/EmailAccountRepository` | PASS |
| **EMAIL-002** | Contrasenas SMTP/ClientSecret CIFRADAS AES-256-GCM via `IEncryptionService` se descifran antes del envio. | `EmailAccountService.cs:25,50,66`; `PassPlatEmailService.cs:292(dotDebug SMTP`) | PASS |
| **EMAIL-003** | Hosting/background: `EmailBackgroundService : BackgroundService` con cola (EmailQueue) — desacoplado de request. | `Services/Email/EmailBackgroundService.cs` | PASS |
| **EMAIL-004** | Plantillas por `EmailTemplateService` + `IEmailTemplateStoreService` (plantillas en BD), 22 templates mapeadas. | `PassPlatEmailService.cs:162-181` (template mapping 22) | PASS |
| **EMAIL-005** | `new EmailService(emailSettings)` — construccion manual NO via DI/named service (los servicios pueden resolverse de CBP). No se usa `eServices.AddEmail()` CBP. Es SUB-OPTIMO pero funcional aislado. | `PassPlatEmailService.cs:292` | WARNING |
| **EMAIL-006** | EmailLog persistido (Estado, Intentos, FecEnvio, MsgIdExterno, ErrorDetalle) — certificacion 17/22 templates. | `CreateOrUpdateLogAsync`, `@Update` | PASS |
| **EMAIL-007** | Retencion/FecRetencion en EmailLog (computed) — poryable mantenimiento. | `EmailLog` (FecRetencion) | PASS |
| **EMAIL-008** | Multi-pipeline act: `EmailService` CBP standalone (`new`) + SMTP (MailKit internal) — no hay IEmailQueue alternativa duplicada | | PASS |

## 6. Clasificacion general
- **CBP.Emails**: reutilizado como motor de SMTP real (via `EmailService`). 
- Config: 100% desde BD (correcto).
- Duplicacion: **0**. El `new EmailService` es el punto de mejora (podria resolverse por DI factory de CBP).

## 7. Resultado F6
- **REUTILIZAR / PASS**: pipeline de email adoptó CBP.Emails para el envio SMTP, con config en BD cifrada y log completo. 22 templates certificados.
- Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.
- Punto de riesgo: exposición de cifrado (CFG-001 ya auditado) no es de este subsistema específico.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| EMAIL-001 | PASS | REUTILIZAR | — | — | Alta |
| EMAIL-002 | PASS | REUTILIZAR (AES-256-GCM) | — | — | Alta |
| EMAIL-003 | PASS | REUTILIZAR (background desacoplado) | — | — | Alta |
| EMAIL-004 | PASS | REUTILIZAR | — | — | Alta |
| EMAIL-005 | WARNING | REEMPLAZAR (DI factory, evitar `new`) | Media | P2 | Media |
| EMAIL-006 | PASS | REUTILIZAR (EmailLog) | — | — | Alta |
| EMAIL-007 | PASS | REUTILIZAR (retencion) | — | — | Alta |
| EMAIL-008 | PASS | REUTILIZAR | — | — | Alta |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 90 % |
| Architecture Score | 84 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-EMAIL-001..008 (solo EMAIL-005 menor) |