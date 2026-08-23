# S15-Logging-Observability-Audit.md — Observability / Trazabilidad (F7.1)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          Logging-Audit
# Depende de      Logging-Audit
# Influye en      Certification, Refactoring
# Area            Observabilidad transversal (F7.1)
# Framework CBP   CBP.Logging (Serilog), CBP.WebApi (base)
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers
# Evidencia       Pipeline 14 middlewares · CorrelationIdMiddleware · LoggingScopeMiddleware · RequestLogging · Serilog Enrichers · 0 OpenTelemetry
# Resultado       WARNING (base de correlacion existe; cobertura de trazabilidad inconsistente)
# Cobertura       55 % (ver F11)
# Riesgo          Medio
# Prioridad       Alta

---

## 1. Proposito

Auditar la trazabilidad transversal de una operacion critica a traves de todo el pipeline (auth, authorization, data, events, cache, email, background, exceptions): CorrelationId / TraceId / ActivityId / RequestId, Tenant/App/Session/User context, structured logging, enrichers, OpenTelemetry, HealthChecks, Metrics y Exception middleware.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Pipeline de middleware (Program.cs, orden verificado)

```
UseCbpExceptionHandler()          (1)  <- exception -> ProblemDetails
UseMiddleware<CorrelationIdMiddleware>()      (2)  X-Correlation-ID + context.Items["CorrelationId"] + LogContext
UseMiddleware<RequestLoggingMiddleware>()     (3)  request/response logs
UseHsts / UseHttpsRedirection / UseStaticFiles
UseCors()
UseRateLimiter()                  (6)  Login/Refresh/Password/MFA/Token/Purge policies
UseMiddleware<TenantResolutionMiddleware>()   (7)  ResolvedTenantId -> context.Items
UseMiddleware<LoggingScopeMiddleware>()       (8)  PushProperty TenantId + UserId (Serilog)
UseCbpAuthentication()            (9)  CBP JWT auth
UseMiddleware<DiagnosticAuthMiddleware>()    (10)
UseAuthorization()                (11)
UseMiddleware<DiagnosticAfterAuthMiddleware>()(12)
```

## 4. Correlacion disponible (bien implementada)

| Identificador | Presente | Fuente | Consumido por |
|---|---|---|---|
| CorrelationId | SI | CorrelationIdMiddleware + LogContext PushProperty | ExternalAuth, IPService, DispConfiableService, EmailQueue/Log |
| RequestId / TraceIdentifier | SI | HttpContext.TraceIdentifier; fallback Activity.Current.Id / Guid in CorrelationIdMiddleware | ExternalAuthService (audit), OAuth BeginScope |
| ActivityId | (TraceIdentifier) | ASP.NET ActivityId | CorrelationIdMiddleware fallback |
| SessionId | SI | JWT/SP (SessionManager) | Auth loguera, auditoria OAuth |
| UserId | SI | LoggingScopeMiddleware (sub/nameidentifier) | LogContext push |
| TenantId | SI | LoggingScopeMiddleware (ResolvedTenantId) | LogContext push |
| AppId | NO | — | — |
| CorrelationId -> dados | SI (audit_autolog) | ExternalAuthService audit | IdenExt/Historial/EmailLog |

## 5. Hallazgos

### 5.1 Trazabilidad por operacion critica

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **OBS-001** | OAuth/Login: bien instrumentado en ExternalAuthService/AuthenticationTokenService (SessionId, Jti, TraceId, correlation). Trazabilidad completa. | `ExternalAuthService.cs:87-92,209-224`; `AuthenticationTokenService.cs:67,94` | PASS |
| **OBS-002** | Email: CorrelationId propagado de EmailJob a EmailLog y al background (`EmailQueue.cs:58`, `EmailBackgroundService.cs:144`). | Servicios Email | PASS |
| **OBS-003** | Dashboard: **DashboardEnterpriseService SIN ILogger** — metrica/lectura sin trazabilidad, no correlacionado. | `Services/Dashboard/DashboardEnterpriseService.cs` (0 ILogger) | WARNING |
| **OBS-004** | Controllers: solo 1/64 inyecta ILogger; errores de controller no quedan estructurados/correlacionados (van al middleware exception). | `Controllers/ExternalAuthController.cs:27` | WARNING |
| **OBS-005** | AppId no en el contexto de logs (solo TenantId/UserId). Para multi-app falta identificador de aplicacion en el scope. | LoggingScopeMiddleware (no AppId) | WARNING |
| **OBS-006** | Background/hosted: PasswordExpiration and SesionCleanup loguean sin CorrelationId de request (correcto—son desacoplados), pero sin identificador de ejecucion propio (JobId) fijo + correlation | `PasswordExpirationBackgroundService`, `SesionCleanupService` | WARNING |

