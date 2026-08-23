# S17-Phase2-Plan.md — FASE 2: Decisión arquitectónica + Plan de instrumentación

**Estado**: ✅ DECISIÓN TOMADA — Opción A (autorizada para planificación 2026-08-10)
**Campaña**: S17 (Evolución framework CBP + instrumentación transversal)
**Base**: `S17-Discovery.md` (F0) + `S17-Phase1-Diagnostic.md` (F1)
**Regla**: Este documento define QUÉ cambiará (proyectos/contratos) y CÓMO (migración/compatibilidad). La implementación (FASE 3) solo comienza tras aprobación de este plan.

---

## 1. Decisión arquitectónica

**Opción A** para ambos subsistemas: `CBP.Authentication.JwtBearer → CBP` (Core) y `CBP.Events → CBP` (Core), inyectando el contrato `ILoggerService` con resolución **opcional** (sin romper hosts que no registran logging).

### Por qué A (resumen de F1)
| Criterio | A ✅ |
|---|---|
| Dirección de dependencia | Framework → **contrato** (Core), DIP-compatible, patrón ya validado por `CBP.Logging → CBP` |
| Riesgo de ciclo | Nulo (CBP 0 deps; nadie lo referencia de vuelta) |
| Acoplamiento | Solo interfaz + catálogos; **no** Serilog |
| Compatibilidad consumidores | Alta: `ILoggerService` opcional, contratos públicos intactos |
| Coherencia | CBP.Core ya es el punto de abstracción de logging del framework |
| Contrato v1.0 | Sin cambios |

B descartada (acople a infra Serilog). C diferida (complejidad, dilución del punto semántico).

---

## 2. Cambios exactos por proyecto

### 2.1 `CBP.Authentication.JwtBearer.csproj`
- **Añadir al ItemGroup ProjectReference** (archivo verificado): `D:\CODIGOS\CBP\CBP.Authentication\CBP.Authentication.JwtBearer\CBP.Authentication.JwtBearer.csproj`
  ```xml
  <ProjectReference Include="..\..\CBP.Core\CBP\CBP.csproj" />
  ```
- **Sin cambiar**: FrameworkRef `Microsoft.AspNetCore.App` (L12), Paquete `System.IdentityModel.Tokens.Jwt` 8.19.1 (L16), ProjectRef → Abstractions (L20).

### 2.2 `CBP.Events.csproj`
- **Añadir al ItemGroup ProjectReference** (archivo verificado): `D:\CODIGOS\CBP\CBP.Core\CBP.Events\CBP.Events.csproj`
  ```xml
  <ProjectReference Include="..\..\CBP.Core\CBP\CBP.csproj" />
  ```
- **Sin cambiar**: Paquete `Microsoft.Extensions.DependencyInjection` 10.0.9 (L10), ProjectRef → CBP.Results (L14).

