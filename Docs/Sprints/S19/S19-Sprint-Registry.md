# S19-Sprint-Registry.md — Registro de Trazabilidad S19 (detección determinista de IP nueva)

# Estado          Cerrado (2026-08-10) — Gate S19 = CLOSED / GATE PASS
# Nivel           Ejecución (fix de causa raíz + tests + E2E mínimo)
# Origen          S17-F6 como diagnóstico (S18-Discovery): heurística `esNueva` no determinista
#                 + catches silenciosos en IPService. Deuda `S19-Fx-IP-DETECTION-DETERMINISTIC` (S17 cierre).
# Regla           NO tocar CBP.Events, CBP.Logging, JWT, esquema SQL, S16/S17/S18.
#                 Concurrencia/outbox/MERGE/SP: explicitamente FUERA DE ALCANCE (riesgo documentado).

---

## Flujo de trazabilidad

```
S18 Discovery (esNueva no determinista) → S19.1 Discovery confirmado → S19.2 Fix determinista
→ S19.3 Tests T1–T8 → S19.4 E2E mínimo (IP TEST-NET) → S19.5 Documentación → Gate PASS
```

---

## Objetivo
Corregir la causa raíz por la que `NewIpDetected` no se publicaba en runtime: la decisión de "IP nueva"
dependía de comparar `FecPrimerUso` vs `UltUso` (dos `DateTime.Now` independientes → no determinista,
una IP realmente nueva se clasificaba como no nueva cuando los timestamps diferían). La decisión pasa a
provenir exclusivamente de la **existencia real** (¿la fila se creó en esta operación?, `EsNueva` en el
repositorio). El `IPService` usa ese resultado para decidir la publicación, cumpliendo la condición de gate:
"el evento no basta con aparecer por cambiar el trigger/IP; debe demostrarse que `IPRepository` determina
`EsNueva` y `IPService` lo usa para decidir".

---

## Decisiones de arquitectura

| Decisión | Valor | Fecha |
|---|---|---|
| Fuente de verdad de "es nueva" | `IPRepository.ObtenerOCrear` → `Result<IPRegistro(IP Entidad, bool EsNueva)>` (existencia), no timestamps | 2026-08-10 |
| Semántica primaria/secundaria | Persistencia/detección = operación primaria (Success conservado) · publicación de evento = efecto secundario (fallo observable vía `LogError` estructurado, nunca silencioso) | 2026-08-10 |
| Logging de fallo de evento | `ILoggerService.LogError(LogEvent)` → `LoggingEvents.EventFailed` (catálogo CBP, sin literales) + `LoggingPropertyNames.*` (correlationId, userId, tenantId, event, ip) | 2026-08-10 |
| Endpoint trigger | `POST /api/dispconfiables/trigger-new-ip/{idUsuario}?ip=` con default `10.0.0.99` — **capacidad de diagnóstico/prueba controlada** (convención `trigger-*`), NO vía funcional de negocio | 2026-08-10 |
| Concurrencia | **Sin solución en S19**: `UQ_IPs_Direccion` existe y evita duplicación física; la carrera "primera vez" puede doble-publicar antes de que falle unicidad. No certificada por InMemory (ver Riesgos). Sin MERGE/transacción/outbox | 2026-08-10 |

---

## Registro de tareas

