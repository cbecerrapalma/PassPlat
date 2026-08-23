# S15-Security-Logging-Audit.md — Security Logging (F7.2)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          Security-Audit + Logging-Audit
# Depende de      Security-Audit, Logging-Audit
# Influye en      Certification, Refactoring
# Area            Seguridad de logs / eventos sensibles (F7.2)
# Framework CBP   CBP.Security.Cryptography, CBP.Authentication.JwtBearer, CBP.Logging
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers
# Evidencia       AuthService.cs:332,370,432,467,504,557,607,667 · ExternalAuthService.cs:159-535 · GoogleIdentityProvider.cs · AuditoriaPwdService · BloqueoService.cs:136 · IntentoAccesoService.cs · 19 catch en SPro
# Resultado       FAIL (fuga ciphertext LOG-006 + excepciones silenciadas en MFA/notificaciones)
# Cobertura       45 % (ver F11)
# Riesgo          Critico
# Prioridad       Muy Alta

---

## 1. Proposito

Auditar el registro seguro de eventos relacionados con: Password, OAuth, JWT, MFA, RefreshToken, Login, Logout, Bloqueos, CambioContrasena y Token revocado. Verificar que NO se expongan secretos (password, tokens, hash, ciphertext) y que las excepciones de seguridad se registren sin ser tragadas.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Operaciones sensibles y su canal de registro

| Operacion | Log estructurado (ILogger) | Auditoria en BD (tabla) | Observacion |
|---|---|---|---|
| Login (password) | SI AuthService | SI (AuditoriaPwd, IntentoAcceso) | Trazado |
| Login OAuth | SI ExternalAuthService (EventIds) | SI (AudIdenExt + TraceId/SessionId/Jti) | Mejor instrumentado |
| Refresh token | SI AuthenticationTokenService:67 | (Sesion) | Reuse detection logueado |
| Logout | parcial | Sesion.revoke | Sin log estructurado consistente (auditar) |
| MFA enviar/validar | SI/Error | MfaStore | **silencia excepciones (enviar)** |
| Bloqueo | SI BloqueoService | Bloqueo | catch traga notificacion |
| Cambio password | SI PasswordService:170,283 | HistorialPwd, AuditoriaPwd | Trazado |
| Token reset | SI TokenRestService:81 | TokensRest | Trazado |
| JWT emit/validate | SI | Sesion/Jti | Trazado |

## 4. Hallazgos

### 4.1 Exposicion de secretos (CRITICO)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **SEC-001** | `ConfigAppService.cs:83` — `Console.WriteLine` **expone ciphertext** (40 chars) de config encriptada a consola/logs. Violacion de no-exponer-secretos. (Mismo CFG-001 / LOG-006). | `Services/BBDD/ConfigAppService.cs:83` | **FAIL - CRITICO** |
| **SEC-002** | `EnableSensitiveDataLogging()` + `LogTo(Console..., Information)` in dev: EF puede emitir SQL con parametros (incluyendo hashes/tokens) en texto. Dev-only pero no estructurado/redactado. | `Program.cs:133-138` | WARNING |
| **SEC-003** | OAuth `ClientSecret` descifrado: se loguea longitud y ok/fail pero `NO el valor` — correcto (NEGATIVE PASS). | `ExternalAuthService.cs:159-164` | PASS |
| **SEC-004** | Password hash / refresh token NUNCA se loguea como valor completo — patron correcto. | revision exhaustiva de {Hash}/{Refresh}/{Password} en mensajes = 0 | PASS |

### 4.2 Excepciones tragadas (PII / diagnostics perdidos)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **SEC-005** | `AuthService.EnviarCodigoMfaAsync` silencia excepciones internas (try-catch sin propagar a Result) — **blocker historico de email de codigo MFA** (FASE13/certificacion). Sin stack trace. | `AuthService.cs:332-340` (return null en catch) y metodo `EnviarCodigoMfaAsync` (L305-337) | **FAIL** |
| **SEC-006** | `BloqueoService.cs:136-146` catch de notificacion email traga error (solo LogError, no propaga). Pierde trazabilidad de entrega. | `SPro/BloqueoService.cs:135-146` | WARNING |
| **SEC-007** | `Get metodo MFA principal` devuelve null en catch (L330-335) — convierte error interno en "sin MFA", comporta como fallback silencioso (riesgo de bypass MFA falsa). | `AuthService.cs:328-336` | WARNING |
| **SEC-008** | 19 catch en servicios SPro; la mayoria hacen LogError + return (adecuado), pero los que `return null` (SEC-005 + SEC-007) degradan la seguridad. | grep catch SPro = 19 | WARNING |