> Ruta relativa verificada: ambos proyectos resuelven `..\..\CBP.Core\CBP\CBP.csproj` (desde su directorio → `D:\CODIGOS\CBP\`→ `CBP.Core\CBP\`). `CBP.csproj` es net10.0, `ImplicitUsings`+`Nullable` enable — 0 deps confirmadas (archivo leído).

### 2.3 `CBP.Authentication.JwtBearer\JwtTokenService.cs` (Jwt_Validated / Jwt_Expired)
Punto semántico: `ValidateToken` (L78-106), single punto real de validación.

- **Nuevo campo**: `private readonly CBP.Logging.Interfaces.ILoggerService? _olog;`
- **Nuevo ctor**: el ctor actual es `JwtTokenService(JwtOptions options, ILogger<JwtTokenService> logger)` (L21). Añadir un **segundo overload** con `ILoggerService? olog` (casi-delegar al primero), SIN tocar el ctor existente — cero `new` en callers, host sin `ILoggerService` conserva el ctor simple.
- **Emisión (usar catálogos exactos)**:
  - Namespace de `ILoggerService` = `CBP.Logging.Interfaces`; `LogEvent` = `CBP.Logging.Models`; catálogos = `CBP.Logging`.
  - `Options`: éxito (L83 tras `Handler.ValidateToken`) con `LogEvent`:
    - `EventName = LoggingEvents.JwtValidated`, `Scope = LoggingScopes.Authentication`, `Message = "JWT validado"`, `Properties[LoggingPropertyNames.Category] = LoggingCategories.ApplicationAuth`, `Properties[LoggingPropertyNames.Operation] = LoggingOperations.Validate`.
  - `Jwt_Expired`: catch `SecurityTokenExpiredException` (L88-93) `EventName = LoggingEvents.JwtExpired`, `Exception = ex`, mismas Scope/Category/Operation.
  - Ambas con método `_olog?.LogInformation(...)` — no-op si null.
- **Conservar** MS `ILogger<JwtTokenService>` actual (LogDebug/LogWarning) — no eliminar diagnóstico.
- **No duplicar** en `JwtAuthenticationOperator`.

### 2.4 `CBP.Core\CBP.Events\DomainEventDispatcher.cs` (Event_Published / Event_Handled / Event_Failed)
Punto semántico: `DispatchAsync` (L31-145) cubre ambos modos (`_serviceProvider` DI y `_manualHandlers` manual). Fichero verificado en `CBP.Core\CBP.Events\`.

- **Instrumentación sin tocar ctors**: resolver `ILoggerService?` una vez vía campo privado:
  - En ctor DI (`IServiceProvider`, L17-22): `_olog = serviceProvider.GetService<CBP.Logging.Interfaces.ILoggerService>();`.
  - En ctor manual (`Dictionary`, L24-29): `_olog = null` → no-op (comportamiento legacy intacto).
- **Datos reales de `EventBase`** (leído): `EventId` (Guid), `OccurredOn` (DateTime), **`CorrelationId` ya es `string`** (`Guid.NewGuid().ToString("N")`), `EventType` (abstract string). Sin `.ToString()` extra.
- **Emisión**:
  - `Event_Published`: al inicio tras validaciones (antes de L39 try o al inicio del try). Properties: `EventType`, `CorrelationId` (solo si no vacío) → claves `LoggingPropertyNames.Event` y `LoggingPropertyNames.CorrelationId`; `Category=LoggingCategories.DomainEvents`, `Operation=LoggingOperations.Publish`, `Scope=LoggingScopes.DomainEvents`.
  - `Event_Handled`: tras invocación exitosa por handler — emite **una por handler**, dentro del bucle/paralelo (punto tras `InvokeHandlerAsync`/`ExecuteWithTimeoutAsync`). Properties: `EventType`, `HandlerType` (clave `LoggingPropertyNames.Method`), `CorrelationId`. Operation=`LoggingOperations.Handle`.
  - `Event_Failed`: en ramas de error handler (caché paralelo L61-69 y secuencial L93-107) y timeout (`ExecuteWithTimeoutAsync` L265-272): `Exception=ex` (message en timeout), Properties: `EventType`, `HandlerType`, `CorrelationId`. Operation=`LoggingOperations.Handle`.
- **No instrumentar** `EventPublisher` (reescritura) ni `DispatchAllAsync` por evento (ya llama a `DispatchAsync`; NO emitir dos veces — solo `DispatchAsync` emite).
- **conservar** Result semantics intactas (éxito/fracaso del dispatcher no cambia).

---

## 3. Contratos afectados

| Contrato | ¿Cambia? | Nota |
|---|---|---|
| `IJwtTokenService` | ❌ No | Firma intacta |
| `JwtTokenService` ctor | ⚠️ Aditivo (overload) | Ctor existente SIN tocar; se añade overload con `ILoggerService?` — compatible binaria/vía DI |
| `IAuthenticationOperator` | ❌ No | Intacto |
| `IDomainEventDispatcher` | ❌ No | Intacto |
| `IEventPublisher` | ❌ No | Intacto |
| `DomainEventDispatcher` ctor | ❌ No | Sin tocar; logging desde `IServiceProvider` ya inyectado |
| `ILoggerService` (CBP) | ❌ No | Interfaz intacta |
| `LogEvent` / Catálogos | ❌ No | v1.0 congelado; vocabulario existente |
| `AddJwtOperator` / `AddCbpAuthentication` / `AddDomainEvents` | ❌ No | No cambian firma |
| `AddCbpLogging` | ❌ No | Ya registra `ILoggerService` singleton (CBP.Logging Host) |

**Clasificación**: todos los cambios son **Internos** o **Aditivos** (Público compatible). **Ningún breaking.**

> DI verificada: `AddJwtOperator` registra `JwtTokenService` como **singleton** (`AddSingleton<IJwtTokenService, JwtTokenService>`; `JwtAuthenticationOperator` scoped vía `AddAuthenticationOperator`). `AddDomainEvents` registra `DomainEventDispatcher` como **scoped** y `EventDispatcherConfiguration` como singleton. `AddCbpLogging` registra `ILoggerService` singleton → resuelto por `GetService` en ambos frameworks.

---

## 4. Estrategia de migración y compatibilidad

1. **Fase de código (F3)**: añadir ProjectRefs + instrumentación en JwtTokenService (overload ctor) + DomainEventDispatcher (resolución desde ISP). Hosts sin `ILoggerService` → falan a no-op (JWT: `_olog == null`; Events: `GetService` null → no-op).
2. **Hosts PassPlat**: ya llaman `AddCbpLogging` (Program.cs L32) → `ILoggerService` singleton resuelto; sin cambios de código de negocio.
3. **Hosts CBP sin logging**: `ILoggerService` null → no-op silencioso; compilan y ejecutan sin cambios.
4. **Sin tocar** `CBP.Logging.Specification.md` v1.0: solo se actualiza el estado del catálogo documental (`Logging.EventCatalog.md`: de "reservado" a "emitido") tras la implementación (cambio documental, no contractual).
5. **Rollback**: revertir ProjectRefs y las N líneas de emisión por archivo; no hay migración de datos.

---

## 5. Riesgos residuales del plan

| # | Riesgo | Mitigación |
|---|---|---|
| R1 | CBP.Core referenciado desde múltiples frameworks | Aceptado deliberadamente; CBP 0 deps, sin ciclo |
| R2 | Host sin `ILoggerService` → ¿rompe DI? | No: `GetService` → null → no-op; ctor JWT original intacto |
| R3 | Doble logging (MS + CBP.Logging) en JWT | Convivencia intencional v1; MS preserva diagnóstico; CBP emite eventos estructurados |
| R4 | `Event_Published` + N×`Event_Handled` = volumen | Eventos aditivos controlados por nivel Serilog; documentar umbral |
| R5 | `EventBase.CorrelationId` es string N (Guid sin guiones) vs enrichers W3C `traceparent` | Coexistencia: se emite `@event.CorrelationId` cuando set; enrichers HTTP siguen aportando `correlationId` W3C del request |
| R6 | `DispatchAllAsync` podría emitir 2× | Se emite únicamente en `DispatchAsync`; `DispatchAll` no añade emisión |

---

## 6. Pruebas (F3/F4)

| ID | Prueba | Tipo |
|---|---|---|
| T1 | `JwtTokenService.ValidateToken` éxito → LogEvent `Jwt_Validated` (fake capturador ILoggerService) | Unit |
| T2 | `ValidateToken` expirado → `Jwt_Expired` + retorno null (comportamiento intacto) | Unit |
| T3 | `ValidateToken` inválido → SIN `Jwt_Validated` (anti-falsificación) | Unit |
| T4 | `DomainEventDispatcher` (modo DI) éxito → `Event_Published` + `Event_Handled` (1/handler) | Unit |
| T5 | `DomainEventDispatcher` handler falla → `Event_Failed` + Result.Failure conservado | Unit |
| T6 | Modo manual (Dictionary) sin ILoggerService → no-op sin fallo | Unit |
| T7 | Contract tests `CacheLogContractTests` 4/4 siguen PASS | Regresión |
| T8 | `dotnet build PassPlat.slnx` 0 errores / 0 warnings nuevos | Baseline |
| T9 | `dotnet test PassPlat.slnx` 70/70 + nuevas | Baseline+ |
| T10 | (post-F4) E2E dedicado S17: request con JWT → `Jwt_Validated`, evento de dominio → `Event_*` con correlationId | Integración |

**Nota testabilidad T1-T6**: los tests vivirán en `PassPlat.Aplicacion.Test` (ya referencia CBP.JwtBearer, CBP.Events y CBP vía project refs transitivas; verificar en F3). Uso de un `ILoggerService` fake estándar (capturador de LogEvent) — la interfaz es propia, sin Serilog. T4/T5 requieren construir el dispatcher con `IServiceProvider` real (vía `ServiceProvider` que registre el handler + ILoggerService fake) o el ctor manual + acceso a `_olog` por reflexión — preferir el modo DI en el test.

---

## 7. Gate de FASE 2 → autorización

No implementar FASE 3 sin aprobación de este plan. La decisión de Opción A está autorizada solo como **dirección**; el detalle técnico de emisión (puntos, properties) queda fijado en este documento y aprobado con él.

**Registrar en decisión**: aprobación del plan → FASE 3 (implementación mínima: csproj + JwtTokenService + DomainEventDispatcher) → F4 (build+test) → F5 (grounding) → F6 (evidencia) → F7 (doc) → F8 (cierre/Gate S17).

---

## 8. Registro de decisiones FASE 2

| Decisión | Valor | Fecha |
|---|---|---|
| Alternativa elegida | **Opción A** — framework → CBP (Core), ILoggerService opcional | 2026-08-10 |
| Proyectos a modificar | `CBP.Authentication.JwtBearer.csproj`, `CBP.Events.csproj` | 2026-08-10 |
| Archivos a modificar | `JwtTokenService.cs`, `DomainEventDispatcher.cs` | 2026-08-10 |
| Contratos públicos | Sin cambios (aditivos internos) | 2026-08-10 |
| Contrato CBP.Logging v1.0 | Sin cambios | 2026-08-10 |
| Consumidores manuales | 0 verificados (todo DI) | 2026-08-10 |
| Consumidores csproj | `PassPlat.Aplicacion`, `PassPlat.WebAPI`, `PassPlat.Aplicacion.Test` (backup `PassPlat_20260722` ignorado) | 2026-08-10 |
| Verificación de código (esta revisión) | Ctor JWT real `(JwtOptions, ILogger<JwtTokenService>)` L21; JWT singleton, DomainEventDispatcher scoped; `EventBase.CorrelationId` ya es `string` N; catálogos con Currency exacta (`LoggingEvents.JwtValidated/JwtExpired/EventPublished/EventHandled/EventFailed`, `LoggingScopes`, `LoggingCategories`, `LoggingOperations`, `LoggingPropertyNames`) | 2026-08-10 |
| Cambio sobre boceto original | Ctor Jwt: **overload** (no parámetro opcional en el existente); Events: resolver en ctor vía `GetService`; `Event_Published` en `DispatchAsync` inicio; `Event_Handled` 1/handler; `Event_Failed` en caché + timeout | 2026-08-10 |
| Estado | **Plan ajustado y verificado contra código real — pendiente aprobación para FASE 3** | 2026-08-10 |

---

## 9. Estado de ejecución F3–F8 (actualización 2026-08-10)

> Administrado por el agente durante la sesión de implementación. F3–F5 cerradas; F6 resuelta por S18 (evidencia Event_* en vivo); F7 cerrada; F8 cierre de sesión completado. Gate S17 cerrado formalmente con `S17-Closure.md`.

### F3 — Implementación ✅
- ProjectRef añadida a `CBP.Authentication.JwtBearer.csproj` y `CBP.Events.csproj` → `CBP.csproj`.
- `JwtTokenService.cs`: nuevo overload ctor con `ILoggerService? olog` (existe ctor original intacto); `Jwt_Validated` (éxito) y `Jwt_Expired` (catch `SecurityTokenExpiredException`) emitidos vía `_olog?.LogInformation` con catálogos; MS `ILogger<JwtTokenService>` conservado.
- `DomainEventDispatcher.cs`: `_olog` resuelto en ctor DI (`GetService<ILoggerService>`); ctor manual `_olog=null` (no-op); `Event_Published` (inicio `DispatchAsync`), `Event_Handled` (1/handler éxito), `Event_Failed` (paralelo+secuencial+timeout); emisión única en `DispatchAsync`.
- **Fix extra pre-existente (dentro del archivo autorizado)**: `GetHandlerDelegate` reescrito — `Delegate.CreateDelegate` nunca podía bindear `HandleAsync(TEvent,...)` fuertemente tipado (ArgumentException). Reemplazado por helper genérico `CreateHandlerDelegateCore<TEvent>()` + `_createHandlerDelegateMethod` + `MakeGenericMethod`. **Sin este fix ningún handler real se ejecutaba** (silencioso vía catch `EVENT_HANDLER_ERROR`).

### F4 — Build + tests ✅
- `dotnet build PassPlat.slnx`: 0 errores (2 warnings pre-existentes NU1603).
- Tests S17 T1–T6: **6/6 PASS**.
- Suite total: **76/76 PASS** (70 baseline + 6 nuevos) en 55s.

### F5 — Grounding DI ✅
- `AddCbpLogging` registra `ILoggerService` **singleton** (3 variantes).
- `JwtTokenService` singleton (`AddSingleton<IJwtTokenService, JwtTokenService>`); `DomainEventDispatcher` scoped; `EventDispatcherConfiguration` singleton.
- Program.cs: `AddCbpLogging` (L32) + `AddJwtOperator` (L50) — sin mismatch de lifetime.
- Ctor JWT 3-params (options, ILogger, ILoggerService?) resuelto por DI (MS DI elige mayor ctor).

### F6 — Evidencia en vivo ✅
- API real en `http://localhost:5000` (binario publicado; puertos launch 5001/5259 no usados; `UseHttpsRedirection` desactivado en Development — no redirect 307).
- **Emitidos en vivo con properties JSON**:
  - `Jwt_Validated` ✅ (GET `/api/apps` Bearer → 200, properties scope=authentication, category=application.auth, operation=Validate, correlationId W3C).
  - `Jwt_Generated`, `Login_Succeeded` ✅ (login platform + tenant).
  - `Background_JobStarted`/`Background_JobFinished` ✅ (jobs al arranque).
- **Event_* — resuelto por S18** (NO defecto de CBP): el flujo trigger `trigger-new-ip` no publicaba por heurística `esNueva` no determinista + IP fija `10.0.0.99` (2ms de diferencia entre `FecPrimerUso` y `UltUso` en fila recién creada). Publicación certificada en vivo mediante flujo incondicional.
  - `Event_Published` ✅ en vivo (POST `/api/dispconfiables/revocar-confianza/3/1`, log `PassPlat.WebAPI\Logs\passplat-20260810.log` 17:03:29, scope=domainEvents, operation=Publish, correlationId W3C).
  - `Event_Handled` ✅ en vivo (`DeviceRevoked por DeviceRevokedEventHandler`, log 17:03:29.980).
  - `Event_Failed` ⚠️ cubierto por pruebas/contrato T5 (no observado en runtime — handler no falló). No se declara E2E certificado.
  - → Ver `S17-F6-EventIP-NoEmitido-Hallazgo.md` (resuelto como diagnóstico) y `S18-Discovery.md`.
  - **Asignación**: la reparación de detección de IP queda como deuda independiente `S19-Fx-IP-DETECTION-DETERMINISTIC`.

### F7 — Documentación ✅
- `Docs/Framework/Logging/Logging.EventCatalog.md` actualizado → v1.1: `Jwt_*`, `Event_*`, `Background_*`, `Password_Reset` movidos de "Pendientes" a "Emisores (S16.4/S17)"; nota del hallazgo en `Event_*`; `Sql_SlowQuery` único pendiente (persistencia sin interceptor EF).

### F8 — Cierre de sesión / Gate
- **Validación de flujo de login/acceso (íntegro) tras el fix del dispatcher** ✅:
  - `POST /api/auth/login/platform` (platform_admin) → 200 + JWT.
  - `POST /api/auth/login` (admin_abarrotes, tenant ABARROTES) → 200 + JWT + permisos.
  - `GET /api/accesos/usuario/3` (Bearer) → 200.
  - `GET /api/auth/mis-tenants` (Bearer) → 200 (`{"id":2,"codigo":"ABARROTES","nombre":"Abarrotes del Sur"}`).
  - Login/Authz/JWT intactos; no se tocó lógica de negocio de PassPlat.
- **Gate S17**: ✅ **CERRADO formalmente** (`S17-Closure.md`). Build 0 errores, 76/76 tests, reconciliación documental completada. Evidencia en vivo JWT/Background (F6 S17) + Event_* (F6 S17 resuelto por S18).
- **Cierre operativo**: proceso API (PID 32388) fue detenido al terminar la sesión; template `{Properties:j}` en `appsettings.json` temporal pendiente de revertir si se conserva (decisión: **conservar** como instrumentación estándar de observabilidad — validado en Gate C S16.4).

### Pruebas T1–T10
| ID | Estado |
|---|---|
| T1–T6 | ✅ 6/6 PASS |
| T7 (contract 4/4) | ✅ (incluido en 76/76) |
| T8 (build) | ✅ 0 errores |
| T9 (suite) | ✅ 76/76 |
| T10 (E2E) | ✅ PASS — `Event_*` certificado parcialmente en vivo (Published + Handled vía `revocar-confianza`); `Event_Failed` cubierto por pruebas/contrato T5, NO declarado E2E certificado |