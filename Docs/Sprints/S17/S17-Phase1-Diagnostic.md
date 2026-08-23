# S17-Phase1-Diagnostic.md — FASE 1: Diagnóstico de instrumentación del framework

**Estado**: ✅ COMPLETA (FASE 1 — diagnóstica; read-only sobre código)
**Campaña**: S17 (Evolución framework CBP + instrumentación transversal)
**Base**: `S17-Discovery.md` (FASE 0, autorizada 2026-08-10)
**Alcance**: Decisiones de instrumentación para `Jwt_Validated`/`Jwt_Expired` (CBP.Authentication.JwtBearer) y `Event_Published`/`Event_Handled`/`Event_Failed` (CBP.Events).
**Regla**: FASE 1 NO modifica código, csproj, DI ni contratos. Produce matriz A/B/C y recomendación para autorización antes de FASE 2.

---

## 1. Hallazgos (evidencia verificado en código)

### H1 — Dependencias actuales de los frameworks objetivo
- `CBP.Authentication.JwtBearer.csproj`: FrameworkRef `Microsoft.AspNetCore.App`, Package `System.IdentityModel.Tokens.Jwt` 8.19.1, ProjectRef → `CBP.Authentication.Abstractions`. **No referencia `CBP` (Core) ni `CBP.Logging`.**
- `CBP.Events.csproj`: Package `Microsoft.Extensions.DependencyInjection` 10.0.9, ProjectRef → `CBP.Results`. **No referencia `CBP` ni `CBP.Logging`.**
- `CBP.logging.csproj`: Reference → `CBP` (Core). Serilog 4.3.1 + enrichers/sinks.

### H2 — Contrato de logging vive en CBP (Core), no en CBP.Logging
- `CBP.Core\CBP\Logging\Interfaces\ILoggerService.cs` → namespace `CBP.Logging.Interfaces`.
- `CBP.Core\CBP\Logging\Models\LogEvent.cs` → namespace `CBP.Logging.Models`.
- Catálogos (`LoggingEvents`, `LoggingScopes`, `LoggingOperations`, `LoggingCategories`, `LoggingSources`, `LoggingPropertyNames`, `LoggingCacheResults`) → `CBP.Core\CBP\Logging`.
- **Implicancia**: el contrato es un ensamblado separado (`CBP.dll`) consumible por JwtBearer/Events vía ProjectReference sin acoplar a Serilog. CBP.Logging (impl) ya depende de CBP — dirección de dependencia consistente.

### H3 — Puntos semánticos de emisión (JWT)
- **Éxito de validación** = `JwtTokenService.ValidateToken` L82 (`Handler.ValidateToken`) → punto real de `Jwt_Validated`.
- **Expirado** = catch `SecurityTokenExpiredException` L88-93 → `Jwt_Expired`.
- Orquestador `JwtAuthenticationOperator.AuthenticateAsync` (L24-52) invoca `ValidateToken` (L38) y rama `TOKEN_EXPIRED` (L42-43) / `NOT_AUTHENTICATED` (L46-47).
- **Decisión semántica**: emitir en el servicio (`JwtTokenService`) cubre todo validación directa e indirecta (operador, callers). Emitir en el operador cubre solo el flujo HTTP. La parte de menor falsificación es `JwtTokenService` (retorno del resultado real). Ambos usan MS `ILogger<T>` hoy.

### H4 — Puntos semánticos de emisión (Events)
- `DomainEventDispatcher.DispatchAsync` → punto real de `Event_Published` (entrada, post validaciones, L39).
- Handler ejecutado exitosamente L73/L87 → `Event_Handled`.
- Handler fallido (`EVENT_HANDLER_ERROR` L61-69, L93-107), timeout (`EVENT_HANDLER_TIMEOUT` L267-272) → `Event_Failed`.
- **Doble constructor** (L17 DI / L24 manual): la instrumentación debe contemplar ambos modos.
- `IEventPublisher.EventPublisher` delega al dispatcher — NO instrumentar (reescritura).

