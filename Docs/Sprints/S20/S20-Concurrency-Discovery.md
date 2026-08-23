# S20 — Concurrency Discovery & Implementation Design: Outbox for NewIpDetectedEvent

## Status: S21 IMPLEMENTED & CERTIFIED (S21.4 · S21.5 · S21.6 = PASS, 2026-08-11)

This document is the **S21.0 Architecture & Implementation Design** — READ-ONLY discovery of how an Outbox pattern could be implemented for `NewIpDetectedEvent` to resolve the race condition confirmed in S20.7.

---

## S21.0.1 — Estado S20 (Contexto)

### S19 = CLOSED / GATE PASS (2026-08-10)

- Deterministic `IPRepository.ObtenerOCrear` → `IPRegistro(Entidad, EsNueva)` based on real existence check
- Build: 0 errors · xUnit: 85/85 PASS · E2E PASS
- Registry: `Docs/Sprints/S19/S19-Sprint-Registry.md`

### S20.7 = PASS (Concurrency Experiment Against SQL Server)

- **IP test**: `203.0.113.57` (TEST-NET-3, pre-verified inexistent)
- **SQL Server**: SQL Server 2022 (Real — not InMemory)
- **Result**:
  - Request A (Id=4265): HTTP 200, `EsNueva=true`, `PublishAsync=SUCCESS`, `SaveChangesAsync=INSERT OK`
  - Request B (Id=4266): HTTP 500, `EsNueva=true`, `PublishAsync=SUCCESS`, `SaveChangesAsync=FAILED` (`DbUpdateException` — `UQ_IPs_Direccion`)
  - IP rows created: **1** · `NewIpDetectedEvent` published: **2** · EmailQueue attempts: **2**
  - Evidence: `Docs/Evidence/s20-concurrency-sqlserver.log`

### S20.8 = READ-ONLY Discovery (This Document)

---

## S21.0.2 — Caso de uso y causa raíz

### Flujo actual (S21.0.3 — IP Flow Inspection)

```
DispConfiablesController.TriggerNewIp (L74-82)
  → IPService.DetectarNuevaIPAsync(idUsuario, idTenant, direccionIP, ...)
    → _repo.ObtenerOCrear(direccionIP)        ← SYNC: determina EsNueva, DbSet.Add (no persiste)
    → _eventPublisher.PublishAsync(evt)       ← ASYNC: publica evento ANTES de commit
    → return Success
  → _uow.SaveChangesAsync()                   ← En Controller (L80), DESPUÉS del publish
```

#### Componentes inspeccionados

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `IPRepository.ObtenerOCrear` | `PassPlat.Datos\Repositories\IPRepository.cs:L35-54` | SYNC. Usa `DbSet.FirstOrDefault` para verificar existencia. Determina `EsNueva`. Llama `DbSet.Add` pero **no persiste** (no SaveChanges). |
| `IPService.DetectarNuevaIPAsync` | `PassPlat.Aplicacion\Services\BBDD\IPService.cs:L58-102` | Llama `ObtenerOCrear` (sync) → `PublishAsync` (line 90) → return. **SaveChangesAsync NO está en el service**. |
| `DispConfiablesController.TriggerNewIp` | `PassPlat.WebAPI\Controllers\DispConfiablesController.cs:L74-82` | Llama `ipService.DetectarNuevaIPAsync` → `_uow.SaveChangesAsync()` (L80). **PublishAsync ocurre antes de SaveChangesAsync**. |
| `IP` entity | `PassPlat.Dominio\Entities\Contexto\IP.cs` | `Id` (int PK), `Direccion` (string). `UX_IPs_Direccion` = índice único sobre `Direccion`. |
| `NewIpDetectedEvent` | `PassPlat.Aplicacion\Services\Security\IPEvents.cs:L5-17` | `record` heredando de `EventBase`. Identity fields: `IdUsuario`, `IdTenant`, `DireccionIP`, `IdIP`. |
| `NewIpDetectedEventHandler` | `PassPlat.Aplicacion\Services\Security\IpEventHandlers.cs:L7-44` | Llama `IEmailQueue.EnqueueAsync(EmailJob)`. No deduplica. No persiste. |
| `DomainEventDispatcher` | `CBP\CBP.Core\CBP.Events\DomainEventDispatcher.cs` | In-line dispatch (sync o paralelo). **No persiste eventos. No hay post-commit hook. No deduplicación.** |

### Causa raíz (CONFIRMADA en S20.7)

```
PublishAsync (IPService) → antes de → SaveChangesAsync (Controller)
```

1. Request A: `ObtenerOCrear` → IP no existe → EsNueva=true → `PublishAsync` → Controller `SaveChangesAsync` → INSERT OK → 1 fila
2. Request B (concurrente): `ObtenerOCrear` → IP no existe (sin lock) → EsNueva=true → `PublishAsync` → Controller `SaveChangesAsync` → `DbUpdateException` (UQ_IPs_Direccion) → HTTP 500

**Evento publicado 2 veces** (ambos requests publican antes del commit).

---

## S21.0.3 — Inspección de infraestructura existente (S21.0.6)

### UnitOfWork / Transacciones

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `IUnitOfWorkAsync` | `CBP\CBP.Data\CBP.Data.Abstractions\IUnitOfWork.cs:L42-70` | Expone `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackTransactionAsync`, `ExecuteInTransactionAsync`. |
| `UnitOfWorkAsync` | `CBP\CBP.Data\CBP.Data.Asynchronous\UnitOfWorkAsync.cs` | `SaveChangesAsync` (L59-63) llama directamente a `_context.SaveChangesAsync`. `ExecuteInTransactionAsync` (L138-183) crea transacción con `CreateExecutionStrategy` (retry). **Prohíbe anidar transacciones** (L151-153). |
| **Estado actual**: No hay `ExecuteInTransactionAsync` en IP flow. Controller llama `SaveChangesAsync` directamente. |

