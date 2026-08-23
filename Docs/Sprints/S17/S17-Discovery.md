# S17-Discovery.md — FASE 0: Discovery del Sprint

**Estado**: ✅ COMPLETA (FASE 0 cerrada técnicamente, autorizada 2026-08-10)
**Campaña**: S17 (Evolución framework CBP + instrumentación transversal)
**Objetivo**: Establecer la base de evidencia para el diagnóstico (FASE 1) de las deudas de observabilidad del framework: `Jwt_Validated` y `Event_Published`/`Event_Handled`/`Event_Failed`.
**Base**: Sprint S16.4 CERRADO formalmente (Gate C = PASS, RC1 = APROBADO, S17 = AUTORIZADO, 2026-08-08).
**Método**: FASE 0 = read-only. Solo estudio, mapeo y reporte. No se modificó código, csproj, DI ni contratos.

---

## 1. Estado S16.4 (punto de partida)

- S16.4 cerrado formalmente (2026-08-08): Gate C = PASS (11/11 + fix 3/3), 8/8 criterios, RC1 APROBADO, S17 AUTORIZADO.
- Deudas S16.4 trasladadas a S17 como **alcance explícito** (no bloqueos):
  - `Jwt_Validated` → punto real en `CBP.Authentication.JwtBearer`.
  - `Event_Published`/`Event_Handled`/`Event_Failed` → puntos reales en `CBP.Events`.
- `Password_Reset`: implementado en `PasswordService.cambiarPasswordAsync` (rama `ETipoCambioPwd.Reset`); su cobertura E2E quedó como **observación histórica de S16.4** — NO es alcance bloqueante de S17.
- Baseline a preservar: build 0 errores · xUnit 70/70 · warnings NU1603 preexistentes aceptados.
- Artefactos congelados de S16.4: `Docs\Evidence\gatec-fix-20260808.log`, `gatec-structured-run-20260808.log`.

## 2. Inventario de la solución

- Solución canónica: `D:\CODIGOS\PassPlat\PassPlat.slnx` → **28 proyectos, 806 documentos** (cargado en Roslyn).
- 8 proyectos PassPlat: `Aplicacion`, `Aplicacion.Dtos`, `Aplicacion.Test`, `Datos`, `Dominio`, `Web`, `WebAPI`, `Consola`.
- ~20 proyectos CBP referenciados vía `../CBP/` (carpetas /fwk/Core/, /fwk/Data/, /fwk/Infraestructure/, /fwk/Caching/, /fwk/Services/, /fwk/WebApi/).
- `D:\CODIGOS\CBP` no tiene `.sln` propio. Sin `global.json`/`NuGet.config`/`Directory.*` en raíz de PassPlat.
- Proyectos CBP **no incluidos** en PassPlat.slnx (existen en disco): `CBP.Security.Password.Tests`, `CBP.Excel`, `CBP.Services.Sync`, `CBP.Data.Synchronous`, UI/WinUI (`WinFrms`, `ToastBenchmark`, `LoginFlowDemo`).

## 3. Grafo de dependencias (Roslyn, sin ciclos)

```
CBP.Authentication.JwtBearer ──► CBP.Authentication.Abstractions ──► CBP.Results
CBP.Events ──► CBP.Results
CBP.Logging ──► CBP (Core)
CBP (Core) ──► (ninguna; 0 dependencias)
```

- **Hallazgo crítico**: ni `CBP.Authentication.JwtBearer` ni `CBP.Events` referencian hoy `CBP` (Core) ni `CBP.Logging`.
- `CBP` (Core) aloja el contrato de logging (`ILoggerService`, catálogos `LoggingEvents`/`LoggingScopes`/`LoggingOperations`/`LoggingCategories`/`LoggingSources`/`LoggingPropertyNames`, `LogEvent`) con **0 dependencias** → candidato natural como punto de abstracción sin riesgo de ciclo.

## 4. Csproj de los proyectos objetivo (evidencia)

| Proyecto | Target | Dependencias actuales |
|---|---|---|
| `CBP.Authentication.JwtBearer.csproj` | net10.0 | FrameworkRef `Microsoft.AspNetCore.App`; Package `System.IdentityModel.Tokens.Jwt` 8.19.1; ProjectRef → Abstractions |
| `CBP.Events.csproj` | net10.0 | Package `Microsoft.Extensions.DependencyInjection` 10.0.9; ProjectRef → CBP.Results |
| `CBP.Logging.csproj` | net10.0 | FrameworkRef ASP.NET Core; Serilog 4.3.1 + enrichers/sinks; ProjectRef → CBP (Core) |
| `CBP.csproj` (Core) | net10.0 | **0 dependencias** |

