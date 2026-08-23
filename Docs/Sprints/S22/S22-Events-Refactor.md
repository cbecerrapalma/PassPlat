# S22 — Framework Architecture Hardening & CBP.Events Refactor

> **Estado**: CLOSED / GATE PASS
> **Fecha**: 2026-08-11
> **Tipo**: Refactor arquitectónico (nomenclatura + dependencias) — sin cambio de comportamiento

---

## 1. Objetivo

Eliminar la nomenclatura heredada `Domain*` del proyecto `CBP.Events`, revisar la
dependencia `CBP.Events → CBP.Results` y validar el grafo de dependencias, preservando
íntegramente el contrato funcional certificado de S21 (Outbox) y el contrato de logging
congelado de S16.4.

**Cadena funcional congelada como contrato de regresión (S21)**:

```
Outbox
  → IEventPublisher
  → EventDispatcher
  → NewIpDetectedEventHandler
  → IEmailQueue
  → EmailBackgroundService
  → EmailLog
```

---

## 2. Cambios aplicados (Antes → Después)

### 2.1 Nomenclatura CBP.Events (S22.1)

| Antes | Después | Tipo |
|-------|---------|------|
| `DomainEventDispatcher` | `EventDispatcher` | clase |
| `IDomainEventDispatcher` | `IEventDispatcher` | interfaz |
| `IDomainEvent` | `CBPEvent` | interfaz |
| `AddDomainEvents()` | `AddCBPEvents()` | método DI |
| `"DOMAIN_EVENT_HANDLING_FAILED"` | `"EVENT_HANDLING_FAILED"` | código de error |
| `DomainEventDispatcher.cs` | `EventDispatcher.cs` | archivo |
| `IDomainEvent.cs` | `CBPEvent.cs` | archivo |
| `DomainEventDispatcherInstrumentationTests` | `EventDispatcherInstrumentationTests` | test S17 |

**Cascada semántica** de `CBPEvent`: `EventBase : CBPEvent`, constraints de
`IEventHandler<TEvent>` y `AddEventHandler<TEvent,THandler>`, firmas de
`DispatchAsync`/`DispatchAllAsync`, `IEventPublisher`/`EventPublisher`, `PipelineExample.cs`,
tests S19 (`It.IsAny<CBPEvent>()` ×11), tests S22 contract.

### 2.2 Lo que NO cambió (por decisión)

- `EventBase`, `IEventPublisher`, `EventPublisher`, `IEventHandler<T>`, `DispatchStrategy`,
  `EventDispatcherConfiguration`/`EventDispatcherMode`, `EntityCreated/Updated/DeletedEvent<TEntity>`.
- Códigos de error sin `Domain`: `EVENT_DISPATCH_ERROR`, `BATCH_EVENT_DISPATCH_FAILED`,
  `EVENT_DISPATCH_CANCELLED`, `EVENT_HANDLER_ERROR`, `EVENT_HANDLER_TIMEOUT`.
- **Contratos de logging congelados (S16.4)**: `LoggingScopes.DomainEvents` /
  `LoggingCategories.DomainEvents` residen en el proyecto `CBP` base (no en `CBP.Events`);
  su valor emitido (`scope=domainEvents`) es contrato de observabilidad certificado y se
  conserva intacto. El refactor no altera la emisión de `Event_Published`/`Event_Handled`/
  `Event_Failed` ni la propagación de `CorrelationId`.

### 2.3 Consumidores actualizados (S22.3)

- `PassPlat.Aplicacion/AplicacionDependencyInjection.cs:119` — `AddDomainEvents()` → `AddCBPEvents()`.
- `PassPlat.Aplicacion.Test/Tests/Framework/S17/EventDispatcherInstrumentationTests.cs` —
  clase renombrada, `new EventDispatcher(...)`, assert `"EVENT_HANDLING_FAILED"`.
- `PassPlat.Aplicacion.Test/Tests/S19/IPServiceDetectionTests.cs` — `It.IsAny<CBPEvent>()` ×11.
- `PassPlat.Aplicacion.Test/Tests/Framework/S17/CapturingLoggerService.cs` — comentario XML.
- Los productores `OutboxProcessor`/`IPService` usan `IEventPublisher` + tipos concretos
  (`NewIpDetectedEvent`, `SecurityAlertEvent`) → **sin cambios funcionales** (solo recompilación).