#### Determinación
- ✅ `ExecuteInTransactionAsync` podría envolver `DbSet.Add(IP) + DbSet.Add(Outbox)` en transacción.
- ✅ `CreateExecutionStrategy` provee retry automático.
- ❌ No hay post-commit hook (no hay `AfterCommit` callback en EF Core ni en CBP).

### EF Core / DbContext

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `PassPlatDbContext` | `PassPlat.Datos\PassPlatDbContext.cs:L73-77` | `ApplyConfigurationsFromAssembly`. **No SaveChanges interceptors**. |
| `IPConfiguration` | `PassPlat.Datos\Configurations\Contexto\IPConfiguration.cs` | `UX_IPs_Direccion = HasIndex(Direccion).IsUnique()` (L23). |

#### Determinación
- ❌ No hay `SaveChangesInterceptor` → no hay punto de gancho para domain events post-commit.
- ❌ No hay `DbContext.SaveChangesAsync` sobrecargado.

### CBP.Events / Event Publisher

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `IEventPublisher` | `CBP\CBP.Core\CBP.Events\IEventPublisher.cs:L9-23` | `PublishAsync(IDomainEvent, CancellationToken)`. |
| `EventPublisher` | `CBP\CBP.Core\CBP.Events\IEventPublisher.cs:L25-52` | Delega a `IDomainEventDispatcher.DispatchAsync`. |
| `DomainEventDispatcher` | `CBP\CBP.Core\CBP.Events\DomainEventDispatcher.cs` | In-line dispatch. **`EmitEventPublished(L47)`** → `EmitEventOutcome`/`EmitEventFailed`. No persistencia. No retry. No deduplicación. |
| `IDomainEvent` | `CBP\CBP.Core\CBP.Events\IDomainEvent.cs:L5-11` | `EventId`, `OccurredOn`, `CorrelationId`, `EventType`. |
| `EventBase` | `CBP\CBP.Core\CBP.Events\EventBase.cs:L3-18` | `Record` base. `CorrelationId` genérico si no se pasa. |

#### Determinación
- ❌ CBP.Events NO tiene soporte transaccional.
- ❌ `PublishAsync` es fire-and-forget dentro del handler.
- ❌ No hay idempotencia/deduplicación de eventos.

### CBP.Emails / EmailQueue

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `EmailQueue` | `PassPlat.Aplicacion\Services\Email\EmailQueue.cs:L71-115` | `Channel<EmailJob>` en memoria (bounded 1024, `SingleReader=true`). **No persiste**. **No deduplica**. **Se pierde en restart**. |
| `EmailJob` | `PassPlat.Aplicacion\Services\Email\EmailQueue.cs:L53-62` | `record` con `Kind`, `ToEmail`, `UserName`, `Extra`, `IdTenant`, `IdUsuario`, `IdApp`, `CorrelationId`, `EmailLogId`. |
| `EmailBackgroundService` | `PassPlat.Aplicacion\Services\Email\EmailBackgroundService.cs` | 2 consumers: `Channel.Reader` (queue) + `PeriodicTimer` → `PollPendingEmailsAsync` (L163). **Recovery via `EmailLog` table** (pending → retry). `RetryDelays=[1min, 5min, 15min]`, `MaxRetries=3`. |
| `EmailLog` | `PassPlat.Dominio\Entities\Core\EmailLog.cs` | `Id` (long), `Estado` (string), `Intentos` (byte), `CorrelationId`, `ExtraJson`. **EmailLog es persistente** pero no deduplica eventos. |

#### Determinación
- ✅ `EmailBackgroundService` tiene **recovery post-crash** vía `EmailLog` table (poll de pendientes).
- ❌ `EmailQueue` (Channel) **NO es Outbox** — es memoria volátil.
- ⚠️ `EmailLog` existe como tabla persistente pero no se usa para deduplicación de eventos.

### Infraestructura distribuida / Background Services

| Component | Archivo | Hallazgo |
|-----------|---------|----------|
| `SqlDistributedLockService` | `PassPlat.Aplicacion\Services\SqlDistributedLockService.cs` | `sp_getapplock` (Exclusive, Transaction-scoped). **Existe pero NO se usa en IP flow**. |
| Background Services | `PassPlat.Aplicacion\Services\Email\EmailBackgroundService`, `PasswordExpirationBackgroundService` | 2 registered (`BackgroundService`). No outbox worker. |

#### Determinación
- ✅ `SqlDistributedLockService` podría usarse para serializar (no recomendado — throughput).
- ❌ No hay outbox worker (`IBackgroundService` que publique desde DB).

### Persistencia de entidad

| Component | Hallazgo |
|-----------|----------|
| `IDbSet<EmailLog>` | En `PassPlatDbContext:L47` → existe tabla `EmailLog`. |
| `IEmailLogRepository` | En `PassPlat.Datos\Repositories\EmailLogRepository.cs` → `ObtenerPendientesAsync`, `GuardarAsync`, etc. |

#### Determinación
- ✅ `EmailLogRepository` existe con métodos de query → podría servir para recovery.
- ❌ No hay `IOutboxRepository` o similar.

---

## S21.0.7 — Búsqueda de infraestructura Outbox existente (S21.0.6 — confirmación)