### H5 — Consumidores reales en PassPlat
- `IJwtTokenService`: inyectado en `AuthService.cs:48,69,89` (campo sin invocación directa; la emisión real es via `AuthenticationTokenService → AuthenticationTokenIssuer.Generate`). `AuthenticationTokenIssuer.cs:14,20` (generación real; ya emite `Jwt_Generated` con `ILoggerService` + `IHttpContextAccessor`, L42-55). `AuthenticationTokenService` registrado en `AplicacionDependencyInjection.cs:27`; `AuthenticationTokenIssuer` scoped.
- `IEventPublisher`: `IPService.cs:31,34`; `DispConfiableService.cs:33,37` (patrón `WithCorrelationId` + `PublishAsync`).
- Los consumidores no llevan cierre artificial: los eventos se emiten en el framework.

### H6 — DI actual (para pronosticar resolución)
- `AddCbpLogging` (CBP.Logging): registra `ILoggerService` **singleton** + `IHttpContextAccessor` + `IContextProvider` + `IExceptionLogger` — `ServiceCollectionExtensions.cs:21-24`.
- `AddJwtOperator` (JwtBearer): `JwtOptions` singleton, `IJwtTokenService`/`JwtTokenService` singleton, `IAuthenticationOperator`/`JwtAuthenticationOperator` scoped — `JwtAuthenticationExtensions.cs:12-14`.
- `AddCbpAuthentication`: `CbpAuthenticationOptions` singleton — `AuthenticationServiceCollectionExtensions.cs:13`.
- `AddDomainEvents` (Events): `IDomainEventDispatcher` scoped, `IEventPublisher` scoped — `EventServiceCollectionExtensions.cs:19-21`.
- `AddAuthenticationOperator<TOperator>`: `IAuthenticationOperator` scoped — `AuthenticationServiceCollectionExtensions.cs:20`.
- WebAPI `Program.cs`: L32 `AddCbpLogging`, L49 `AddCbpAuthentication`, L50 `AddJwtOperator`.

---

## 2. Grafo de dependencias afectado

```
Hoy:
  JwtBearer ──► Abstractions ──► Results
  Events ──► Results
  Logging ──► CBP (Core)      ← contrato ILoggerService + catálogos (0 deps)

Opción A (propuesta):
  JwtBearer ──► CBP (Core) ──► (none)
  Events ──► CBP (Core) ──► (none)
  Logging ──► CBP (Core)      (sin cambio)
  → sin ciclos: CBP no referencia a JwtBearer/Events/Logging.

Opción B:
  JwtBearer ──► Logging ──► CBP (Core)
  Events ──► Logging ──► CBP (Core)
  → sin ciclos técnicamente, pero framework acoplado a implementación Serilog.

Opción C:
  ningún cambio de ProjectReference en JwtBearer/Events.
  PassPlat registra listener del hook y traduce a LogEvent.
```

---

## 3. Matriz comparativa A/B/C

### Criterios evaluados (11)

