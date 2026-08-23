# S15-Logging-Context-Audit.md — Logging Context / Campos de Auditoría (F7 ampliado — documento compañero)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Logging-Audit
# Depende de      Logging-Audit, Logging-Observability
# Influye en      Refactoring, Certification
# Área            Contexto de logging: quién, qué, cuándó, dónde, por qué + contexto (F7.1-contexto)
# Framework CBP   CBP.Logging (Serilog LogContext), Middlewares CorrelationId/LoggingScope
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers
# Evidencia       CorrelationIdMiddleware.cs · LoggingScopeMiddleware.cs · RequestLoggingMiddleware.cs · DiagnosticAuthMiddleware.cs · AuthService.cs · ExternalAuthService.cs · EmailBackgroundService.cs
# Resultado       WARNING (base CorrelationId+TenantId+UserId presente; faltan AppId/SessionId/OAuthId/TraceId/EventId consistentes)
# Cobertura       55 % de los 10 campos
# Riesgo          Medio
# Prioridad       Media

---

## 1. Proposito (documento compañero — no sustituye F7)

Complementar `S15-Logging-Audit.md` con el análisis de **contexto de cada log**: los campos que debe portar todo registro y las 5W. Cada log debe responder: ¿Quién? ¿Qué? ¿Cuándo? ¿Dónde? ¿Por qué? + contexto (Tenant, App, Usuario, CorrelationId). Cualquier campo ausente es deuda técnica.

## 2. Metodo (estructura obligatoria por hallazgo)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Matriz de campos de contexto — estado real verificado

| # | Campo de contexto | Canal | Real en LIGH | State | Confidence |
|---|---|---|---|---|---|
| 1 | CorrelationId | `X-Correlation-ID` header + `context.Items["CorrelationId"]` + `LogContext.PushProperty("CorrelationId")` | ✅ Global (request) | PASS | Alta |
| 2 | RequestId | `TraceIdentifier` (ASP.NET), fallback `Activity.Current.Id`/Guid en CorrelationIdMiddleware | ✅ request | PASS | Alta |
| 3 | TraceId | NO se enriquece (falta enricher TraceId/SpanId de OpenTelemetry) | ❌ NO | WARNING | Alta |
| 4 | UserId | `LogContext.PushProperty("UserId")` con `sub`/`nameidentifier` (LoggingScopeMiddleware) | ✅ autenticado | PASS | Alta |
| 5 | TenantId | `LogContext.PushProperty("TenantId")` desde `ResolvedTenantId` (LoggingScopeMiddleware) | ✅ | PASS | Alta |
| 6 | **AppId** | NO en ámbito de logs (solo TenantId/UserId en el scope) | ❌ NO | WARNING | Alta |
| 7 | **SessionId / Jti** | Presente en OAuth log (SessionId/Jti), NO globalizado | ⚠️ parcial (solo OAuth) | WARNING | Media |
| 8 | **OAuthId / Provider** | En OAuth EventIds/BeginScope | ⚠️ parcial | WARNING | Media |
| 9 | **EventId** | Se usan `OAuthEventIds` en OAuth; resto usa numeric literal o nada | ⚠️ parcial | WARNING | Media |
| 10 | **ExceptionId** | No se genera ID de excepcion por error | ❌ NO | WARNING | Alta |

## 4. Cobertura de conversacion (que proceso registra que contexto)

| Proceso | CorrelationId | TenantId | UserId | AppId | SessionId | EventId | Confidence |
|---|---|---|---|---|---|---|---|
| Login (password) | parcial | ✅ | ✅ | ❌ | ⚠️ | ❌ | Media |
| Login OAuth | ✅ | ✅ | ✅ | ❌ | ✅ (Jti) | ✅ (OAuthEventIds) | Alta |
| Refresh | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | Media |
| MFA | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ | Media |
| Cambio password | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | Media |
| Email background | ✅ (job) | ✅ | ✅ | ❌ | ❌ | ❌ | Alta |

## 5. Hallazgos del contexto de logging

### 5.1 Gaps estructurales

| ID | Hallazgo | Evidencia | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| **CTX-001** | **AppId no cubierta en el ámbito de logging** — el LoggingScopeMiddleware pushea solo TenantId + Userid; no hay AppId en LogContext. Para multi-app (IdApp) falta contexto de aplicacion en cada log. | `LoggingScopeMiddleware.cs:24-25` | WARNING | REEMPLAZAR/EXTENDER (agregar AppId al scope) | Alta |
| **CTX-002** | **TraceId / SpanId no se enriquecen** — no OpenTelemetry; no hay TraceId/SpanId en los logs pese a tener Activity. | grep enrichers OTel = 0 | WARNING | EXTENDER (cache OTel o enricher) | Alta |
| **CTX-003** | **SessionId/Jti inconsistentes** — solo OAuth loguea; en login password/refresh no se propaga SessionId al ámbito de logs. | `ExternalAuthService.cs` (Session) · AuthService (sin Session en LogContext) | WARNING | EXTENDER (SessionId en scope) | Media |
| **CTX-004** | **ExceptionId** no se asigna — cada excepcion logueada sin correlacion con los logs posteriores del mismo fallo. Sin `AppFailId`/`CorrelationId` en el exception middleware. | `UseCbpExceptionHandler` (sin ExceptionId) | WARNING | EXTENDER (genarse ExceptionId) | Media |

### 5.2 Las 5W — cobertura por caso parte del pipeline

| Proceso | Quién | Qué | Cuándo | Dónde | Por qué | Evaluacion |
|---|---|---|---|---|---|---|
| Login fallido | controller | error | timestamp | modulo | cred invalida | PASS (excepto AppId) |
| Error de repositorio | repo | ex.Details | ts | Datos | catch | PASS |
| Background email | EmailBackgroundService | retry | ts | cola | SMTP | PASS |
| Dashboard | **NA** | si off | ts | Dashboard | no logger | FAIL (OBS-003) |

## 6. Resultado (contexto)

- **Bien**: CorrelationId, RequestId, UserId, TenantId bien establecidos vía middleware; Serilog LogContext funciona.
- **Faltan**: AppId (multi-app), TraceId/SpanId (OTel), SessionId/Jti global, EventId consistente, ExceptionId. → cada uno = deuda técnica (CTX-001..004).
- Cobertura objetable media 5/10 campos; mejorable.

| Métrica | Valor |
|---|---|
| Cobertura CBP | 60 % (CorrelationId middleware = zoncl) |
| Architecture Score | 66 / 100 |
| Confidence | Alta |
| Technical Debt | TD-CTX-001..024 |

- Enlace de ADR: ADR-004 (usar CBP.Logging con enrichers de contexto) — registrada en S15-Architecture-Decisions.md.

**Insumo F12**: agregar AppId/SessionId al LoggingScopeMiddleware; incorporar enrichers TraceId/SpanId; asignar ExceptionId en UseCbpExceptionHandler; normalizar EventIds a clase dedicada ciúmeros. Este cambio actualiza F7.1 (Observability) y F7.2 (Security) por consistencia de campos.