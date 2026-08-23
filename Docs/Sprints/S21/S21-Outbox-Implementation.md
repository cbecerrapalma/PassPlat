# S21 — Outbox Pattern Implementation & Certification: NewIpDetectedEvent

## Status: S21 = CLOSED / GATE PASS (S21.4 · S21.5 · S21.6 = PASS, 2026-08-11)

This document is the implementation + certification record for the **Transactional Outbox** converged from the approved design
(`Docs/Sprints/S20/S20-Concurrency-Discovery.md`). The architectural correction of the pre-existing deviation
(`OutboxProcessor.EnqueueEventAsync` → direct `IEmailQueue`) is applied, and the naming contract `ProcessingStartedAt` is closed.

---

## §1 — Arquitectura final certificada

```
HTTP Request (POST /api/dispconfiables/trigger-new-ip/{idUsuario}?ip=...)
    │  [Authorize(Policy="USUARIOS_VERDISP")]
    ▼
DispConfiablesController.TriggerNewIp
    │  _uow.ExecuteInTransactionAsync
    ▼
IPService.DetectarNuevaIPConOutboxAsync
    │  IPRepository.ObtenerOCrear (SYNC: EsNueva determinista, DbSet.Add)
    │  Outbox.Crear("NewIpDetectedEvent", payload, corrId, idTenant, idUsuario)   ← NO publish inline
    ▼
COMMIT (IP + Outbox atómicos)  ← único punto de commit (consumer = Controller)
    │  posteriormente (worker, polling 15s, batch 100)
    ▼
OutboxProcessor
    │  ResetStale → ObtenerPendientes → MarcarProcessingAtomicAsync (claim SQL)
    │  [dedup] EmailLogRepository.ExisteNotificacionNuevaIpAsync(IdUsuario, DireccionIP)
    │  PublishEventAsync: deserializa NewIpDetectedPayload → NewIpDetectedEvent → WithCorrelationId
    ▼
IEventPublisher.PublishAsync   (scoped, desde CreateAsyncScope del ciclo)
    ▼
EventDispatcher.DispatchAsync
    ▼
NewIpDetectedEventHandler (único punto de construcción del EmailJob)
    ▼
IEmailQueue.EnqueueAsync(EmailJobKind.NewIp)
    ▼
EmailBackgroundService (poll 15s, retries [1,5,15]min, MaxRetries 3)
    ▼
PassPlatEmailService → SMTP
    ▼
EmailLog (Estado='enviado', CorrelationId, ExtraJson)
```

### Reglas respetadas
- ❌ `CBP.Events` NO modificado. Publisher/dispatcher/handlers como en `CBP` (scoped).
- ❌ `CBP.Data.Asynchronous` / `CBP.Data.Synchronous` NO modificados (parity Async ↔ Sync intacta).
- ✅ `EmailQueue` = consumer del Outbox, nunca productor.
- ✅ Outbox limitado a `NewIpDetectedEvent`; `SecurityAlertEvent` conserva flujo inline (fuera de alcance).
- ✅ IP + Outbox se persisten atómicamente en `ExecuteInTransactionAsync`.
- ✅ El worker publica DESPUÉS del commit; el evento NO se ejecuta inline desde `IPService`.
- ✅ CorrelationId propagado: request W3C → Outbox → evento reconstructido → handler → EmailJob → EmailLog.

### Corrección de desviación (S21.2)
- **Desviación**: `OutboxProcessor.EnqueueEventAsync` (L178-225) construía `EmailJob` y llamaba `IEmailQueue.EnqueueAsync` directo,
  saltándose `IEventPublisher`/`NewIpDetectedEventHandler`.
