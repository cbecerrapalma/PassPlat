# SuperMemory — Estado de Proyecto (Actualizado)

## S19 — CLOSED / GATE PASS (85/85 tests, 0 errores, E2E PASS)
- Build: 0 errores.
- Tests: 85/85 PASS.
- E2E mínimo con IP `203.0.113.17` (TEST-NET): 1ª llamada → 1 evento publicado, 2ª misma IP → sin evento.
- `IPRepository.ObtenerOCrear` usa `EsNueva` (no heurística `FecPrimerUso == UltUso`).
- `IPService.DetectarNuevaIPAsync` elimina catches silenciosos, reemplazados por `LogError` estructurado (correlationId, userId, tenantId, eventName, ip).
- `DispConfiablesController.TriggerNewIp` con `[FromQuery] string? ip` default `10.0.0.99`.
- S19.5 documentación → `S19-Sprint-Registry.md` + `AGENTS.md`.
- Deuda trasladada: `Sql_SlowQuery` (backlog), `Event_Failed` opcional.
- **Cierre: GATE PASS**.

## S20 — BLOCKED / DISCOVERY INCOMPLETO

### Estado actual
- **S20.1–S20.6**: COMPLETADOS (análisis conceptual).
- **S20.7**: BLOCKED (falta evidencia SQL Server real).
- **S20.8–S20.10**: PENDIENTES (dependen de S20.7).
- **S21**: NO DEFINIDO.

### S20.1–S20.6 — Discovery completado

| Módulo | Contenido |
|---|---|
| S20.1 | Flujo: Controller → IPService → IPRepository.ObtenerOCrear → PublishAsync → SaveChangesAsync |
| S20.2 | Análisis carrera: SELECT → INSERT → COMMIT |
| S20.3 | SQL Server: UQ_IPs_Direccion (no-clústeo, columns: Id, Direccion, TipoIP, Pais, Ciudad, EsSospechosa, FecPrimerUso, UltUso) |
| S20.4 | EF Core: SaveChangesAsync transacción implícita, DbUpdateException por unique constraint |
| S20.5 | Pipeline eventos: Publisher → Handler → EmailQueue (sin deduplicación) |
| S20.6 | Mecanismos reutilizables: no se ha identificado solución existente que garantice unicidad del evento NewIpDetected bajo concurrencia |

### Hipótesis (conceptual)
El orden de operaciones es la raíz: `detectar nueva → publicar evento → persistir`. En una operación concurrente, la publicación ocurre antes de conocer si la persistencia realmente ganó la carrera. Aunque SQL Server rechaza la inserción duplicada (UQ en `Direccion`), el evento `NewIpDetected` ya fue publicado antes de que SQL Server determine cuál request gana la restricción única.

### Regla
**NO ejecutar S20.7 contra InMemory como sustituto de SQL Server real.** InMemory no puede certificar el comportamiento de UQ, SaveChangesAsync y DbUpdateException bajo concurrencia real.

### Evidencia pendiente (S20.7)
- SQL Server real accesible
- 2 requests concurrentes para misma IP inexistente (TEST-NET, pre-verificada)
- Registrar: CorrelationId, EsNueva, PublishAsync, SaveChangesAsync, DbUpdateException, filas IPs finales, eventos NewIpDetected, emails encolados, correlación evento-request

### Próximo paso (OpenCode)
Si SQL Server real está disponible: ejecutar S20.7 en modo READ-ONLY. Si no, mantener S20 = BLOCKED / DISCOVERY INCOMPLETO.

### S21
NO DEFINIDO. No se implementarán soluciones de concurrencia (transacción, MERGE, outbox, idempotencia) hasta tener evidencia real de S20.7.

### Cierre S20
S20 = BLOCKED / DISCOVERY INCOMPLETO — requiere evidencia real de SQL Server. No se puede cerrar S20.8–S20.10 ni avanzar a S21.

### Evidencia actual
| Evidencia | Estado |
|---|---|
| UQ_IPs_Direccion | ✅ SQL comprobar (no-clústeo, columns: Id, Direccion, TipoIP, Pais, Ciudad, EsSospechosa, FecPrimerUso, UltUso) |
| 2 requests concurrentes | 🔴 No ejecutado |
| 2 eventos NewIpDetected | ⏸️ No probado |
| 1 INSERT exitoso / 1 DB_ERROR | ⏸️ No probado |
| 1 email encolado | ⏸️ No probado |
| CorrelationId entre requests | ⏸️ No probado |
| EsNueva observado por cada request | ⏸️ No probado |

### Decisiones clave (S20)
- **No modificar código, SQL, tests ni arquitectura** hasta tener evidencia real de S20.7.
- **No avanzar a S21**: no hay base de evidencia de concurrencia real.
- **CorrelationId no es clave de deduplicación funcional**: dos requests concurrentes legítimos tendrán correlationIds distintos.
- **La identidad funcional es**: `Direccion + NewIpDetected + estado de creación efectiva`.
- **S19 (CLOSED / GATE PASS) y S20 (BLOCKED / DISCOVERY INCOMPLETO)** mantienen el estado actual.

### Evidencia (estado certificado)
- S19: 85/85 tests PASS, build 0 errores, E2E mínimo con IP `203.0.113.17`.
- S20: S20.1–S20.6 completados análisis; S20.7 BLOCKED sin evidencia real.
- S21: NO DEFINIDO.
- No se modifica código, SQL ni tests.
- No se usa InMemory como sustituto de SQL Server real.
- No se define S21 como solución concreta.

### Registros S19-S20
| Sprint | Estado | Métricas |
|---|---|---|
| S19 | CLOSED / GATE PASS | 85/85 tests, 0 errores, E2E PASS |
| S20 | BLOCKED / DISCOVERY INCOMPLETO | S20.1–S20.6 completados, S20.7 pendiente |
| S21 | NO DEFINIDO | No implementar |

### Relevancia para SuperMemory
Este estado representa la disposición técnica más sólida actual del proyecto: las hipótesis de concurrencia están identificadas conceptualmente, pero no se ha demostrado empíricamente en SQL Server real. La decisión de no ejecutar S20.7 contra InMemory se basa en el principio de que solo SQL Server real puede certificar el comportamiento de UQ, SaveChangesAsync y DbUpdateException bajo concurrencia.

### Notas importantes
- S19 (CLOSED) no se modifica.
- S20 (BLOCKED) no se avanza.
- No se implementan soluciones de concurrencia hasta tener evidencia real de S20.7.
- No se define S21 como solución concreta.
- S20.7 requiere acceso real a SQL Server. Si SQL Server real no está disponible, mantener S20 = BLOCKED / DISCOVERY INCOMPLETO.
