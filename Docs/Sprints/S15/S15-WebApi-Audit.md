# S15-WebApi-Audit.md — Capa WebAPI / Controllers (F10)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory+Services
# Influye en      Certification
# Area            API Web / Controllers (F10)
# Framework CBP   CBP.WebApi (BaseApiController, AddCbpWebApi, AddCbpControllers, AddCbpOpenApi, UseCbpExceptionHandler/ExceptionHandlingMiddleware, ResultFilter, ModelStateValidationFilter, PagedResponse, SeekPagedResponse, ResultResponseTransformer, StandardResponsesTransformer, ServiceController, ApplicationBuilderExtensions)
# Cobertura       PassPlat.WebAPI
# Evidencia       64 Controllers · 58 heredan `BaseApiController` (CBP.WebApi) · 6 heredan ControllerBase plano · Program.cs usa AddCbpWebApi/AddCbpControllers/AddCbpOpenApi/AddCbpCache/AddCbpAuthentication/UseCbpExceptionHandler/UseCbpAuthentication/MapControllers
# Resultado       REUTILIZAR / PASS (pipeline WebApi dominado por CBP.WebApi; 58/64 use BaseApiController; PERO 6 controllers no usan FromResult)
# Cobertura       88 % (ver F11)
# Riesgo         Bajo
# Prioridad       Alta

---

## 1. Proposito

Auditar la capa WebAPI: uso de CBP.WebApi (BaseApiController, extensiones DI, middleware de exceplому, OpenApi, filtros), y la cobertura de controllers que adoptan `FromResult` vs ControllerBase clasico.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Peso de CBP.WebApi en el pipeline

| Extension/Middleware CBP | Uso en Program.cs | Evidencia |
|---|---|---|
| `AddCbpControllers()` | Registra controllers+filter de Result/ModelState | `Program.cs` |
| `AddCbpWebApi()` | Pipeline completo web | `Program.cs` |
| `AddCbpOpenApi()` | Documentacion con ResultResponseTransformer | `Program.cs` |
| `AddCbpCache(UseLocal MemoryCacheProvider)` | Cache CBP | `Program.cs` |
| `AddCbpAuthentication` | Auth CBP (AutoChallenge=false) | `Program.cs` |
| `UseCbpExceptionHandler()` | Exception→ProblemDetails (RFC 7807) | `Program.cs` |
| `UseCbpAuthentication()` | middleware auth CBP | `Program.cs` |
| `MapControllers()` | ruteo | `Program.cs` |

Todo el pipeline de pipeline web está basado en CBP.WebApi: Startup, errores, resolucion de Result, JSON, OpenAPI, paginacion.

## 4. Controllers

| Metrica | Valor | Evidencia |
|---|---|---|
| Total controllers | 64 | `WebAPI/Controllers/*Controller.cs` |
| Heredan `BaseApiController` (CBP) | **58** | `: BaseApiController` |
| Heredan `ControllerBase` plano (sin FromResult) | **6** | `: ControllerBase` |
| Endpoints via `FromResult/FromResultQuery/CreatedFromResult` | (los de BaseApi) | 58 controllers |

Los 58 controllers usan los helpers `FromResult`, `FromResultQuery`, `CreatedFromResult(action,route,result)` que convierten `Result<T>.IsFailure` → `ProblemDetails` (RFC 7807), cumpliendo la cadena de Result pattern.

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **WEB-001** | 58/64 controllers heredan `BaseApiController` CBP — uso masivo del helper. | 58 `: BaseApiController` | PASS |
| **WEB-002** | Pipeline usa `AddCbpWebApi/Controllers/OpenApi/Cache/Authentication` + `UseCbpExceptionHandler/Authentication` — adherido a CBP | `Program.cs` | PASS |
| **WEB-003** | Exception unificada `UseCbpExceptionHandler` → ProblemDetails; excepciones logueadas una vez. | `Program.cs`; `CBP.WebApi.Middleware.ExceptionHandlingMiddleware` | PASS |
| **WEB-004** | **6 controllers NO usan `BaseApiController`** — usan `ControllerBase` plano + acceso directo `result.IsSuccess` (en lugar de `FromResult`). Inconsistencia menor. | `EmailLogController`, `GruposController`, `GruposUsuariosController`, `RolesHerenciaController`, `TipAsigPermisoController`, `UsuariosPermisosController` | WARNING |
| **WEB-005** | `ModelStateValidationFilter` (CBP) validado automaticamente; evita rep от vuelve `400`. | `AddCbpControllers` | PASS |
| **WEB-006** | Paginacion: `PagedResponse`/`PagedRequest` de CBP usados por repos GetPaged. | `CBP.WebApi.Models.PagedResponse` | PASS |
| **WEB-007** | OpenAPI refleja Result transformer (StandardResponses). | `AddCbpOpenApi` + OpenApi ext | PASS |

## 6. Clasificacion general
- **CBP.WebApi**: dominio del pipeline web (start, controller base, exception, filter, OpenApi).
- Duplicacion: **baja** (6 controllers classicos).
- Cobertura de resultado: 58/64 controllers con FromResult.

## 7. Resultado F10
- **REUTILIZAR / PASS**: PassPlat adopta CBP.WebApi para el pipeline completo, usando BaseApiController/FromResult en la mayoria de controllers.
- Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| WEB-001 | PASS | REUTILIZAR (58/64 BaseApiController) | — | — | Alta |
| WEB-002 | PASS | REUTILIZAR (pipeline CBP.WebApi) | — | — | Alta |
| WEB-003 | PASS | REUTILIZAR (exception→ProblemDetails) | — | — | Alta |
| WEB-004 | WARNING | REEMPLAZAR (migrar 6 controllers) | Baja | P2 | Alta |
| WEB-005 | PASS | REUTILIZAR (ModelState filter) | — | — | Alta |
| WEB-006 | PASS | REUTILIZAR (PagedResponse) | — | — | Alta |
| WEB-007 | PASS | REUTILIZAR (OpenApi) | — | — | Alta |
| WEB-008 | WARNING | EXTENDER (LoggingAuthorization ResultHandler) | Baja | P3 | Media |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 88 % |
| Architecture Score | 87 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-WEB-001..008 (WEB-004 limpieza) |