## 5. Puntos semánticos reales (confirmados con evidencia)

### Jwt_Validated / Jwt_Expired
- `CBP.Authentication.JwtBearer\JwtTokenService.cs`
  - `ValidateToken(token, out validatedToken)` → **éxito en L82** (`Handler.ValidateToken`) → punto real de `Jwt_Validated`.
  - catch `SecurityTokenExpiredException` **L88-93** → punto real de `Jwt_Expired`.
  - Ya usa MS `ILogger<JwtTokenService>` (LogDebug L84 / LogWarning L90 / LogError L102).
- `JwtAuthenticationOperator.cs` (orquestador): invoca `ValidateToken` (L38) + rama `TOKEN_EXPIRED` (L42-43) + `NOT_AUTHENTICATED` (L46-47). Usa MS `ILogger<JwtAuthenticationOperator>`.
- `IJwtTokenService.cs`: contrato `GenerateToken`/`GenerateRefreshToken`/`ValidateToken`.

### Event_Published / Event_Handled / Event_Failed
- `CBP.Core\CBP.Events\DomainEventDispatcher.cs`
  - `DispatchAsync` → **punto real de despacho/notificación L31-145**.
  - Handler ejecutado exitosamente → punto real de `Event_Handled` (L73, L87).
  - Handler fallido (`EVENT_HANDLER_ERROR`, L61-69 y L93-107) → punto real de `Event_Failed`.
  - Timeout (`EVENT_HANDLER_TIMEOUT`, L267-272) y cancelación (`EVENT_DISPATCH_CANCELLED`, L275-280) como ramas explícitas.
  - **Doble constructor**: (IServiceProvider, configuration) y (Dictionary<Type, List<object>>, configuration) — la instrumentación debe funcionar en ambos modos.
- `CBP.Core\CBP.Events\IEventPublisher.cs`: `EventPublisher.PublishAsync/PublishAllAsync` delegan al dispatcher (no instrumentar aquí; es reenvío).
- `EventBase.cs`: ya porta `CorrelationId` (Guid N default) + `WithCorrelationId` → **no introducir AsyncLocal/static nuevo**.

## 6. Vocabulario de logging disponible (sin ampliar catálogo)

`CBP.Core\CBP\Logging\LoggingEvents.cs` ya define:
- `JwtValidated = "Jwt_Validated"`, `JwtExpired = "Jwt_Expired"`, `JwtGenerated`.
- `EventPublished = "Event_Published"`, `EventHandled = "Event_Handled"`, `EventFailed = "Event_Failed"`.

Scopes disponibles: `authentication`, `domainEvents`. Operations: `Validate`, `Publish`, `Handle`. Categories: `application.auth`, `domain.events`.
→ **No crear eventos nuevos**; usar el vocabulario existente conforme `CBP.Logging.Specification.md` v1.0 (CONGELADO).

## 7. Consumidores verificados en PassPlat

### IJwtTokenService
- `PassPlat.Aplicacion\Services\SPro\AuthService.cs:48` (campo `_jwtService`; inyectado L69).
- `PassPlat.Aplicacion\Services\Authentication\AuthenticationTokenIssuer.cs:14` (campo `_jwtService`; inyectado L20) — **ya emite `Jwt_Generated`** vía `ILoggerService` + `LogEvent` (L42-55) con category/operation/correlationId/userId/tenantId. Referente de patrón de emisión.

### IEventPublisher
- `PassPlat.Aplicacion\Services\BBDD\IPService.cs:31` (campo; inyectado L34).
- `PassPlat.Aplicacion\Services\SPro\DispConfiableService.cs:33` (campo; inyectado L37) — patrón `WithCorrelationId` + `PublishAsync` (L68-80).

Los consumidores NO deben llevar cierre artificial de los eventos; el punto semántico está en el framework.

## 8. DI y registro actual

- WebAPI `Program.cs`: L32 `AddCbpLogging`, L49-50 `AddCbpAuthentication`/`AddJwtOperator`, L70 `AddAuthentication(CbpFallback)`, L244 `UseCbpAuthentication`.
- `CBP.Logging\DependencyInjection\ServiceCollectionExtensions.cs`: `AddCbpLogging` registra `ILoggerService` (singleton) + `IHttpContextAccessor` + enrichers.
- `CBP.Events\EventServiceCollectionExtensions.cs`: `AddDomainEvents()`, `AddEventHandler<TEvent,THandler>`, `AddEventHandlersFromAssembly`.
- `PassPlat.Aplicacion\AplicacionDependencyInjection.cs` L110-111: `AddDomainEvents()` + `AddEventHandlersFromAssembly`.
- `AuthenticationTokenIssuer` (PassPlat) ya inyecta `ILoggerService` + `IHttpContextAccessor` → patrón probado de consumo del contrato.

