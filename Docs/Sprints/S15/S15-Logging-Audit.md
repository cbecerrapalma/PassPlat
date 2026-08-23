# S15-Logging-Audit.md — Logging Audit (F7)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Logging-Context, Logging-Observability, Certification
# Area            Logging - uso correcto del framework (F7)
# Framework CBP   CBP.Logging (Serilog), CBP.WebApi (LoggingAuthorizationMiddlewareResultHandler)
# Cobertura       Aplicacion | Infraestructura | WebApi | Workers
# Evidencia       75 inyecciones ILogger · 0 usos ILoggerService (CBP.Logging) · 169 llamadas de log · 4 middlewares · appsettings Serilog
# Resultado       FAIL (registrado ILoggerService CBP sin consumo; multi-pipeline logging; 1 fuga)
# Cobertura       52 % (ver F11)
# Riesgo          Alto (fuga ciphertext CFG-001 / LOG-001 + multi-pipeline no correlacionado)
# Prioridad       Muy Alta

---

## 1. Proposito

Auditar el uso correcto del framework de logging: quien registra, que registra, que nivel, que contexto, correlacion, y exposicion de datos sensibles. Este documento cubre el **uso correcto del framework**; la trazabilidad transversal y la seguridad de logs se cubren en F7.1 y F7.2 respectivamente.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Estado del pipeline de logging

### 3.1 Infraestructura (Program.cs WebAPI)

| Registro | Linea | Funcion |
|---|---|---|
| `AddCbpLogging(builder.Configuration)` | 32 | Registra CBP.Logging (Serilog) + ILoggerService |
| `AddSingleton(Log.Logger)` | 38 | Expone Serilog global como singleton |
| `UseSerilogRequestLogging` (en pipeline) | (middleware) | Request logging |
| 4 middlewares custom | CorrelationId · LoggingScope · RequestLogging · Diagnostic*Auth | Contexto/correlacion |

### 3.2 Middlewares de contexto (evidencia F7.1 en parte)

| Middleware | Proporciona | Archivo |
|---|---|---|
| `CorrelationIdMiddleware` | X-Correlation-ID (request+response), `context.Items["CorrelationId"]`, PushProperty `CorrelationId` via Serilog LogContext | `WebAPI/Middleware/CorrelationIdMiddleware.cs` |
| `LoggingScopeMiddleware` | PushProperty `TenantId` + `UserId` (sub) | `WebAPI/Middleware/LoggingScopeMiddleware.cs` |
| `RequestLoggingMiddleware` | Log de request/response | `WebAPI/Middleware/RequestLoggingMiddleware.cs` |
| `DiagnosticAuthMiddleware` / `DiagnosticAfterAuthMiddleware` | Logs de auth (diagnostico) | `WebAPI/Middleware/` |

## 4. Consumo (conteo verificado)

- **75** inyecciones de `ILogger<T>` en servicios/repos/controllers/middlewares.
- **0** inyecciones de `ILoggerService` (CBP.Logging). El unico `using CBP.Logging.DependencyInjection` esta en Program.cs (para AddCbpLogging). **Scaffolding muerto** (DI-005 / LOG-001).
- Llamadas por nivel: Information 70 · Warning 49 · Error 46 · Critical 1 · Debug 3 · Trace 0.

## 5. Hallazgos

### 5.1 Uso del framework

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **LOG-001** | `CBP.Logging` registrado (`AddCbpLogging`) con `ILoggerService` disponible pero **no se consume en ningun servicio**; todo el logging real usa `ILogger<T>` de Microsoft.Extensions + Serilog directo. Duplica lo que CBP.Logging ya abstrae. | `Program.cs:32` · grep `ILoggerService` en PassPlat = 0 usos (solo using) | **FAIL** |
| **LOG-002** | **Multi-pipeline de logging**: `Log.Logger` singleton + `AddCbpLogging` (crea su propia pipeline Serilog) + `AddSingleton(ILogger)` + EF `LogTo(Console...)`. Sin un contrato unico. Riesgo de logs duplicados o perdidos segun sink. | `Program.cs:34-38, 133-138` · appsettings (Serilog + Cbp:Logging + Logging) | WARNING |
| **LOG-003** | Solo **1/64** controllers inyecta `ILogger` (ExternalAuthController). La mayoria de errores de controller se pierden sin contexto estructurado (ver F7.2). | `WebAPI/Controllers/ExternalAuthController.cs:27` | WARNING |
| **LOG-004** | `LogCritical` solo 1 uso en toda la base de codigo; excepciones no esperadas se tratan como Error/Warning sin escalar. | grep `LogCritical` = 1 | WARNING |
| **LOG-005** | `LogDebug` solo 3 usos; casi todo el detalle de flujo OAuth/login va a `LogInformation` (ruido, no trazable por nivel). | grep `LogDebug` = 3 | WARNING |