Búsqueda exhaustiva en `D:/CODIGOS` (`.cs` files):

```
Pattern: Outbox|OutboxMessage|IntegrationEvent|PendingEvent|ProcessedEvent
Resultado: No files found
```

#### Determinación definitiva
- ❌ **NO existe infraestructura Outbox** en el repositorio.
- ❌ No hay tabla, entidad, repositorio, worker, ni interceptor.
- ❌ No hay `IHostedService` de outbox.
- ❌ No hay `SaveChangesInterceptor` que capture domain events.

---

## S21.0.8 — Diseño conceptual del Outbox (S21.0.7 — sin implementar)

### Entidad Outbox (diseñada)

Campos mínimos:

| Field | Tipo | Comentario |
|-------|------|------------|
| `Id` | `long` (PK, bigint) | Identity auto-generado |
| `EventType` | `string` | e.g., `"NewIpDetected"` |
| `Payload` | `string` (nvarchar(max)) | JSON-serializado del evento (IdUsuario, IdTenant, DireccionIP, etc.) |
| `CorrelationId` | `string` (nvarchar(64)) | Propagado desde request |
| `IdTenant` | `int?` (FK) | Para filtering por tenant en worker |
| `IdUsuario` | `int?` (FK) | Para contexto de usuario |
| `Status` | `string` (nvarchar(20)) | `"pending"`, `"published"`, `"failed"` |
| `Attempts` | `int` | Contador de reintentos |
| `CreatedAt` | `DateTime` | Sysdatetime |
| `ProcessedAt` | `DateTime?` | NULL hasta publicado |
| `LastError` | `string?` (nvarchar(max)) | Error detallado |
| `NextAttemptAt` | `DateTime?` | Para retry scheduling |

#### Tabla SQL (diseñada)

```sql
CREATE TABLE Outbox (
    Id               bigint IDENTITY(1,1) PRIMARY KEY,
    EventType        nvarchar(100)      NOT NULL,
    Payload          nvarchar(MAX)      NOT NULL,
    CorrelationId    nvarchar(64)       NOT NULL,
    IdTenant         int                NULL,
    IdUsuario        int                NULL,
    Status           nvarchar(20)       NOT NULL DEFAULT 'pending',
    Attempts         int                NOT NULL DEFAULT 0,
    CreatedAt        datetime2(3)       NOT NULL DEFAULT sysdatetime(),
    ProcessedAt      datetime2(3)       NULL,
    LastError        nvarchar(MAX)      NULL,
    NextAttemptAt    datetime2(3)       NULL
);

CREATE NONCLUSTERED INDEX IX_Outbox_Pending
ON Outbox (Status, CreatedAt)
INCLUDE (Id)
WHERE Status = 'pending';
```

#### Persistencia dentro de transacción (design)

```csharp
// IPService.DetectarNuevaIPAsync (redesigned)
using var tx = await _uow.BeginTransactionAsync(ct);
try
{
    var repoResult = _repo.ObtenerOCrear(direccionIP, ...);
    var saveResult = await _uow.SaveChangesAsync(ct);  // persiste IP + Outbox en una transacción

    if (repoResult.Value.EsNueva && saveResult.IsSuccess)
    {
        var outbox = Outbox.Crear(evt);  // serializa evento
        _dbContext.Outbox.Add(outbox);
        await _uow.SaveChangesAsync(ct);

        await _uow.CommitTransactionAsync(ct);
    }
    else
    {
        await _uow.RollbackTransactionAsync(ct);
        // IP ya existía o INSERT falló → no se persiste Outbox
    }
}
catch
{
    await _uow.RollbackTransactionAsync(ct);
    throw;
}
```

#### Worker de publicación (OutboxProcessor)

```csharp
// NEW: PassPlat.Aplicacion\Services\Email\OutboxProcessor.cs
// BackgroundService que:
// 1. Poll: SELECT TOP(100) * FROM Outbox WHERE Status='pending' ORDER BY CreatedAt
// 2. PublishAsync cada evento (retry con backoff: 1min → 5min → 15min, MaxRetries=3)
// 3. UPDATE Status='published'/'failed' + ProcessedAt
// 4. Requeue failed: UPDATE NextAttemptAt = DATEADD(MINUTE, delay, GETDATE())
```

#### Identity funcional y deduplicación

**Identity funcional**: `DireccionIP + EventType=NewIpDetected + creación efectiva`

**NO usar CorrelationId como key**: 2 requests concurrentes pueden tener CorrelationId distintos.

**Garantía de unicidad**: El `INSERT` de IP con `UX_IPs_Direccion` determina quién es el creador. Solo el request que gana el INSERT persiste el Outbox row.

---

## S21.0.9 — Comportamiento esperado bajo concurrencia (S21.0.9)

### Caso de referencia: S20.7 (2 requests concurrentes → misma IP nueva)

#### Flujo actual (BUG)
```
Request A: SELECT(no existe) → EsNueva=true → PublishAsync → INSERT(OK) → SaveChanges(OK)
Request B: SELECT(no existe) → EsNueva=true → PublishAsync → INSERT(Fail: UQ) → SaveChanges(Fail)
Resultado: 1 IP, 2 eventos, 2 emails
```

