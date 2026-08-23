# S15-Technical-Debt-Index.md — Indice de Deuda Tecnica (S15)

# Estado          borrador
# Tipo            ☐ Evidencia ☐ Análisis ☑ Decisión
# Fuente          todos N1+N2
# Depende de      todos los candidatos
# Influye en      Refactoring, Executive-Summary
# Area            Deuda tecnica consolidada
# Cobertura       Todos los hallazgos con ID de F0..F12
# Resultado       Indice integrado por sprint / prioridad
# Prioridad       —

---

## 1. Proposito

Consolidar TODOS los hallazgos de las 16 auditorias S15 con su ID, area, severidad, impacto, esfuerzo, prioridad y sprint sugerido. Es el backlog de deuda tecnica sobre CBP, para seguimiento S16+.

## 2. Indice de deuda tecnica

| ID | Area | Severidad | Impacto | Esfuerzo | Prioridad | Sprint | Fuente | Resumen |
|---|---|---|---|---|---|---|---|---|
| CFG-001 / SEC-001 | Config/Security | CRITICA | Alto | S | P0 | S16 | F9.3/F7.2 | Fuga ciphertext ConfigUserService:83 |
| CFG-002 | Config | CRITICA | Alto | S | P0 | S16 | F9.3 | Password SQL en appsettings |
| SEC-005 | Security-Log | ALTA | Alto | M | P0 | S16 | F7.2 | EnviarCodigoMfaAsync silencia excepciones |
| EVENT-002 | Events | ALTA | Medio | M | P1 | S18 | F3 | Dispatcher no consumido |
| EVENT-003 | Events | MEDIA | Bajo | M | P1 | S18 | F3 | Publishers sealed static |
| LOG-001 | Logging | ALTA | Medio | M | P1 | S17 | F7 | ILoggerService sin uso |
| OBS-003 | Observab | MEDIA | Bajo | S | P2 | S17 | F7.1 | Dashboard sin ILogger |
| OBS-011 | Observab | MEDIA | Bajo | M | P2 | S17 | F7.1 | Sin Metrics |
| OBS-007 | Observab | MEDIA | Bajo | S | P2 | S17 | F7.1 | Enrichers TraceId |
| SEC-006 | Security-Log | MEDIA | Bajo | S | P2 | S16 | F7.2 | Bloqueo notif tragada |
| SEC-047 | Security | MEDIA | Bajo | M | P2 | S19 | F5 | Modelo PoliticaPwd duplicado |
| DI-002/CACH-001 | DI/Cache | MEDIA | Bajo | S | P2 | S19 | F9.2 | Doble cache (AddMemoryCache) |
| DI-013 | DI | ALTA | Medio | S | P1 | S16 | F9.2 | Service locator IEmailQueue |
| DI-001 | DI | BAJA | Bajo | S | P3 | S19 | F9.2 | Factura directa/new |
| DATA-004 | Data | MEDIA | Bajo | M | P2 | S19 | F4 | IUnitOfWork en Synchronous |
| DATA-005 | Data | BAJA | Bajo | S | P3 | S19 | F4 | 5 repos sin interfaz |
| DATA-007 | Data | MEDIA | Medio | M | P1 | S16 | F4 | Bug AsignarAccesoAsync |
| WEB-004 | WebApi | BAJA | Bajo | S | P3 | S19 | F10 | 6 controllers ControllerBase |
| EVENT-004/005/006 | Events | MEDIA | Bajo | M | P2 | S18 | F3 | Propagation/acoplamiento email |
| Q-AUD-002 | Data | MEDIA | Medio | M | P2 | S19 | F4-companero | No se usa GetPagedAsync (pagin manual) |
| Q-AUD-003 | Data | MEDIA | Medio | S | P2 | S19 | F4-companero | Sin AsSplitQuery (cartesian) |
| CAD-001 | Caching | MEDIA | Medio | S | **P1** | **S16** | F2-companero | PoliticaPwd sin cache (6 q/login) |
| CAD-002 | Caching | BAJA | Bajo | S | P2 | S19 | F2-companero | ConfigTenant sin cache |
| CTX-001 | Logging | MEDIA | Bajo | S | P2 | S17 | F7-companero | AppId no en scope |
| CTX-002 | Logging | MEDIA | Bajo | S | P2 | S17 | F7-companero | TraceId/SpanId no enriquecidos |
| CTX-003/004 | Logging | BAJA | Bajo | S | P3 | S17 | F7-companero | SessionId/ExceptionId inconsistentes |
| COUP-001/002/003 | Events | ALTA | Medio | M | **P1** | **S18** | F3-companero | Acoplamiento evento→email (publisher static) |
| SRV-006 | Services | BAJA | Bajo | S | P3 | S19 | F9 | Boilerplate repetido |

## 3. Resumen por sprint

| Sprint/Fase | Items | Esfuerzo | Nota |
|---|---|---|---|
| S16 — Seguridad (P0) | CFG-001, CFG-002, SEC-005, DATA-007 (P1), DI-013, **CAD-001 (P1)** | S/M | Rápido win cache |
| S17 — Logging | LOG-001, OBS-003/007/011, CTX-001/002/003/004 | M | Habilitar ILoggerService |
| S18 — Events | EVENT-002/003/004/006, **COUP-001/002/003** | M | Integrar dispatcher |
| S19 — DI/Data/Web | DI-001/002, DATA-004/005, WEB-004, Q-AUD-002/003, CAD-002, SRV-006 | S/M | limpieza + paginación |

## 3. Count

| Severidad | Count |
|---|---|
| CRITICA | 2 |
| ALTA | 3 |
| MEDIA | 15 |
| BAJA | 8 |
| Total items | 28 |

> Nota: Este indice alimenta la Certification S15. No se modifica ningun codigo S15 — bloqueado hasta pass (S16+).

## 4. Versionado
Fase referenciada en AGENTS.md como excel para seguimiento post S15 (FASE_S15_closed).