### 5.2 Mapa de logging por servicio (resumen por area)

| Area | Servicio/Clase | Nivel dominante | CBP usado | Notas |
|---|---|---|---|---|
| Auth | AuthService | Info/Error | Serilog directo | OAuthEventIds parcial |
| OAuth | ExternalAuthService, GoogleIdentityProvider | Info/Error | Serilog + BeginScope | Mejor instrumentado (F17) |
| OAuth | IdenExtTokensRotacionJob | Info | Serilog | Background |
| Email | PassPlatEmailService, EmailBackgroundService | Info/Error | Serilog | CorrelationId en jobs |
| Security | PasswordExpirationBackgroundService, SesionCleanupService | Info | Serilog | Background |
| Dashboard | DashboardEnterpriseService | **SIN ILogger** | — | Area sin logging (auditar) |
| Config | ConfigAppService | **Console.WriteLine** (fuga) | — | CFG-001 |
| Datos | SesionRepository | Info | Serilog | Unico repo con ILogger |
| Cache | JwksStore, MfaCodeStore | Info/Error | Serilog | — |
| WebAPI | Controllers (63/64) | **SIN ILogger** | — | Solo ExternalAuthController |

### 5.3 Exposicion de datos sensibles en logs (detalle en F7.2)

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **LOG-006** | `ConfigAppService.cs:83` — `Console.WriteLine` con **prefix del ciphertext** (40 chars) de valores encriptados. CRITICO (mismo que CFG-001). | `Services/BBDD/ConfigAppService.cs:83` | **FAIL - CRITICO** |
| **LOG-007** | `EnableSensitiveDataLogging()` + `LogTo(Console.WriteLine, Information)` en dev — SQL con parametros potencialmente sensibles a consola. Solo dev, pero es `Console` no ILogger. | `Program.cs:133-138` | WARNING |

### 5.4 Correlacion (resumen — detalle F7.1)
Ya existe base: CorrelationIdMiddleware, LoggingScopeMiddleware (TenantId, UserId), Serilog LogContext, `context.Items["CorrelationId"]`, Activity/TraceIdentifier. Bien implementado pero **inconsistente**: solo OAuth/IP/DispConfiable/Email consumen el CorrelationId; el resto no propaga.

## 6. Resultado F7
- **FAIL**: LOG-001 (ILoggerService CBP sin consumo), LOG-006 (fuga cipher).
- **WARNING**: LOG-002 multi-pipeline, LOG-003 controllers sin logger, LOG-004/005 niveles mal distribuidos.
- **Bien**: Middlewares de correlacion/scope ya existen; OAuth bien instrumentado con EventIds.
- **Contexto**: base CorrelationId+TenantId+UserId presente; faltan AppId, TraceId, SessionId, EventId, ExceptionId — ver `S15-Logging-Context-Audit.md` (TD-CTX-001..004).

Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 6.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| LOG-001 | FAIL | EXTENDER/REEMPLAZAR (consumir o eliminar ILoggerService CBP) | Media | P1 | Alta |
| LOG-002 | WARNING | REEMPLAZAR (unificar pipeline log) | Media | P2 | Alta |
| LOG-003 | WARNING | EXTENDER (ILogger en controllers) | Media | P2 | Alta |
| LOG-004 | WARNING | EXTENDER (LogCritical en no esperado) | Baja | P3 | Alta |
| LOG-005 | WARNING | REEMPLAZAR (niveles por importancia) | Baja | P3 | Alta |
| LOG-006 | FAIL | **EXTERMINAR (fuga cipher)** | **Critica** | **P0** | Alta |
| LOG-007 | WARNING | JUSTIFICAR (solo dev, migrar a ILogger) | Media | P2 | Alta |
| CTX-001 | WARNING | EXTENDER (AppId al scope) | Media | P2 | Alta |
| CTX-002 | WARNING | EXTENDER (TraceId/SpanId OTel) | Media | P2 | Alta |
| CTX-003 | WARNING | EXTENDER (SessionId/Jti global) | Baja | P3 | Media |
| CTX-004 | WARNING | EXTENDER (ExceptionId) | Baja | P3 | Media |

## 7. Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 52 % |
| Architecture Score | 60 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-LOG-001..007 + TD-CTX-001..004 |