#### Flujo con Outbox (ESPERADO)
```
Request A: BeginTransaction
            → SELECT(no existe)
            → INSERT IP (OK) ← gana la carrera
            → INSERT Outbox row (Status=pending)
            → SaveChanges(COMMIT) ← IP + Outbox persistidos juntos
            → CommitTransaction
            → (publish es ASYNC vía worker)

Request B: BeginTransaction
            → SELECT(no existe) ← todavía no existe (sin lock)
            → INSERT IP (FAIL: UX_IPs_Direccion) ← DbUpdateException
            → RollbackTransaction ← todo revertido, OUTBOX también revertido
            → HTTP 500 (o retry por strategia)

Worker (OutboxProcessor):
            → SELECT pending (1 row)
            → PublishAsync(NewIpDetectedEvent) ← 1 evento solo
            → UPDATE Status=published
```

**Resultado esperado**: 1 IP creada, 1 Outbox row, 1 evento publicado, 1 email. ✅

**Nota**: Request B puede recibir `DbUpdateException` si no usa retry strategy. Con `CreateExecutionStrategy` (ya disponible en `UnitOfWorkAsync.ExecuteInTransactionAsync`), EF Core reintentaría automáticamente.

---

## S21.0.10 — Failure scenarios (S21.0.10)

### Caso 1: IP insertada + Outbox insertada + COMMIT

```
DB: IP creada + Outbox row (Status=pending)
Worker: publica evento → EmailQueue → EmailBackgroundService
Resultado: Email enviado ✅
```

### Caso 2: IP insertada + Outbox falla (SaveChangesAsync falla)

```
DB: IP insertada + Outbox row NO insertada (transacción = rollback)
Resultado: No hay IP efectivamente creada (rollback atómico) → No hay evento ✅
```

### Caso 3: Outbox COMMIT realizado + PublishAsync falla (handler)

```
DB: Outbox row (Status=pending) persistida
Worker: PublishAsync → NewIpDetectedEventHandler → EmailQueue.EnqueueAsync falla
Worker: UPDATE Status=failed, Attempts=1, NextAttemptAt=now+1min
Resultado: Retry → evento publicado en próximo ciclo ✅
```

### Caso 4: Worker falla después de publicar

```
DB: Outbox row (Status=pending) persistida
Worker: PublishAsync = SUCCESS → handler ejecutado → EmailQueue.EnqueueAsync
Worker: CRASH antes de UPDATE Status=published
Resultado: Email colocado en EmailQueue (Channel en memoria) → PERDIDO en restart
Worker: Al restart, reprocesa Outbox row (Status=pending) → DUPLICATE event ❌
```

**Mitigación**: Worker debe usar **idempotencia en handler** (ver Análisis alternativas) o persistir EmailQueue a DB.

### Caso 5: Worker se reinicia

```
DB: Outbox rows (Status=pending) persistidas
Worker: al restart, poll de `IX_Outbox_Pending` → reprocesa rows pendientes
Resultado: Recovery ✅ (siempre que handler sea idempotente — ver Caso 4)
```

### Caso 6: Dos workers procesan simultáneamente el mismo Outbox row

```
Worker A: SELECT TOP(100) pending → incluye row #5
Worker B: SELECT TOP(100) pending → incluye row #5 (misma fila)

Solución: UPDATE con SET Status='processing' WHERE Id=5 AND Status='pending'
(optimistic lock) → solo uno gana → el otro salta la fila
```

### Caso 7: Email handler falla (EmailQueue.EnqueueAsync)

```
Worker: PublishAsync → NewIpDetectedEventHandler → EmailQueue.EnqueueAsync falla
Worker: Result.Failure("NOTIFY_ERROR") → UPDATE Status=failed
Resultado: Retry → el evento no se publica hasta que el handler funcione ✅
```

### Caso 8: Reenvío de request HTTP (retry client)

```
Request POST /trigger-new-ip → timeout client → reenvía
Request A: INSERT IP (OK) + Outbox (COMMIT)
Request B: SELECT (existe) → EsNueva=false → No Outbox
Resultado: 1 IP, 1 evento ✅ (determinismo por UQ constraint)
```

---

## S21.0.11 — Matriz comparativa final (A/C/D/E)

| Criterion | A: Persistir primero | C: SQL atómico (MERGE) | D: Outbox Pattern | E: Idempotencia |
|-----------|---------------------|----------------------|-------------------|-----------------|
| **Race eliminación** | ⚠️ Parcial (SaveChanges determina, pero PublishAsync antes) | ✅ SÍ (MERGE determina creador) | ✅ SÍ (IP+Outbox en transacción) | ⚠️ Parcial (dedup, pero DB race sigue) |
| **Atomicidad DB+evento** | ❌ NO (2 comandos no atómicos) | ⚠️ Parcial (DB atómico, evento después) | ✅ SÍ (INSERT IP + Outbox en tx) | ❌ NO |
| **Publish failure (handler falla)** | ❌ Evento perdido | ❌ Evento perdido | ✅ Worker reintenta (Status=failed) | ✅ Handler dedup/reintenta |
| **Retry/crash recovery** | ❌ Si crash post-commit → evento perdido | ❌ Sin recovery | ✅ Worker reprocesa Outbox pendientes | ✅ Retry idempotente |
| **Performance (latencia)** | ✅ Mínima | ✅ Latencia INSERT corta | 🟡 Overhead fila extra + worker poll | ✅ Overhead mínimo |
| **Performance (throughput)** | ✅ Alto | ⚠️ Medio (MERGE hold locks) | ✅ Alto (outbox desacoplado) | ✅ Alto |
| **Complejidad implementación** | ⚪ Baja | 🟡 Media | 🟡🟡 Alta | 🟡 Media |
| **Cambios SQL** | 0 | MERGE, OUTPUT | Tabla Outbox + índice | 0 |
| **Cambios EF** | 1 método (IPService invertido) | 1 método (repo cambia) | DbContext+transaction+entity | Handler/queue |
| **Cambios App (.cs)** | IPService + Controller | IPRepository | IPService + Worker + Entity | Handler/queue |
| **Cambios infra (worker)** | 0 | 0 | NEW BackgroundService | 0 |
| **Breaking changes** | ❌ Controller invertido (SaveChanges en service = patrón arquitectónico viola "commit from consumer") | ⚠️ Repo cambia (no breaking en API) | ❌ Nueva tabla + entity + worker | ⚠️ Handler logic |
| **Compatibilidad CBP** | ✅ Usa CBP.Events/IUnitOfWork | ✅ Usa RawQuery | ✅ Usa IUnitOfWork transacciones + BackgroundService | ⚠️ Necesita dedup key |
| **Observabilidad** | ⚠️ Evento antes de commit → timing confuso | ⚠️ Evento después de MERGE | ✅ Status/publishedAt/LastError en Outbox | ✅ Handler metrics |
| **Reutilización futura** | ❌ Solo IP/NewIp | ❌ Solo IP/NewIp | ✅ Outbox para TODO evento | ⚠️ Solo handlers con dedup |