- **Fix**: `IEmailQueue` eliminado del constructor. Nuevo `PublishEventAsync`:
  1. Deserializa `NewIpDetectedPayload` (internal record, `Services/Security/NewIpDetectedPayload.cs`).
  2. Construye `NewIpDetectedEvent(IdUsuario, IdTenant, IdIP, DireccionIP, Pais, Ciudad, UserAgent, DeviceName)`.
  3. `evt = (NewIpDetectedEvent)evt.WithCorrelationId(outbox.CorrelationId)`.
  4. Resuelve `IEventPublisher` **scoped** desde `scope.ServiceProvider` del ciclo y llama `PublishAsync(evt, ct)`.
- El campo extra `UserEmail` del payload se ignora (el email se resuelve por `IdUsuario` en `PassPlatEmailService`, template `new-ip`).

---

## §2 — Naming contract (decisión cerrada en S21)

`ProcessingAt` **NO es nombre alternativo válido**. Renombrado a `ProcessingStartedAt` en toda la cadena:

| Artefacto | Antes | Después |
|-----------|-------|---------|
| Entidad `Outbox.cs` | `ProcessingAt` | `ProcessingStartedAt` |
| EF config `OutboxConfiguration.cs` | property + idx `IX_Outbox_ProcessingAt` | property + idx `IX_Outbox_ProcessingStartedAt` |
| SQL schema `Docs/BBDD/S21_Outbox_Schema.sql` | columna + idx | columna + idx `IX_Outbox_ProcessingStartedAt` |
| BD live (PassPlat) | `ProcessingAt` | `sp_rename` → `ProcessingStartedAt` + idx creado |
| `OutboxRepository.cs` | `ProcessingAt` en 3 SQL + param | `ProcessingStartedAt` |
| `OutboxProcessor.cs` | `processingAt` local | `processingStartedAt` local |

Ciclo de vida: `CreatedAt` (creación) → `ProcessingStartedAt` (claim: pending→processing) → `ProcessedAt` (published) → `NextAttemptAt` (retry).

Evidencia BD live post-rename: 6 filas `published` preservadas con `ProcessingStartedAt=NULL` (correcto), índices
`IX_Outbox_Pending_Status_CreatedOn` + `IX_Outbox_ProcessingStartedAt` presentes.

---

## §3 — Lock/claim y recovery (invariantes implementadas)

| Invariante | SQL / Comportamiento |
|-----------|----------------------|
| Claim atómico | `UPDATE Outbox SET Status='processing', ProcessingStartedAt={0} WHERE Id={1} AND Status='pending'` → filas afectadas decide |
| Polling | `Status='pending' && (NextAttemptAt IS NULL OR NextAttemptAt <= UTCNOW)`, ORDEN CreatedAt, batch, AsNoTracking |
| Stale recovery | `Status='processing' && ProcessingStartedAt < UTCNOW-300s` → `pending` (mensajes abandonados por worker caído; multi-instancia) |
| Published | `UPDATE ... SET Status='published', ProcessingStartedAt=NULL, ProcessedAt={0} WHERE Id={1}` (idempotente) |
| Failed | `SET Status='failed', LastError={0}, NextAttemptAt={1}, Attempts={2} WHERE Id={3}` (NextAttemptAt no null) |
| Reprogramar | `SET Status='pending', LastError=NULL, NextAttemptAt={0}, Attempts={1} WHERE Id={2}` (retry delay `[1,5,15]min`) |
| Árbitro de carrera | `UQ_IPs_Direccion` (índice único) decide el creador real; perdedor atrapa `DbUpdateException` 2601/2627 → `queued:false` |

Configuración (`appsettings.json` → `Outbox:...`, `OutboxOptions.cs`): `PollIntervalSeconds=15`, `BatchSize=100`, `MaxRetries=3`,
`RetryDelayMinutes=[1,5,15]`, `ProcessingTimeoutSeconds=300`.

---

## §4 — Gate S21.4: Concurrency / Atomicity — **PASS**

**Setup**: IP `203.0.113.57` (TEST-NET-3, pre-verificada inexistente). 2 POST concurrentes (`Start-Job` paralelos) con JWT
`admin_abarrotes` (Id=3), tenant 2, app 1.