### 4.3 Registro de eventos de seguridad correctos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **SEC-009** | Deteccion de reuso de refresh token SI logueada (warning) y revoca sesion. | `SessionManager.cs:47` | PASS |
| **SEC-010** | Auditoria OAuth persiste CorrelationId, TraceId, SessionId, Jti, IP, User-Agent (AudIdenExt). | `ExternalAuthService.cs:209-224,304` | PASS |
| **SEC-011** | AuditoriaPwd registra TipoAccion, Detalles, NivelRiesgo, Metadata en BD. | `AuditoriaPwdService` | PASS |
| **SEC-012** | `LoggingAuthorizationMiddlewareResultHandler` loguea fallos de autorizacion. | `WebAPI/Services/LoggingAuthorizationMiddlewareResultHandler.cs` | PASS |

### 4.4 Log despues del return / logs muertos

| ID | Hallazgo | Clasificacion |
|---|---|---|
| **SEC-013** | Fondos de controllers 63/64 sin ILogger: fallos auth/validation no logueados estructuradamente en la capa controller. | WARNING |
| **SEC-014** | Diagnostic*AuthMiddleware (2 middlewares) — verificamos si realmente loguean o son placeholders. Solo se registran; revisar cobertura en F12. | WARNING |

## 5. Clasificacion final por operacion sensible

| Operacion | Exposicion | Tragado | Auditoria persistida | Estado |
|---|---|---|---|---|
| Login password | OK | OK | SI | PASS |
| Login OAuth | OK | OK | SI (completa) | PASS |
| Refresh | OK | OK | SI | PASS |
| MFA enviar | OK | **SI (SEC-005)** | parcial | **FAIL** |
| Logout | a | a | SI | WARNING |
| Bloqueo notif | OK | **SI (SEC-006)** | SI | WARNING |
| Cambio pwd | OK | OK | SI | PASS |
| ConfigApp cifrado | **CIPHER EXPUESTO (SEC-001)** | — | — | **FAIL CRIT** |

## 6. Resultado F7.2
- **FAIL critico**: SEC-001 (fuga ciphertext), SEC-005 (silenciado MFA email/try-catch).
- **WARNING**: SEC-002, SEC-006, SEC-007, SEC-008, SEC-013.
- **Correcto**: OAuth y auditoria persistida robusta; no se loguean passwords/tokens crudos.

Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 6.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| SEC-001 | FAIL | EXTERMINAR (fuga ciphertext) | **Critica** | **P0** | Alta |
| SEC-002 | WARNING | JUSTIFICAR (dev-only, migrar a ILogger redactado) | Media | P2 | Alta |
| SEC-003 | PASS | REUTILIZAR (negativo correcto) | — | — | Alta |
| SEC-004 | PASS | REUTILIZAR (negativo correcto) | — | — | Alta |
| SEC-005 | FAIL | REEMPLAZAR (propagar error, no silenciar) | **Critica** | **P1** | Media |
| SEC-006 | WARNING | REEMPLAZAR (propagar notificacion) | Media | P2 | Alta |
| SEC-007 | WARNING | REEMPLAZAR (no fallback silencioso MFA) | **Alta** | **P1** | Alta |
| SEC-008 | WARNING | JUSTIFICAR (revisar return null) | Media | P2 | Media |
| SEC-009 | PASS | REUTILIZAR | — | — | Alta |
| SEC-010 | PASS | REUTILIZAR | — | — | Alta |
| SEC-011 | PASS | REUTILIZAR | — | — | Alta |
| SEC-012 | PASS | REUTILIZAR | — | — | Alta |
| SEC-013 | WARNING | EXTENDER (ILogger en controllers) | Media | P2 | Alta |
| SEC-014 | WARNING | JUSTIFICAR (verificar cobertura) | Baja | P3 | Media |

## 7. Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 45 % |
| Architecture Score | 54 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-SEC-001..014 |