---

## 3. Dependencias (S22.2)

### 3.1 `CBP.Events → CBP.Results` = **RETAINED** (justificado)

`CBP.Results` es el contrato de retorno de todo el pipeline: `DispatchAsync`,
`DispatchAllAsync`, `PublishAsync`, `IEventHandler<TEvent>.HandleAsync`, más `Error`,
`ErrorType`, `CommonErrors`. Es una capa hoja sin dependencias (no es capa superior);
eliminar la referencia exigiría sustituir el tipo artificialmente (prohibido por regla S22 §15).

```
CBP
 ▲
 │
CBP.Results
 ▲
 │
CBP.Events
```

Sin ciclos (`hasCycles: false`, grafo Roslyn 28 proyectos).

### 3.2 Grafo verificado

- `CBP.Events → [CBP.Results, CBP]` — intacto.
- `CBP.Events` **no depende** de PassPlat / WebAPI / EF Core / SQL / Email / infra específica.
- `CBP.Results → []`, `CBP → []` (hojas).
- `CBP.Logging → CBP`; `CBP.Authentication.*` y `CBP.Data.*` usan `CBP.Results` (sin cambios).

---

## 4. Tests (S22.4)

- **87/87 PASS** (`dotnet test PassPlat.slnx --no-build`):
  - 85 baseline (66 S16 + 6 S17 + 3 CacheLogContract + 10 S19/… según estado previo) — **sin regresión**.
  - **+2 contract tests** en `PassPlat.Aplicacion.Test/Tests/Framework/S22/EventContractTests.cs`:
    - `T1_AddCBPEvents_Registers_Scoped_Dispatcher_And_Publisher` — resolución DI scoped de
      `IEventDispatcher`/`IEventPublisher` post-rename.
    - `T2_Publish_Dispatches_Handler_With_CorrelationId` — publish → dispatch → handler
      con `scope=domainEvents` y propagación de `CorrelationId`.

- **Build**: `dotnet build PassPlat.slnx` → **0 errores, 0 warnings nuevas** (3 CS8602
  pre-existentes en ConfProvIden — ajenas a S22).

---

## 5. Documentación (S22.5)

| Doc | Cambio |
|-----|--------|
| `AGENTS.md` | Este resumen S22 añadido; nombres actualizados donde representan código vivo |
| `Docs/Sprints/S21/S21-Outbox-Implementation.md` | `DomainEventDispatcher` → `EventDispatcher`, `AddDomainEvents` → `AddCBPEvents` (solo nombres actuales) |
| `Docs/Framework/Logging/Logging.EventCatalog.md` | Emisor `DomainEventDispatcher` → `EventDispatcher` + entrada de cambio S22 |
| Docs históricos S15–S18 | Referencias históricas **conservadas** (excepción aprobada del gate S22.6) |

---

## 6. Validación obsoleta (S22.6)

Gate: **0 referencias obsoletas en código funcional** de `CBP.Events`, PassPlat y tests
activos, exceptuando:
- Contratos de logging congelados (`LoggingScopes.DomainEvents`, `LoggingCategories.DomainEvents`).
- Historial documental histórico (S15–S18).

Resultado grep (`IDomainEvent\b|IDomainEventDispatcher|DomainEventDispatcher|AddDomainEvents|DOMAIN_EVENT_HANDLING_FAILED`)
sobre `*.cs` (excl. bin/obj): **0 coincidencias**.

---

## 7. Regression S21 (S22.9)

La cadena funcional S21 se preservó sin cambios de comportamiento. Verificación en vivo
(S21.4/S21.5/S21.6) documentada en la sección de ejecución del gate — todos **PASS**,
incluida la propagación del `CorrelationId` W3C y la emisión `Event_Published`/`Event_Handled`
con `scope=domainEvents`.

Evidencia concreta de la corrida de certificación (API `http://localhost:5259`, cuenta `admin_abarrotes`,
IPs TEST-NET `203.0.113.x`):