| Verificación | Resultado |
|--------------|-----------|
| Respuestas HTTP | Ganador `{queued:true}` · Perdedor `{queued:false}` (catch 2601/2627, arbitrado por `UQ_IPs_Direccion`) |
| 1 sola fila IPs | ✅ Id=19 |
| 1 solo Outbox | ✅ Id=13, `Status='published'` |
| 1 solo EmailLog (1 evento lógico) | ✅ Id=23, `Estado='enviado'` |
| No `PublishAsync` inline | ✅ `Event_Queued` en request (16:38:53); `Event_Published` solo en worker (16:39:05) |
| IP + Outbox atómicos | ✅ `ExecuteInTransactionAsync`; perdedor nunca persistió Outbox |
| Sin `DbUpdateException` expuesto | ✅ catch `EsViolacionIndiceUnico` (2601/2627) en el controller |

**Evidencia**: ver log §5. TraceIds concurrentes `b51f190b` (ganador) y `37f8c1b8` (perdedor).

---

## §5 — Gate S21.5: Idempotency / Crash window — **PASS**

**Escenario**: `pending → worker adquiere (processing) → PublishAsync SUCCESS + Email_Sent → crash antes de MarcarPublished → recovery reprocesa`.

**Setup**: IP `203.0.113.59` (flujo completo: IP Id=20, Outbox Id=14 `published`, EmailLog Id=24).
Simulación de crash: `UPDATE Outbox SET Status='processing', ProcessingStartedAt=DATEADD(second,-400,GETUTCDATE()), ProcessedAt=NULL WHERE Id=14`.

**Resultado**:
| Verificación | Resultado |
|--------------|-----------|
| Recovery: fila stuck `processing` NUNCA se queda abandonada | ✅ `ResetStaleAsync` (ProcessingStartedAt viejo) → `pending` → re-claim |
| Segundo procesamiento NO genera segundo EmailJob efectivo | ✅ EmailLog única IP quedó = **1** |
| Dedup por identidad funcional (DireccionIP + EventType + creación efectiva), NO CorrelationId | ✅ `ExisteNotificacionNuevaIpAsync(3,'203.0.113.59')` sobre `ExtraJson.Contains(ip) && ExtraJson.Contains("NewIp")` |
| Estado final | ✅ Outbox Id=14 `published`, `Attempts=0`, `ProcessedAt=20:41:35` |

**Log clave (16:41:35.841)**:
```
NewIp dedup: notificacion ya existe para usuario 3 IP 203.0.113.59 - omitiendo publicacion
```

**Observación (no bloqueante, límite conocido del diseño)**: la dedup vía `EmailLog` protege el crash window entre
publish+send persistidos y `MarcarPublished`. La micro-ventana publish→send (EmailJob aún en channel in-memory) cubre
at-least-once: un crash ahí pierde el EmailJob en memoria que se re-encolará en el reproceso → 1 email neto. No se exige
exactly-once.

---

## §6 — Gate S21.6: E2E Certification — **PASS**

**Setup**: IP `203.0.113.60` (TEST-NET, pre-inexistente). `POST trigger-new-ip/3?ip=203.0.113.60` (16:42:30).

**Pipeline en vivo (cadena completa)**:
| Paso | Evidencia |
|------|-----------|
| POST → IP + Outbox COMMIT | IP Id=21 (`FecPrimerUso=UltUso=16:42:30.742`) |
| OutboxProcessor | Outbox Id=15: `CreatedAt=20:42:30.742`, `ProcessedAt=20:42:35.917`, `Status='published'`, `Attempts=0` |
| `IEventPublisher.PublishAsync` | `Event_Published` 16:42:35.912 (scope=domainEvents, event=NewIpDetected) |
| `EventDispatcher` | (visible como Event_Published/Event_Handled) |
| `NewIpDetectedEventHandler` | `Event_Handled` 16:42:35.915 (method=NewIpDetectedEventHandler) |
| `IEmailQueue.EnqueueAsync` | `Email_Queued` 16:42:35.913 (scope=email, userId=3, tenantId=2) |
| `EmailBackgroundService` | — |
| `PassPlatEmailService` → SMTP | `Email_Sent` 16:42:37.847 |
| `EmailLog` persistido | Id=25, `Estado='enviado'`, `Intentos=1`, CorrelationId W3C, ExtraJson con DireccionIP/FechaDeteccion |