## 9. Hallazgos FASE 0 (consolidados)

1. **H1/H5 CONFIRMADAS**: ni `CBP.Authentication.JwtBearer` ni `CBP.Events` dependen de `CBP` (Core) ni de `CBP.Logging`. Instrumentar en el punto real de la validación JWT y del despacho de eventos **requiere introducir una dependencia de framework→contrato de logging** (decisión arquitectónica, NO meramente técnica).
2. **Punto de abstracción candidato**: `CBP` (Core) ya es el hogar del contrato `ILoggerService` y catálogos, con 0 dependencias y consumido por `CBP.Logging`. Es el candidato natural para que JwtBearer/Events dependan SIN acoplarse a Serilog/implementación.
3. **Sin riesgo de ciclo**: `CBP` no referencia a JwtBearer ni a Events, y `CBP.Logging` ya depende de `CBP`.
4. **Framework usa MS logging hoy**: `JwtTokenService` y `JwtAuthenticationOperator` inyectan `ILogger<T>`. La instrumentación debe convivir o reemplazar ese uso según la opción A/B/C elegida en FASE 1.
5. **Double-constructor en dispatcher**: la instrumentación de `Event_*` debe cubrir el modo DI y el modo manual.
6. **Vocabulario completo**: no se requiere tocar catálogos para emitir los cinco eventos de alcance.
7. **Divergencia documental**: AGENTS.md ubica `CBP.Caching` bajo `CBP.Infraestructure`, pero en disco es directorio raíz `D:\CODIGOS\CBP\CBP.Caching`. Se documenta para reconciliación futura (no bloqueante).

## 10. Riesgos preliminares (detalle en FASE 1)

- Cambio de framework compartido (impacto en consumidores actuales y futuros de CBP).
- Dependencia nueva = decisión arquitectónica (regla §7/§18 del plan S17) → requiere autorización.
- Riesgo de acoplar framework a infraestructura concreta (Serilog) si se elige dependencia directa a CBP.Logging.
- `Event_*` debe mantener ambos modos del dispatcher.
- Coherencia de requisitos `operation`/`method`/`scope`/`category` del contrato v1.0 al emitir desde el framework (no solo desde PassPlat).

## 11. Opciones A/B/C identificadas (detallar y evaluar en FASE 1)

| Opción | Dependencia introducida | Sentido |
|---|---|---|
| **A** | `JwtBearer → CBP` y `Events → CBP` (Core) | Framework depende del contrato (interfaz `ILoggerService`), resuelto por DI en la app host. Sin acople a Serilog. |
| **B** | `JwtBearer → CBP.Logging` y `Events → CBP.Logging` | Framework acoplado a la implementación concreta (Serilog). Directo, pero acopla infraestructura. |
| **C** | Ninguna directa (hook/callback propio en framework consumido por PassPlat) | Sin dependencia, pero más código de extensión y punto de emisión indirecto. |

> La determinación de cuál es la correcta arquitectónicamente NO se resuelve en FASE 0 (evidencia = viabilidad técnica). Se evalúa en FASE 1 con matriz A/B/C completa y se decide tras autorización explícita (FASE 1 → decisión → FASE 2).

## 12. Siguiente paso

- FASE 1 — Diagnóstico detallado (read-only de código): matriz A/B/C con criterios completos, impacto en consumidores, riesgos, compatibilidad, cambios DI, testabilidad, recomendación técnica → `S17-Phase1-Diagnostic.md`.
- Criterio de salida FASE 1: evidencia suficiente para elegir una arquitectura de instrumentación del framework. Sin implementar A/B/C hasta autorización explícita de la opción seleccionada.
- FASE 2 — Diseño/implementación mínima (tras gate).

## 13. Registro de decisión

| Decisión | Valor | Fecha |
|---|---|---|
| FASE 0 Discovery | ✅ COMPLETA (autorizada) | 2026-08-10 |
| Se conserva rigor de S16.4 | No agregar ProjectReference e implementar; primero diagnóstico → decisión → diseño | 2026-08-10 |
| `Password_Reset` | Observación histórica de S16.4, no bloqueo de S17 | 2026-08-10 |