| ID | Descripción | Resultado | Estado |
|---|---|---|---|
| S19-001 | Discovery FASE 0 (read-only): confirmar causa raíz, clave funcional, handler, DI | Confirmado en código | ✅ DONE |
| S19-002 | `IPRepository`: `ObtenerOCrear` → `Result<IPRegistro(Entidad, EsNueva)>`; `EsNueva` = creación real (existencia) | Build OK | ✅ S19.2 |
| S19-003 | `IPService`: elimina heurística timestamps y duplicado `UltUso = Now`; usa `repoResult.Value.EsNueva`; elimina `catch` silenciosos en `DetectarNuevaIPAsync` y `VerificarCambioIPAsync`; `ILoggerService` para fallo de publicación (Result.Failure y excepción), primaria siempre Success | Build OK | ✅ S19.2 |
| S19-004 | `DispConfiablesController.TriggerNewIp`: `[FromQuery] string? ip` (default `10.0.0.99`) + doc de capacidad de prueba | Build OK | ✅ S19.2 |
| S19-005 | Build `PassPlat.slnx` | 0 errores · 0 warnings nuevas (solo NU1603 pre-existente) | ✅ S19.3 |
| S19-006 | Suite xUnit | **85/85 PASS** (76 baseline + 9 S19) | ✅ S19.3 |
| S19-007 | E2E mínimo IP TEST-NET `203.0.113.17` | Primera llamada → `Event_Published`→`Email_Queued`→`Event_Handled`; segunda → 200 sin evento | ✅ S19.4 |
| S19-008 | Evidencia | `Docs/Evidence/s19-ip-detection.log` | ✅ S19.4 |
| S19-009 | Documentación | Este registry + AGENTS.md | ✅ S19.5 |

---

## Pruebas S19 (S19.3)

| ID | Prueba | Resultado |
|---|---|---|
| T1 | IP inexistente → crea, `EsNueva=true`, `PublishAsync` exactamente 1 vez | ✅ |
| T2 | IP existente → 0 publicaciones, 1 fila (sin duplicado), `UltUso` refrescado | ✅ |
| T3 | **Independencia de timestamps** (regresa la heurística): existente con `FecPrimerUso==UltUso` → 0; existente con `!=` → 0; IP nueva persistida (timestamps forzados iguales) → segunda detección `EsNueva=false`; IP genuinamente nueva → 1 publicación | ✅ |
| T4 | Error de persistencia (operación primaria) → `Result.Failure("DB_ERROR")` propagado + 0 publicaciones | ✅ |
| T5 | `PublishAsync` → `Result.Failure`: primaria Success, IP persistida, `LogError` estructurado con correlationId; excepción → mismo contrato + `Exception` no tragada | ✅ |
| T6 | El evento propaga `CorrelationId` (W3C) del request | ✅ |
| T7 | El evento propaga `IdUsuario` e `IdTenant` | ✅ |
| T8 | Regresión: tras persistir, segunda detección de la misma IP no duplica fila ni evento | ✅ |
| T9 | `dotnet build PassPlat.slnx` 0 errores / 0 warnings nuevas | ✅ |
| T10 | Suite completa `dotnet test PassPlat.slnx` | ✅ 85/85 |

---

## Evidencia E2E en vivo (S19.4) — `Docs/Evidence/s19-ip-detection.log`

| Evento | Ruta | Evidencia |
|---|---|---|
| `Event_Published` (NewIpDetected) | `POST /api/dispconfiables/trigger-new-ip/3?ip=203.0.113.17` (1ª llamada) | `PassPlat.WebAPI\Logs\passplat-20260810.log` 18:07:06.770, correlationId `00-ee323b790fec243096be552c8f96a11d-...` |
| `Email_Queued` | idem (handler → IEmailQueue) | 18:07:06.781, correlationId idéntico, tenantId=2 userId=3 |
| `Event_Handled` | idem (`NewIpDetected por NewIpDetectedEventHandler`) | 18:07:06.783, correlationId idéntico |
| Sin evento (2ª llamada) | misma IP 2s después | 18:07:08.880 (`Jwt_Validated` sí, `Event_Published` NO), HTTP 200 |
| Estado BD | `IPs` | 1 única fila Id=3 `203.0.113.17` (FecPrimerUso 18:07:06.722, UltUso 18:07:08.905, TipoIP=4, EsSospechosa=0) |

**Lectura crítica**: la 1ª llamada creó la fila con `FecPrimerUso≠UltUso` medidos con dos `DateTime.Now`
independientes (escenario exacto del bug S18) y **publicó**; la 2ª llamada, misma IP ya existente, **no publicó**.
La decisión fue por existencia (`EsNueva` del repositorio), no por timestamps. `Email_Queued` y `Email_Sent`
en segundo plano real completan el pipeline (template `new-ip`).

---