| Gate | Resultado | Evidencia |
|------|-----------|-----------|
| S21.4 Concurrency | ✅ PASS | 2 POST concurrentes a `203.0.113.61` → `queued:true`/`queued:false`; IPs Id22 única (FecPrimerUso=UltUso); Outbox Id16 `published` (CreatedAt 03:42:00 → ProcessedAt 03:42:10 UTC); EmailLog Id26 `enviado`, correlation `00-f60fc880...` |
| S21.5 Crash/Idempotency | ✅ PASS | Crash sim `UPDATE Outbox SET Status='processing', ProcessingStartedAt=DATEADD(second,-400,GETUTCDATE()) WHERE Id=16` → ResetStale → re-claim → dedup `NewIp dedup: notificacion ya existe... omitiendo publicacion` (log 23:43:55 local) → sin EmailJob duplicado; Outbox Id16 `published`, ProcessedAt 03:43:55.793, Attempts=0 |
| S21.6 E2E cadena | ✅ PASS | IP `203.0.113.62` → IPs Id23 · Outbox Id17 `published` (03:47:41→03:47:55) · EmailLog Id27 `enviado` (trk-4ef7f312); cadena `Event_Queued→Event_Published→Email_Queued→Event_Handled→Email_Sent` con correlationId `00-62c62d7f...` propagado request→worker→handler→SMTP |


---

## 8. Build y tests finales

| Gate | Resultado |
|------|-----------|
| S22.1 Refactor CBP.Events | ✅ Aplicado (0 residuos `Domain` en código) |
| S22.2 Dependencia CBP.Results | ✅ RETAINED — sin ciclos |
| S22.3 Consumidores | ✅ Actualizados |
| S22.4 Contract + regression tests | ✅ 2 nuevos contract tests, suite sin regresión |
| S22.5 Documentación | ✅ Creada/actualizada |
| S22.6 Obsolete-reference gate | ✅ 0 referencias obsoletas |
| S22.7 Build | ✅ 0 errores |
| S22.8 Tests | ✅ 87/87 PASS |
| S22.9 S21 regression | ✅ 3/3 PASS |
| **FINAL GATE** | ✅ **CLOSED / GATE PASS** |

---

## 9. Archivos relevantes

| Archivo | Rol |
|---------|-----|
| `CBP/CBP.Core/CBP.Events/EventDispatcher.cs` | Antes `DomainEventDispatcher.cs`; clase `EventDispatcher : IEventDispatcher` |
| `CBP/CBP.Core/CBP.Events/CBPEvent.cs` | Antes `IDomainEvent.cs`; `CBPEvent`, `IEventHandler<TEvent>`, `IEventDispatcher` |
| `CBP/CBP.Core/CBP.Events/IEventPublisher.cs` | `IEventPublisher`/`EventPublisher` delegando a `IEventDispatcher` |
| `CBP/CBP.Core/CBP.Events/EventBase.cs` | `EventBase : CBPEvent` |
| `CBP/CBP.Core/CBP.Events/DependencyInjection/EventServiceCollectionExtensions.cs` | `AddCBPEvents()`, `AddEventHandler<>`, `AddEventHandlersFromAssembly` |
| `CBP/CBP.Core/CBP.Events/Examples/PipelineExample.cs` | Ejemplo con `CBPEvent` |
| `PassPlat.Aplicacion/AplicacionDependencyInjection.cs` | `AddCBPEvents()` |
| `PassPlat.Aplicacion.Test/Tests/Framework/S17/EventDispatcherInstrumentationTests.cs` | Tests S17 renombrados |
| `PassPlat.Aplicacion.Test/Tests/Framework/S22/EventContractTests.cs` | 2 contract tests S22 (nuevo) |
| `PassPlat.Aplicacion.Test/Tests/S19/IPServiceDetectionTests.cs` | `It.IsAny<CBPEvent>()` ×11 |
| `Docs/Sprints/S21/S21-Outbox-Implementation.md` | Nombres actualizados |
| `Docs/Framework/Logging/Logging.EventCatalog.md` | Emisor + changelog S22 |
| `Docs/Evidence/s21-gates-20260811.log` | Evidencia regression S21 (sin cambios) |
| `AGENTS.md` | Resumen S22 añadido; líneas S21 actualizadas (AddCBPEvents) |

---

## 10. Deudas no bloqueantes

- Las 3 warnings `CS8602` pre-existentes en `CrearConfProvIdenValidator.cs` y
  `ConfProvIdenService.cs` — ajenas a S22, backlog existente.
- La deuda de S21 (`S19-Fx-IP-DETECTION-DETERMINISTIC`, heurística `esNueva`) permanece
  vigente e independiente de S22.