### Evaluación por escenario (8 escenarios)

| Scenario | A | C | D | E |
|----------|---|---|---|---|
| S1: 2 requests concurrentes | ⚠️ 1 evento (gateado por Error) | ✅ 1 evento | ✅ 1 evento | ⚠️ 1 evento efectivo |
| S2: IP ya existe | ✅ 0 eventos | ✅ 0 eventos | ✅ 0 eventos | ✅ 0 eventos |
| S3: PublishAsync falla | ❌ evento perdido | ❌ evento perdido | ✅ retry | ✅ retry |
| S4: Crash post-commit | ❌ evento perdido | ❌ evento perdido | ✅ recovery | ❌ evento perdido |
| S5: Reenvío request | ✅ 0 eventos (deterministic) | ✅ 0 eventos | ✅ 0 eventos | ✅ 0 eventos |
| S6: Email notificación | ✅ EmailBackgroundService retry | ✅ | ✅ | ✅ |
| S7: Lock escalation | ⚠️ SELECT+INSERT lock | ⚠️ MERGE hold | ✅ INSERT lock only | ✅ SELECT+INSERT |
| S8: Worker concurrency | n/a | n/a | ⚠️ necesita optimistic lock | n/a |

### Recomendación técnica (S21.0.11)

| Prioridad | Recomendación | Justificación |
|-----------|---------------|---------------|
| **1** | **D: Outbox Pattern** | Única opción que resuelve atomicity DB+evento + crash recovery + publish failure. Trade-off: complejidad alta, pero es la solución arquitectónica correcta y reutilizable para TODOS los eventos. |
| **2** | **C+D (combo)**: MERGE + Outbox | MERGE elimina carrera DB a nivel motor; Outbox garantiza atomicity evento. Pero es redundante (Outbox ya persiste dentro de transacción con SaveChanges). |
| **3** | **A+E (combo)**: Persistir primero + idempotencia | Mínima invasión. No resuelve atomicity ni crash recovery, pero elimina eventos duplicados. ⚠️ Violates arquitectura (SaveChangesAsync en service). |
| **4** | **E (idempotencia only)** | No resuelve el problema raíz (DB race, crash recovery). |
| **5** | **B: Transacción larga** | NO RECOMENDADO — PublishAsync dentro de transacción = dead lock/timeout, worst performance. |

---

## S21.0.12 — Arquitectura recomendada (S21.0.12)

### Flujo propuesto (Outbox Pattern)

```
Request (TriggerNewIp)
  → IPService.DetectarNuevaIPAsync
    → _repo.ObtenerOCrear(direccionIP)        ← SYNC: determina existencia + EsNueva, DbSet.Add(IP)
    → [NEW] _dbContext.Outbox.Add(Outbox.Crear(evt))  ← persiste evento como fila
    → return (sin PublishAsync)
  → _uow.SaveChangesAsync()                  ← Controller: COMMIT de IP + Outbox juntos
  → HTTP 200

--- Async ---

OutboxProcessor (BackgroundService)
  → SELECT TOP(100) FROM Outbox WHERE Status='pending' (optimistic lock)
  → for each: _eventPublisher.PublishAsync(evt)
    → DomainEventDispatcher → NewIpDetectedEventHandler → EmailQueue.EnqueueAsync
  → UPDATE Outbox SET Status='published'/'failed', ProcessedAt, Attempts, LastError
  → Retry failed: NextAttemptAt = now + backoff
```

### Componentes afectados

| Component | Rol | Acción |
|-----------|-----|--------|
| `IP` entity | IP record | NO CHANGE |
| `Outbox` entity | **NUEVO** | `PassPlat.Dominio\Entities\Core\Outbox.cs` |
| `OutboxConfiguration` | **NUEVO** | `PassPlat.Datos\Configurations\Core\OutboxConfiguration.cs` |
| `OutboxRepository` | **NUEVO** | `PassPlat.Datos\Repositories\OutboxRepository.cs` |
| `IPRepository` | IP CRUD | NO CHANGE (ObtenerOCrear determina EsNueva) |
| `IPService` | Event logic | **CAMBIO**: elimina `PublishAsync`, agrega `Outbox.Add` |
| `DomainEventDispatcher` | Event dispatch | NO CHANGE |
| `NewIpDetectedEventHandler` | Email enqueue | NO CHANGE (pero debe ser idempotente — ver nota) |
| `EmailQueue` | In-memory channel | NO CHANGE |
| `EmailBackgroundService` | Email delivery | NO CHANGE |
| `DispConfiablesController` | SaveChanges | NO CHANGE |
| `PassPlatDbContext` | DbContext | **CAMBIO**: `+ DbSet<Outbox>` |