| # | Criterio | Opción A — Framework→CBP (Core) | Opción B — Framework→CBP.Logging | Opción C — Hook propio del framework |
|---|---|---|---|---|
| 1 | **Dependencia introducida** | `JwtBearer→CBP`; `Events→CBP` (ensamblado Core, interfaz + catálogos, Sin Serilog) | `JwtBearer→CBP.Logging`; `Events→CBP.Logging` (impl concreta Serilog) | Ninguna en jwtBearer/Events; PassPlat añade adaptador/listener |
| 2 | **Dirección de dependencia** | Framework → **contrato** (Core). Consistente con DIP y con patrón existente `Logging→CBP` | Framework → **implementación de infraestructura** (abstract/definir), inviern DIP | Framework expone evento/hook; dirección de notificación framework→app |
| 3 | **Riesgo de ciclo** | **Nulo** (CBP 0 deps; nadie lo referencia de vuelta) | **Nulo** técnicamente (Logging→CBP; nadie al revés) | **Nulo** (sin referencia nueva) |
| 4 | **Impacto consumidores actuales** | Ninguno en comportamiento: JwtTokenService singleton; añadir `ILoggerService` al ctor se resuelve por DI (registrado). PassPlat sin cambios de código funcional (solo registra en Program.cs si no existiera; ya existe AddCbpLogging) | Arrastra Serilog y sinks a todo consumidor de JwtBearer/Events; PassPlat sin cambios funcionales | Requiere nuevo registro DI de listener + adaptador en PassPlat (add-code) |
| 5 | **Impacto futuros consumidores CBP** | Un host que use JwtBearer/Events deberá seguir inyectando `ILoggerService` (no-op opcional si no registrado) — es un requisito de host, documentable | Obliga a todo host a aceptar dependencia Serilog en framework — acopla el framework de por vida a infra | Host sin logging: sin coste; host con logging: implementa callback (mayor ensamblado de boilerplate) |
| 6 | **Compatibilidad hacia atrás** | **Alta**: cambios **internos** (ctor). Hacer `ILoggerService` opcional (resuelto vía `IServiceProvider.GetService` o ctor optional) → no rompe hosts que NO usan logging. Contrato público de `IJwtTokenService`/`IDomainEventDispatcher` **inalterado** | **Media**: mismo ctor opcional posible, pero introduce dependencia transitiva Serilog (runtime/deps aumenta); attr. package nivel → rompe "zero-extra-deps" | **Alta**: nuevo contrato aditivo; requiere registro nuevo (cambio de configuración de host = aditivo) |
| 7 | **Cambios DI requeridos** | **Mínimos**: el host ya registra `ILoggerService` (AddCbpLogging). No se requiere nuevo registro para que los ctor se resuelvan. Registro del framework sin cambios | **Mínimos** igualmente, pero arrastra deps Serilog a la resolución | **Medios**: nuevo registro `AddCbpAuthObservability(callback)` en host + adaptador; reinversión en cada host |
| 8 | **Testabilidad** | Alta: mock de `ILoggerService` trivial (interfaz propia, sin Serilog). Tests unitarios de ValidateToken con logger capture | Baja/Media: mock de LoggerService real requiere Serilog; acopla tests | Media-Alta: listener mockeable, pero hay que transmitir el evento del framework al adaptador (2 hops) |
| 9 | **Coherencia arquitectura actual** | **Alta**: CBP.Core YA es el hogar del contrato de logging (interfaz + catálogos + vocabulario); Logging→CBP ya validó la dirección. Extender el mismo patrón a JwtBearer/Events es coherente. Responde explícitamente: **sí, CBP debe ser el punto de abstracción de logging del framework** (no introduce responsabilidad nueva; es la que ya tiene) | **Baja**: contradice la capa contrato/impl del patrón actual (Logging depende de CBP, no al revés) | **Media**: desacopla a costa de re-inventar un mecanismo de observabilidad dentro del framework (duplica concepto) |
| 10 | **Complejidad de implementación** | **Media**: añadir ProjectRef + inyectar ILoggerService + emitir LogEvent en 2 archivos (JwtTokenService, DomainEventDispatcher) | Baja (una ProjectRef simple), alto costo de arquitectura | **Alta**: nuevo contrato de hook, evento interno, adaptador, mapeo a catálogos, propagación correlationId |
| 11 | **Impacto contrato CBP.Logging v1.0** | **Ninguno**: solo nuevos **emisores** usando vocabulario existente (Jwt_Validated/Expired/Event_* ya en LoggingEvents). No cambia Specification ni catálogos | **Ninguno** (mismo vocabulario). Riesgo via serlilog no contractual | **Ninguno** formalmente, pero el emisor real deja de cumplir el contrato (el framework emite a hook; PassPlat traduce) — diluye la gobernanza |

### Sub-decisiones JWT

| Criterio | Emitir en `JwtTokenService.ValidateToken` | Emitir en `JwtAuthenticationOperator.AuthenticateAsync` |
|---|---|---|
| Punto semántico real | ✅ Validación directa con resultado (éxito L82 / expired L88) | Media — orquesta, pero repite lógica de expiración |
| Cobertura | ✅ Todos los callers (operador y futuros) | ❌ Solo flujo HTTP |
| Duplicación | Ninguna | ⚠ Duplicaría observación con ValidateToken |
| Testabilidad | Alta (método aislado) | Media (requiere HttpContext) |
| **Recomendación** | **Emitir en `JwtTokenService.ValidateToken`** (punto menor, cobertura completa): `Jwt_Validated` tras L82 éxito; `Jwt_Expired` en catch L88-93. `JwtAuthenticationOperator` mantiene MS logging actual (no duplica) | NO duplicar aquí |

### Sub-decisiones Events