**CorrelationId W3C único propagado por toda la cadena**: `00-64aa37b18cb653c8631e6842c4725044-741e86a78523b439-00`
(request → Event_Queued → Event_Published → Email_Queued → Event_Handled → Email_Sent → EmailLog).

---

## §7 — Gate de cierre

| Gate | Estado |
|------|--------|
| S21.4 Concurrency / Atomicity | ✅ **PASS** |
| S21.5 Idempotency / Recovery | ✅ **PASS** |
| S21.6 E2E Certification | ✅ **PASS** |
| Build | ✅ 0 errores (3 warnings pre-existentes CS8602 en ConfProvIden) |
| Tests xUnit | ✅ 85/85 PASS |
| Parity CBP.Data Async ↔ Sync | ✅ Intacta (sin modificaciones) |
| **S21** | ✅ **CLOSED / GATE PASS** |

---

## §8 — Deudas / notas (no bloqueantes)

- Micro-ventana exactly-once (publish→send con channel in-memory) documentada en §5; diseño S21 es at-least-once con dedup EmailLog.
- `Event_Failed` para `NewIpDetectedEvent` no observado en gates (handler no falló); cubierto por dispatcher ante `Result.Failure`.
- Logs de diagnóstico `DIAGNOSTIC AUTH` siguen activos en el entorno de desarrollo (no afectan S21).
- `Docs/Evidence/s21-gates-*.log` = evidencia congelada de la campaña (ver archivo de evidencia).

## Relevant Files
| Archivo | Rol |
|---------|-----|
| `PassPlat.Aplicacion/Services/Infrastructure/OutboxProcessor.cs` | Worker convergido: publish vía `IEventPublisher`, dedup EmailLog, retry/failed |
| `PassPlat.Aplicacion/Services/Security/IpEventHandlers.cs` | `NewIpDetectedEventHandler` → `EmailJob(EmailJobKind.NewIp)` |
| `PassPlat.Aplicacion/Services/Security/IPEvents.cs`, `NewIpDetectedPayload.cs` | Evento + payload (reconstrucción del worker) |
| `PassPlat.Datos/Repositories/OutboxRepository.cs` (`IOutboxRepository`) | Claim/published/failed/reprogramar/reset stale (SQL idempotente) |
| `PassPlat.Datos/Repositories/EmailLogRepository.cs` | `ExisteNotificacionNuevaIpAsync` (dedup persistente) |
| `PassPlat.Dominio/Entities/Core/Outbox.cs`, `OutboxConfiguration.cs`, `Docs/BBDD/S21_Outbox_Schema.sql` | Entidad/config/schema con `ProcessingStartedAt` |
| `PassPlat.WebAPI/Controllers/DispConfiablesController.cs` | `TriggerNewIp` + `ExecuteInTransactionAsync` + catch 2601/2627 |
| `PassPlat.WebAPI/Program.cs`, `AplicacionDependencyInjection.cs`, `DatosDependencyInjection.cs` | DI: `AddHostedService<OutboxProcessor>`, `AddCBPEvents`, repos + `IEventPublisher` scoped |
| `PassPlat.Aplicacion.Test/Tests/S19/IPServiceDetectionTests.cs` | 85/85 PASS; contrato outbox sin publish inline |
| `Docs/Evidence/s21-gates-20260811.log` | Evidencia congelada de S21.4/S21.5/S21.6 |