### Componentes que NO deberían modificarse

- ✅ CBP.Events (no hay post-commit hook)
- ✅ CBP.Logging (estructura de logs)
- ✅ CBP.Data (UnitOfWork, Repository, RawQuery)
- ✅ JWT / autenticación
- ✅ Schema SQL existente (`IPs`, `EmailLog`, `UX_IPs_Direccion`)
- ✅ `IEventPublisher` / `DomainEventDispatcher`
- ✅ Tests S19 (no re-ejecutar)
- ✅ `AGENTS.md` (hasta S21.10 Gate)

### Responsabilidades

| Component | Responsible for |
|-----------|-----------------|
| `IPRepository.ObtenerOCrear` | Determinar existencia + EsNueva + `DbSet.Add(IP)` |
| `IPService.DetectarNuevaIPAsync` | Si EsNueva: serializar evento → `DbSet.Add(Outbox row)` |
| `DispConfiablesController` | `SaveChangesAsync` → COMMIT de IP + Outbox juntos |
| `OutboxRepository` | Poll pending rows (con optimistic lock) |
| `OutboxProcessor` | Background: publish → UPDATE status/retry |
| `DomainEventDispatcher` | Dispatch handlers (inline) |
| `NewIpDetectedEventHandler` | Enqueue EmailJob (debe ser idempotent) |

### Transacción

| Component | Transaction |
|-----------|-------------|
| Request flow | `ExecuteInTransactionAsync` o `BeginTransactionAsync` envolviendo IP.Add + Outbox.Add + SaveChangesAsync |
| Worker | No usa transacción DB (PublishAsync puede fallar) — UPDATE de status es individual |

### Persistencia Outbox

- **Entity**: `Outbox` en `PassPlat.Dominio\Entities\Core\Outbox.cs`
- **Table**: `Outbox` (diseñada en S21.0.7)
- **DbContext**: `DbSet<Outbox> Outbox` en `PassPlatDbContext`
- **Repository**: `OutboxRepository` con método `ObtenerPendientesAsync(limit, ct)`
- **Index**: `IX_Outbox_Pending` (Status + CreatedAt) para worker polling
- **Filter**: `WHERE Status = 'pending'` para no reprocesar

### Publicación

- **Worker**: `OUTBOX` (BackgroundService) — poll periódico (15s o configurable)
- **Método**: `_eventPublisher.PublishAsync(evt)` (ya existe en DI)
- **Retry**: backoff 1min/5min/15min (igual EmailBackgroundService), MaxRetries=3
- **Error**: `Status=failed`, `LastError`, `NextAttemptAt`

### Retry

- **DB**: `CreateExecutionStrategy` (ya en `UnitOfWorkAsync.ExecuteInTransactionAsync`) — retry de SaveChangesAsync
- **Worker**: backoff lineal → exponencial en `NextAttemptAt`
- **Email**: `EmailBackgroundService` retry (1min/5min/15min, MaxRetries=3) — ya existe

### Idempotencia

**CRÍTICO**: `NewIpDetectedEventHandler` debe ser idempotente.

- **Key funcional**: `DireccionIP + EventType=NewIpDetected + creación efectiva`
- **NO usar**: `CorrelationId` (2 requests concurrentes pueden tener CorrelationId distintos)
- **Mecanismo**: El `INSERT IP con UX_IPs_Direccion` determina el creador. Solo el creador persiste Outbox. El worker no necesita idempotencia SI el Outbox row es único (1 fila por IP).
- **Edge case**: Si worker publica 2 veces (Caso 4 — crash antes de UPDATE status), el handler necesita idempotencia. Propuesta: `NewIpDetectedEventHandler` verifica si ya existe un `EmailLog` o notificación para la misma `(IdUsuario, DireccionIP, fecha)` antes de encolar EmailJob.

### Concurrencia (S21.0.9)

- **Request**: `BeginTransactionAsync` → IP.Add + Outbox.Add → SaveChangesAsync → CommitTransactionAsync
- **Worker**: SELECT con optimistic lock (`WHERE Id=X AND Status='pending'`) para evitar doble procesamiento
- **Lock contention**: MERGE alternativa mantiene `UPDLOCK,HOLDLOCK`; Outbox mantiene `INSERT` lock corto

### Recuperación ante crash (S21.0.10)

- **Caso 1**: IP + Outbox COMMIT → Worker publica → ✅
- **Caso 2**: IP INSERT falla → Rollback → Outbox no persiste → ✅
- **Caso 3**: Outbox COMMIT + PublishAsync falla → Worker reprocesa → ✅
- **Caso 4**: Worker crash post-publish, pre-UPDATE → Duplicate (mitigación: handler idempotente)
- **Caso 5**: Worker restart → reprocesa `IX_Outbox_Pending` → ✅
- **Caso 6**: 2 workers simultáneos → optimistic lock → 1 gana → ✅
- **Caso 8**: HTTP retry → IP existe → EsNueva=false → No Outbox → ✅

---

## S21.0.13 — Plan de implementación futuro (S21.1–S21.6)

> ✅ **EJECUTADO Y CERTIFICADO (2026-08-11)** — ver sección "S21 Certification" al final de este documento.