| Criterio | En `DomainEventDispatcher.DispatchAsync` | En `EventPublisher` |
|---|---|---|
| Punto real | ✅ despacho/handlers/errores (L31-145) | ❌ reenvío delegado (no instrumentar) |
| Doble ctor | ⚠ instrumentar ambos modos (DI `_serviceProvider`/manual) | n/a |
| `Event_Published` | ✅ entrada post-validaciones (L39) | — |
| `Event_Handled` | ✅ L73/L87 (éxito handler) | — |
| `Event_Failed` | ✅ L61-69/L93-107 (error handler) + timeout L267-272 | — |
| **Recomendación** | **Emitir únicamente en `DomainEventDispatcher`** — propaga `@event.EventType`, `@event.EventId`, `@event.CorrelationId` (disponibles; ver `EventBase`) | NO instrumentar EventPublisher (reescritura) |

---

## 4. Recomendación técnica

**Opción A (framework → CBP/Core)** para ambos subsistemas, con resolución **opcional** de `ILoggerService`:

1. `CBP.Authentication.JwtBearer.csproj` + `CBP.Events.csproj` → añadir `ProjectReference` a `..\CBP.Core\CBP\CBP.csproj`.
2. **JWT** — en `JwtTokenService`:
   - Nuevo ctor que inyecte `ILoggerService?` (o resolución vía `IServiceProvider.GetService` con no-op por defecto) → **compatibilidad hacia atrás** para hosts sin `AddCbpLogging`.
   - Emitir `LogEvent{EventName=JwtValidated, Scope=Authentication}` tras éxito (L82); `Jwt_Expired` en catch L88-93; `Jwt_Expired` pero conservando MS LogWarning para no perder diagnósticos actuales.
3. **Events** — en `DomainEventDispatcher`:
   - Instrumentar con `ILoggerService?` opcional (resuelto desde `_serviceProvider.GetService<ILoggerService>()`, activo en modo DI; null en modo manual → no-op).
   - Emitir `Event_Published` (L39), `Event_Handled` (éxito), `Event_Failed` (error/timeout), usando `@event.EventType/EventId/CorrelationId`.
4. **No duplicar** emisión en `JwtAuthenticationOperator` ni en `EventPublisher`.
5. **No tocar** catálogos ni Specification v1.0 (vocabulario ya existe).

**Justificación**: CBP.Core ya es el punto de abstracción del contrato de logging (interfaz + vocabulario); añadir que los frameworks hermanos dependan de él **no crea una responsabilidad nueva** — la consolida. La dirección framework→contrato es DIP-compatible, sin ciclos, y mantiene las implementaciones (Serilog) intercambiables. La Opción B acopla el framework a infra de por vida; la C diluye el punto semántico y la gobernanza del contrato.

**Criterio de salida FASE 1 cumplido**: existe evidencia suficiente para elegir arquitectura (Opción A). Pendiente autorización.

---

## 5. Riesgos

| # | Riesgo | Perfil | Mitigación |
|---|---|---|---|
| R1 | CBP.Core se convierte en "núcleo de logging" referenciado por varios frameworks | Decisión arquitectónica (deliberada) | Documentar en S17 diseño; CBP.Core≈0 deps intocados; referencia es solo a contrato |
| R2 | Hosts de CBP que no registren `ILoggerService` rompan la construcción `JwtTokenService` (singleton) o `DomainEventDispatcher` (scoped) | Alto si ctor hard-inyecta | Resolución **opcional** (`GetService` o ctor `ILoggerService? = null` + no-op) → sin fallo |
| R3 | Doble logging (MS `ILogger<T>` + `ILoggerService`) en JwtTokenService/AuthenticationMiddleware | Medio | Mantener MS logging actual y añadir eventos estructurados como capa adicional (no reemplazar en v1 de S17) |
| R4 | Volumen de eventos: `Jwt_Validated` se emite por cada request autenticado | Medio | EventName es aditivo; se controla por nivel Serilog (LogInformation); documentar umbral |
| R5 | `Event_Failed` en modo manual (sin DI) no emite: falta contexto | Bajo | Aceptar no-op en modo manual (uso legacy); documentar |
| R6 | Resolución scoped de `ILoggerService` (singleton) en `DomainEventDispatcher` (scoped) — sin conflicto | Bajo | Singleton inyectado en scoped es seguro (captación por scope) |

