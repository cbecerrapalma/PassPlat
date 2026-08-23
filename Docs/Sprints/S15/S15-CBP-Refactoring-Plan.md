# S15-CBP-Refactoring-Plan.md — Plan de Refactoring por Fases (F12)

# Estado          Borrador
# Tipo            ☐ Evidencia ☐ Análisis ☑ Decisión
# Fuente          todos N1+N2
# Depende de      todos los hallazgos
# Influye en      Ejecución F12
# Area            Plan de refactorizacion (F12)
# Framework CBP   Todo CBP (unificar)
# Cobertura       Hallazgos F9.2, F9.3, F7, F7.1, F7.2, F3, F4, F8, F9, F10
# Evidencia       Ids: DI-002, DI-005, CFG-001, CFG-002, LOG-001, OBS-003, OBS-011, SEC-005, SEC-001, SEC-002, EVENT-002, EVENT-003, DATA-004, DATA-005, WEB-004, F5/SEC-047
# Resultado       Orden de prioridad por seguridad→coherencia→limpieza
# Cobertura       —
# Riesgo          Critico en fase 1
# Prioridad       —

---

## 1. Proposito

Definir el plan de refactor para alinear PassPlat al framework CBP, atendiendo los hallazgos de las 16 auditorias, priorizando seguridad primero. Cada Fase tiene: objetivo, items, esfuerzo (S/M/L), riesgo y puerta de salida (build+test).

## 2. Orden de refactor

### Fase 1 — Seguridad critica (inmediato)
| Item | Hallazgo | Esfuerzo | Riesgo | Accion |
|---|---|---|---|---|
| Eliminar fuga ciphertext en ConfigAppService.cs:83 | CFG-001 / SEC-001 | S | Alto (usa val en logs) | No llamar Console.WriteLine con ciphertext; loguear solo success/fail + longitud. |
| Quitar password en appsettings (inicio123) | CFG-002 | S | Alto | Mover a User Secrets / KeyVault; ConnectionStrings sin clave plana en repo |
| Corregir EnviarCodigoMfaAsync silenciando excepciones | SEC-005 | M | Alto | Propagar Result.Failure en vez de return null; agregar log error completo |
| Registrar notificaciones de bloqueo no tragadas | SEC-006 / BLQ | S | Medio | No capturar en bloque -> logging y retornar indicacion |

Gate F1: build 0 error/0 warn + tests 66/66 + 17/17 + 24/24.

### Fase 2 — Logging unificacion (CBP.Logging)
| # | Hallazamiento | Esfuerzo | Accion |
|---|---|---|---|
| Consumir ILoggerService de CBP.Logging (LOG-001) | M | Sustituir ILogger<T> por ILoggerService en servicios singulares; definir contrato |
| Agregar ILogger a Dashboard (OBS-003) | S | Inyectar ILoggerService/ILogger |
| Enrichers de TraceId/SpanId (OBS-007) | S | Config Serilog |
| Correlacion app background jobs | OBS-006 | S | JobId + correlation |

### Fase 3 — Observabilidad / metrics
| # | Hallazamiento | Impact | Accion |
|---|---|---|---|
| Agregar OpenTelemetry (opcional) OBS-009 | M | SDK + Activity sources en key paths |
| Metrics via Meter (OBS-011) | M | Counters de login/errors |
| HealthChecks avanzados + metricas (OBS-010) | S | Expose /health + metrics |

### Fase 4 — Events (CBP.Events)
| # | Hallazamiento | Impact | Accion |
|---|---|---|---|
| Emitir eventos via DomainEventDispatcher (EVENT-002) | M | Registar AddDomainEvents(); handler email |
| Convertir static publishers a DI (EVENT-003) | M | Inyectar IEventPublisher/IDomainEventDispatcher |
| Mantener impacto (OBS/FASE13) | M | Conservar EmailJob compat |

### Fase 5 — DI/data/services
| # | Hallazamiento | Impact | Accion |
|---|---|---|---|
| Doble cache AddMemoryCache (DI-002) | S | Eliminar AddMemoryCache (dejar AddCbpCache) |
| Servicio locator Password (DI-013) | S | Resolver via constructor |
| IUnitOfWork Hybrid (DATA-004) | M | Mover/duplicar interfaz a Asynchronous |
| 5 repos cat SIN IFace (DATA-005) | S | Unificar a IRepositoryAsync |
| Bug concurrencia Acceso (DATA-007) | M | Fix rowcount en SP/EF |
| 6 controllers a BaseApiController (WEB-004) | S | Migrar FromResult |
| Mapa PoliticaPwd unico (SEC-047) | M | Consolidar modelo |

## 3. Estimacion de esfuerzo

| Fase | Esfuerzo estimado | Riesgo |
|---|---|---|
| F1 Seguridad | 1-2 jornadas | Alto (si no se hace: exposicion real) |
| F2 Logging | 2-3 | Medio |
| F3 Observabilidad | 2-3 | Bajo/Medio |
| F4 Events | 2-4 | Medio |
| F5 DI/data/services/web | 2-4 | Bajo |

Total aproximado: 9-16 jornadas. Cada fase con puerta de validacion (build 0/0 + tests).

## 6. Validacion de cada fase
- Build: `dotnet build PassPlat.slnx` con 0 errores/0 warnings.
- Regresion: `dotnet test` de 66/66 + Playwright 17 + 24 (A1.9/A1.8) + 22 FASE 13.
- Riesgo de regresion bajo por aislamiento por fase.

Resultado F12: plano de orden priorizado. Con la Certification y el Technical Debt Index.

## 7. Puertas / orden
Orden de ejecucion: F1 (seguridad) → F2 (log) → F3 (ops) → F4 (events) → F5 (DI/data/web). Las fases son independientes salvo F4 que depende de tener eventos (F2 logging) Prerrequisito: bueno set de tests.

Resultado F12: **Plan de refactor 5 fases, F1 seguridad primero**.