### 5.2 Structured logging / enrichment

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **OBS-007** | Serilog Enrich configura: FromLogContext, WithMachineName, WithThreadId, WithProcessId (appsettings). No incluye OpenTelemetry trace/span ni request path. | `appsettings.json` CBP+Serilog.Enrich | WARNING (faltan enrichers de TraceId/SpanId) |
| **OBS-008** | `BeginScope` usado en OAuth (ExternalAuthService:87, GoogleIdentityProvider:48) — buen patron. Poco en el resto de servicios. | grep BeginScope | JUSTIFICAR |
| **OBS-009** | NO se usa OpenTelemetry (solo `AddHealthChecks`)/MapHealthChecks `/health`). Sin trazas OTLP, sin ActivitySource. | grep = solo HealthChecks Program.cs:214,249 | WARNING (falta OTel) |

### 5.3 Estructura de cambio / observabilidad de datos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **OBS-010** | HealthChecks basicos (`/health`) sin integracion a metricas ni a dashboard. | `Program.cs:214,249` | PASS (presente) |
| **OBS-011** | Metrics: **0** (no se usa `Meter`/`AddMetrics`). Sin telemetria de rendimiento. | grep Metrics = 0 | WARNING |
| **OBS-012** | Exception middleware: `UseCbpExceptionHandler` (CBP) convierte Result->ProblemDetails y loguea excepciones. Correcto; excepciones logueadas una vez. | `Program.cs:228` CBP.WebApi | PASS |

### 5.4 Concurrencia / correlacion de contexto en Background

| ID | Hallazgo | Clasificacion |
|---|---|---|
| **OBS-013** | `SessionManager` loguea suficiente (SessionId, UserId, TenantId, Jti). | PASS |
| **OBS-014** | `AuthenticationTokenService` loguea con contexto -> PASS | PASS |

## 6. Matriz de cobertura de observabilidad

| Operacion | CorrelationId | TenantId | UserId | SessionId | TraceId | ILogger | Clasificacion |
|---|---|---|---|---|---|---|---|
| Login (password) | parcial | SI | SI | SI | SI | SI | *** |
| OAuth login | SI | SI | SI | SI | SI | SI | PASS |
| Refresh | SI | SI | SI | SI | SI | SI | PASS |
| Logout | parcial | parcial | SI | SI | — | — | WARNING |
| MFA | — | SI | SI | — | — | ... | WARNING |
| Password reset | parcial | SI | SI | — | — | ... | WARNING |
| Dashboard | — | — | — | — | SS LANE | NO | FAIL |
| Email | SI (job) | SI | SI | — | SI | SI | PASS |
| Background maintenance | — | parcial | — | — | — | SI | WARNING |

## 7. Resultado F7.1
- **Bien**: correlacion base (CorrelationId/TenantId/UserId) implementada con middleware; OAuth + Email + AuthSesion bien trazados.
- **Faltantes**: OpenTelemetry (0), Metrics (0), Dashboard sin ILogger, AppId en contexto, enrichers TraceId/SpanId, controllers 63/64 sin ILogger.
- **Clasificacion**: OBS-011 (Metrics) FAIL, OBS-006/OBS-003/OBS-005 WARNING.

Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| OBS-001 | PASS | REUTILIZAR | — | — | Alta |
| OBS-002 | PASS | REUTILIZAR | — | — | Alta |
| OBS-003 | WARNING | EXTENDER (ILogger en Dashboard) | Alta | P1 | Alta |
| OBS-004 | WARNING | EXTENDER (ILogger en controllers) | Media | P2 | Alta |
| OBS-005 | WARNING | EXTENDER (AppId al scope) | Media | P2 | Alta |
| OBS-006 | WARNING | EXTENDER (JobId execut) | Baja | P3 | Alta |
| OBS-007 | WARNING | EXTENDER (enrichers TraceId) | Media | P2 | Alta |
| OBS-008 | PASS | REUTILIZAR | — | — | Media |
| OBS-009 | WARNING | REEMPLAZAR (adoptar OTel) | **Alta** | **P1** | Alta |
| OBS-010 | PASS | REUTILIZAR (HealthChecks presente) | — | — | Alta |
| OBS-011 | **FAIL** | REEMPLAZAR (Metrics ausente) | **Alta** | **P1** | Alta |
| OBS-012 | PASS | REUTILIZAR (Exception middleware) | — | — | Alta |
| OBS-013 | PASS | REUTILIZAR | — | — | Alta |
| OBS-014 | PASS | REUTILIZAR | — | — | Alta |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 55 % |
| Architecture Score | 47 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-OBS-001..014 |