---

## 6. Estrategia de compatibilidad

- **Aditivo**, no breaking: se añade dependencia de ensamblado Core (sin cambiar dependencias existentes) y constructor con `ILoggerService` opcional.
- Los contratos públicos `IJwtTokenService`, `IAuthenticationOperator`, `IDomainEventDispatcher`, `IEventPublisher` **no cambian su firma**.
- Hosts PassPlat: ya registran `AddCbpLogging` (Program.cs L32) → resolución automática. Sin cambio en código de negocio.
- Hosts CBP que no usen logging: siguen compilando por sobrescritura del ctor opcional / null.
- `CBP.Logging.Specification.md` v1.0: **sin cambios**. El catálogo `LoggingEvents` ya registra los eventos; solo se pasa su estado de "reservado" a "emitido" en `Logging.EventCatalog.md` (cambio documental, no contractual).

---

## 7. Pruebas necesarias (FASE 2/3)

| # | Prueba | Cobertura |
|---|---|---|
| T1 | Unit `JwtTokenService.ValidateToken` éxito → LogEvent `Jwt_Validated` (fake capturador de ILoggerService) | Emisor + contrato (eventName/scope/category/operation) |
| T2 | Unit `JwtTokenService.ValidateToken` token expirado → `Jwt_Expired` + return null (comportamiento inalterado) | Regresión + emisor |
| T3 | Unit `JwtTokenService.ValidateToken` token inválido/firma → sin `Jwt_Validated` (respeta semántica) | Anti-falsificación |
| T4 | Unit `DomainEventDispatcher` (modo DI) handler éxito → `Event_Published` + `Event_Handled` | Emisor + contrato |
| T5 | Unit `DomainEventDispatcher` handler falla → `Event_Failed` + Result.Failure conservado | Emisor + regresión del pipeline de errores |
| T6 | Unit `DomainEventDispatcher` modo manual sin ILoggerService → no-op (sin fallo) | Compatibilidad doble ctor |
| T7 | Contract tests `CacheLogContractTests` actuales siguen PASS (4/4) | No regresión contrato v1.0 |
| T8 | `dotnet build PassPlat.slnx` (0 errores, 0 warnings nuevos) | Baseline |
| T9 | `dotnet test PassPlat.slnx` (70/70 + nuevos) | Baseline + nuevas |
| T10 | E2E opcional (Gate C parcial o dedicado S17): request autenticado → `Jwt_Validated` con correlationId | Integra-ción (post FASE 4) |

---

## 8. Decisión que requiere autorización antes de FASE 2

> **Solicitud**: autorizar la **Opción A** (instrumentación `Jwt_Validated`/`Jwt_Expired` en `JwtTokenService` y `Event_Published`/`Event_Handled`/`Event_Failed` en `DomainEventDispatcher`), con `ProjectReference` de `JwtBearer` y `Events` hacia `CBP` (Core), `ILoggerService` opcional en los constructores, y sin cambios en contratos públicos ni en `CBP.Logging.Specification.md` v1.0.

Alternativas presentadas para registro: A (recomendada), B (descartada por acople a infra Serilog), C (diferida: viable solo si se desea framework 100% agnóstico, a coste de complejidad y dilución del punto semántico).

Sin autorización explícita, FASE 2 NO se inicia.

## 9. Registro de decisiones FASE 1

| Decisión | Valor | Fecha |
|---|---|---|
| Opción de arquitectura recomendada | **A** — framework→CBP (Core), contrato `ILoggerService`, 0 ciclos, sin tocar v1.0 | 2026-08-10 |
| Punto de emisión JWT | `JwtTokenService.ValidateToken` (éxito→Jwt_Validated; expired→Jwt_Expired). NO en JwtAuthenticationOperator | 2026-08-10 |
| Punto de emisión Events | `DomainEventDispatcher` (Published/Handled/Failed). NO en EventPublisher | 2026-08-10 |
| `Password_Reset` | Observación histórica S16.4 — fuera del alcance de implementación S17 | 2026-08-10 |
| S16.4 | No reabierto; sus deudas son el alcance de S17 | 2026-08-10 |