## Matriz de cierre de hallazgos

| Hallazgo | Estado | Resolución |
|---|---|---|
| `S19-Fx-IP-DETECTION-DETERMINISTIC` heurística `esNueva` no determinista | ✅ RESUELTO | `EsNueva` del repositorio por existencia; tests T1–T3 + E2E |
| Catches silenciosos en `IPService` (`DetectarNuevaIPAsync`/`VerificarCambioIPAsync`) | ✅ RESUELTO | `ILoggerService.LogError` estructurado (Result.Failure y excepción), correlación presente |
| CBP descartado como causa (S17-F6 / S18) | ✅ Confirmada | No se modificó CBP.Events/CBP.Logging/JWT |
| Concurrencia de doble publicación | ⚠️ Riesgo documentado | Ver Riesgos |

---

## Riesgos y deudas técnicas (fuera de alcance S19)

1. **Concurrencia (doble publicación)**: `UQ_IPs_Direccion` es UNIQUE en SQL Server y evita la duplicación
   física de `Direccion`; sin embargo, dos requests concurrentes contra una IP inexistente pueden publicar el
   evento dos veces antes de que `SaveChanges` falle por unicidad. Esta propiedad **no queda certificada por
   EF Core InMemory** (no reproduce restricciones relacionales de SQL Server). La eliminación de la doble
   publicación bajo concurrencia es deuda/mejora futura (MERGE/SP atómico/outbox/transacción): NO se introdujo
   aquí porque convertiría un fix acotado en una modificación arquitectónica.
2. **Default SQL vs entidad**: `IPs.FecPrimerUso` usa `sysutcdatetime()` (SQL) pero la entidad `IP.Crear`
   usa `DateTime.Now` (local) — divergencia menor ya pre-existente, documentada; no afecta a la decisión
   `EsNueva` (independiente de timestamps).
3. `Sql_SlowQuery`: en backlog (sin interceptor EF, P6 alcance cerrado). Sin cambios.
4. `Email_Sent` de `new-ip`: depende del `EmailBackgroundService` (24h no; ciclo real), fuera del E2E mínimo S19.

---

## Gates

| Gate | Estado |
|---|---|
| G1 Discovery | ✅ Causa raíz confirmada en código |
| G2 Fix determinista (S19.2) | ✅ `IPRegistro.EsNueva` + `IPService` + controller |
| G3 Build | ✅ 0 errores · 0 warnings nuevas |
| G4 Tests (S19.3) | ✅ 9/9 S19 + regresión 85/85 |
| G5 E2E mínimo (S19.4) | ✅ Publica 1ª, no publica 2ª; evidencia con correlationId W3C |
| G6 Condición de gate S19 | ✅ **EsNueva proviene del repositorio y gobierna la publicación** (tests T1-T3 + E2E: la 2ª llamada con timestamps distintos NO publica → la decisión no es de timestamps) |
| G7 Documentación | ✅ Registry + evidencia + AGENTS.md |
| G8 Cierre | ✅ **S19 = CLOSED / GATE PASS** |

---

## Deudas trasladadas (no bloqueantes)

| Deuda | Sprint | Estado |
|---|---|---|
| Doble publicación bajo concurrencia (carrera primera-vez) | Backlog (mejora futura, sin solución en S19) | ⏸️ Riesgo documentado |
| `Sql_SlowQuery` / `Event_Failed` E2E opcional | Backlog | ⏸️ No bloqueante |

---

## Reglas respetadas

- NO se modificó `CBP.Events`, `CBP.Logging`, `CBP.Authentication.JwtBearer`, esquema SQL, ni docs S16/S17/S18.
- NO se introdujo solución de concurrencia (MERGE, SP, transacción, outbox).
- NO se realizó mutación destructiva en BD desde el E2E (pre-check de `203.0.113.17`; STOP si existía).
- E2E usa IP TEST-NET determinista única, sin depender del estado residual de `10.0.0.99`.
- T5 conserva primaria Success y deja fallo de evento observable (log estructurado con correlationId), sin
  reintroducir el problema original (catches vacíos).