### S21.1 — Schema + Entity (✅ CERTIFICADO)
- **Objetivo**: Crear tabla `Outbox` + entidad EF.
- **Componentes afectados**: `Docs/BBDD/S21_Outbox_Schema.sql`, `PassPlat.Dominio\Entities\Core\Outbox.cs`, `PassPlat.Datos\Configurations\Core\OutboxConfiguration.cs`, `PassPlatDbContext`.
- **Tests**: SQL schema validation, EF entity mapping.
- **PASS**: Tabla creada, entity mapeada, 0 warnings EF.

### S21.2 — Repository + Worker (✅ CERTIFICADO)
- **Objetivo**: `OutboxRepository` + `OutboxProcessor`.
- **Componentes afectados**: `PassPlat.Datos\Repositories\OutboxRepository.cs`, `PassPlat.Aplicacion\Services\Infrastructure\OutboxProcessor.cs`, `Program.cs` (DI).
- **Tests**: Poll/query logic, optimistic lock.
- **PASS**: `ObtenerPendientesAsync` funciona, worker encola.

### S21.3 — IPService Integration (✅ CERTIFICADO)
- **Objetivo**: Invertir IPService para usar Outbox.
- **Componentes afectados**: `IPService.cs` (`DetectarNuevaIPConOutboxAsync`, elimina PublishAsync inline), `IPRepository.cs`.
- **Tests**: Unit tests con mock de Outbox (85/85 PASS).
- **PASS**: Evento no publica inline; Outbox row creado.

### S21.4 — Transacción garantizada (✅ CERTIFICADO)
- **Objetivo**: Envolver IP + Outbox en `ExecuteInTransactionAsync`.
- **Componentes afectados**: `DispConfiablesController.cs` (TriggerNewIp), `IPService.cs`.
- **Tests**: Race condition reproduction (SQL Server real, 2 Start-Job concurrentes).
- **PASS**: 2 requests concurrentes → 1 fila IP + 1 Outbox row.

### S21.5 — Idempotencia en punto de encolado (✅ CERTIFICADO)
- **Objetivo**: Deduplicar en `OutboxProcessor.EnqueueEventAsync` vía `EmailLogRepository.ExisteNotificacionNuevaIpAsync` (el worker encola directo a `IEmailQueue`, no pasa por el handler).
- **Componentes afectados**: `OutboxProcessor.cs`, `EmailLogRepository.cs`, `EmailBackgroundService.cs` (resolución de email por `IdUsuario`).
- **Tests**: `IPServiceDetectionTests.cs` T1–T8 + E2E re-trigger (misma IP → `queued:false`, sin EmailLog duplicado).
- **PASS**: No se duplica EmailJob (1 IP → 1 Outbox → 1 EmailLog).

### S21.6 — Certificación (✅ CERTIFICADO)
- **Objetivo**: E2E con IP TEST-NET.
- **Tests**: 2 requests concurrentes → 1 IP + 1 evento + 1 email.
- **PASS**: 0 `DbUpdateException` expuestos, 1 evento lógico.

### Riesgos

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Breaking change en IPService (SaveChanges en Controller) | ⚠️ Medium | Controller ya llama SaveChangesAsync → compatible |
| Caso 4 (duplicate on worker crash) | ⚠️ Medium | Idempotencia en handler (S21.5) |
| Performance (poll 15s) | ✅ Low | Configurable; polling solo cuando IP nueva |
| `UX_IPs_Direccion` causa DbUpdateException | ✅ Low | Con Outbox, rollback atómico → request rechazado limpio |
| Scope creep (Outbox para todos los eventos) | ⚠️ Medium | S21 implementa solo para NewIpDetected; generalizar en S22 |

---

## S21.0.14 — Gate

S21.0 = **READ-ONLY Discovery** → **PASS**

- ✅ Causa raíz confirmada (S20.7).
- ✅ Flujo IP inspeccionado (controller, service, repo, entity, event, handler).
- ✅ Infraestructura existente evaluada (UnitOfWork transacciones, CBP.Events sin post-commit, EmailBackgroundService retry, SqlDistributedLockService).
- ✅ Búsqueda exhaustiva confirma: **NO existe outbox en el repositorio**.
- ✅ Diseño conceptual Outbox definido (entidad, tabla, worker, transacción).
- ✅ 8 failure scenarios analizados.
- ✅ Matriz comparativa A/C/D/E con 9 criterios + 8 escenarios.
- ✅ Recomendación priorizada: **D (Outbox)** como solución arquitectónica > C (MERGE) como mínimo viable.
- ✅ Plan S21.1–S21.6 definido (no ejecutado).

---

## Referencias

| Archivo | Rol | Líneas clave |
|---------|-----|--------------|
| `PassPlat.Datos\Repositories\IPRepository.cs` | `ObtenerOCrear`: determina `EsNueva`, `DbSet.Add` (no persiste) | L35-54 |
| `PassPlat.Aplicacion\Services\BBDD\IPService.cs` | `DetectarNuevaIPAsync`: `PublishAsync` ANTES de SaveChangesAsync | L58-102 (L90 = PublishAsync) |
| `PassPlat.WebAPI\Controllers\DispConfiablesController.cs` | `TriggerNewIp`: `SaveChangesAsync` en controller | L74-82 (L80 = SaveChangesAsync) |
| `PassPlat.Dominio\Entities\Contexto\IP.cs` | Entity IP: `Id`, `Direccion` | L3-31 |
| `PassPlat.Aplicacion\Services\Security\IPEvents.cs` | `NewIpDetectedEvent`: identity = DireccionIP + IdUsuario + IdTenant | L5-17 |
| `PassPlat.Aplicacion\Services\Security\IpEventHandlers.cs` | `NewIpDetectedEventHandler`: `EmailQueue.EnqueueAsync` | L7-44 |
| `PassPlat.Aplicacion\Services\Email\EmailQueue.cs` | `Channel<EmailJob>` en memoria; no persiste; no deduplica | L71-115 |
| `PassPlat.Aplicacion\Services\Email\EmailBackgroundService.cs` | BackgroundService; retry 1/5/15min; recovery vía EmailLog | L14-305 |
| `PassPlat.Dominio\Entities\Core\EmailLog.cs` | Tabla persistente; recovery email; no dedup eventos | L3-49 |
| `PassPlat.Datos\Configurations\Contexto\IPConfiguration.cs` | `UX_IPs_Direccion = HasIndex(Direccion).IsUnique()` | L23 |
| `PassPlat.Datos\PassPlatDbContext.cs` | DbContext; **no SaveChanges interceptors** | L73-77 |
| `CBP\CBP.Data\CBP.Data.Abstractions\IUnitOfWorkAsync.cs` | `BeginTransactionAsync`, `CommitTransactionAsync`, `ExecuteInTransactionAsync` | L59-66 |
| `CBP\CBP.Data\CBP.Data.Asynchronous\UnitOfWorkAsync.cs` | SaveChangesAsync directo; ExecuteInTransactionAsync con retry; prohíbe nesting | L59-63, L138-183 |
| `CBP\CBP.Core\CBP.Events\DomainEventDispatcher.cs` | In-line dispatch; **no post-commit hook, no dedup, no persist** | L37-158 |
| `CBP\CBP.Core\CBP.Events\IEventPublisher.cs` | `PublishAsync(IDomainEvent, CancellationToken)` | L9-23 |
| `CBP\CBP.Core\CBP.Events\IEventPublisher.cs` | `EventPublisher` delega a `DomainEventDispatcher.DispatchAsync` | L25-52 |
| `CBP\CBP.Core\CBP.Events\IDomainEvent.cs` | `EventId`, `OccurredOn`, `CorrelationId`, `EventType` | L5-27 |
| `CBP\CBP.Core\CBP.Events\EventBase.cs` | Base record; `CorrelationId` genérico | L3-18 |
| `PassPlat.Aplicacion\Services\SqlDistributedLockService.cs` | `sp_getapplock`; existe pero NO usado en IP flow | L1-91 |
| `Docs/Evidence/s20-concurrency-sqlserver.log` | Evidencia S20.7: 2 requests → 1 IP, 2 eventos, 2 EmailQueue, 1 DbUpdateException | — |

---

## S21 = IMPLEMENTED & CERTIFIED (2026-08-11)

> S21 fue **autorizado** tras el Gate S20 y ejecutado completo (S21.1→S21.6). Cierre de certificación:

### S21 Certification — Resultados en vivo (SQL Server real, `http://localhost:5259`)

| Gate | Resultado | Evidencia |
|------|-----------|-----------|
| S21.1 Schema + Entity | ✅ | Tabla `Outbox` creada (`Docs/BBDD/S21_Outbox_Schema.sql`), entidad + config EF, build 0/0 |
| S21.2 Repository + Worker | ✅ | `OutboxProcessor` inició, poll 15s, lock optimista `UPDATE...WHERE Status='pending'` |
| S21.3 IPService Integration | ✅ | `DetectarNuevaIPConOutboxAsync` crea Outbox row; PublishAsync inline eliminado |
| S21.4 Transacción garantizada | ✅ | 2 Start-Job concurrentes (IP `198.51.100.49`) → **1 fila IP + 1 Outbox** |
| S21.5 Idempotencia | ✅ | Dedup en `EnqueueEventAsync` (`ExisteNotificacionNuevaIpAsync`); re-trigger misma IP → 1 IP + 1 Outbox + 1 EmailLog (sin duplicado) |
| S21.6 Certificación | ✅ | 2 requests concurrentes → 200/200 (sin 500), 1 IP + 1 evento (Outbox `published`) + 1 email `enviado` (`EmailLog Id=21`, `admin@abarrotesdelsur.com`, MsgId `trk-515fa2640e0a4443bae1035eb640dff5`) |

### Datos de certificación

- **Race limpio**: el perdedor recibe 200 `{"mensaje":"NewIp event ya encolado por otra solicitud","queued":false}` — el `DbUpdateException` (UQ_IPs_Direccion) se captura y traduce a respuesta limpia en `DispConfiablesController.TriggerNewIp` (`EsViolacionIndiceUnico` usa `SqlException.Number` 2601/2627, no el mensaje).
- **Email resuelto**: `EmailBackgroundService` L120 no descarta jobs sin `ToEmail` si `IdUsuario` está presente; `PassPlatEmailService.SendFromJobAsync` resuelve por `IdUsuario` → `admin@abarrotesdelsur.com`.
- **CorrelationId W3C** preservado IP→Outbox→Email (`00-fb3aac...` en EmailLog Id=21).
- **xUnit**: 85/85 PASS (incluye `IPServiceDetectionTests` T1–T8 con contrato outbox). **Build**: 0 errores / 0 warnings.
- **Criterio S21.6**: ✅ 0 `DbUpdateException` expuestos · ✅ 1 evento lógico.

### Regresión y estado final

- Build final: 0 errores, 0 warnings (NU1603 pre-existente fuera de control).
- `EmailLog` Id=20 e Id=21 generados (NewIp, template 16) — sin duplicados.
- Outbox: 1 fila por IP detectada (Status `published`), intentos 0.
- Los emails NewIp se re-emiten si el destinatario no existe (Skip en `SendFromJobAsync`) — comportamiento esperado para usuarios sin email.

