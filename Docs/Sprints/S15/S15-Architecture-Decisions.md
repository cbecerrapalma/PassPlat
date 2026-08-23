# S15-Architecture-Decisions.md — Registro de Decisiones de Arquitectura (ADR)

# Estado          Borrador
# Tipo    ☐ Evidencia ☐ Análisis ☑ Decisión
# Fuente          todos N1 (ADR)
# Depende de      hallazgos decisión
# Influye en      Refactoring
# Área            Decisiones de arquitectura derivadas del S15 (adopción CBP)
# Cobertura       Referenciable desde todos los docs (Fase12/G)
# Prioridad       —

---

## 1. Propósito

Registro centralizado de decisiones de arquitectura (ADR) surgidas de la auditoría S15. Cada hallazgo pendiente de la matriz F11 se resuelve con un ADR con: **Estado → Contexto → Decisión → Consecuencia**. Los documentos de auditoría referencian `ADR-XXX` en lugar de duplicar la decisión.

## 2. Decisiones registradas

### ADR-001 — Authentic token / claims layer → EXTENDER
- **Contexto**: PassPlat reutiliza `IJwtTokenService` de CBP.Authentication para generar/validar JWT; la capa de claims de permisos y sesión es propia (multi-tenant).
- **Decisión**: Mantener `AuthenticationTokenService` + `PermissionClaimBuilder` sobre CBP.Authentication (ratio ~80:20). No migrar claims a CBP.
- **Consecuencia**: Sin duplicación de JWT, dominio tenant own claims. Aceptado.
- **Estado**: **ACEPTADO** (F1).

### ADR-002 — Adoptar CBP.Events DomainEventDispatcher — PENDIENTE
- **Context:** Los eventos se definen como `EventBase` pero se publican con `sealed static` publishers que encolan `EmailJob` (COUPL-001..004). Sin handlers, sin desacople.
- **Decisión**: Migrar a `AddDomainEvents()` + `IEventPublisher` (CBP). El email pasa a ser un consumidor del evento, no el único efecto.
- **Consecuencia**: Testeabilidad, 2.º consumidor (auditoría) sin tocar email, propagación de CorrelationId.
- **Estado**: **PROPUESTO** (S18, Fase 4 del plan).

### ADR-003 — ILoggerService de CBP.Logging — EN EVALUACIÓN
- **Context:** `ILoggerService` registrado pero sin consumidores; se usa `ILogger<T>` + Serilog multi-pipeline (LOG-001).
- **Decisión**: Definir contrato único de logging. Decidir entre consumir `ILoggerService` o unificar un único pipeline Serilog.
- **Estado**: **EN EVALUACIÓN** (Fase 2 del plan).

### ADR-004 — Contexto de logging rico (AppId, TraceId, SessionId) — PROPUESTO
- **Context:** `LoggingScopeMiddleware` pushea solo `TenantId` + `UserId`; faltan AppId, TraceId/SpanId, SessionId, EventId, ExceptionId (CTX-001..004).
- **Decisión**: Ampliar el scope (AppId) + enrichers Trace/Span + SessionId coherente.
- **Estado**: **PROPUESTO** (S17).

### ADR-005 — Cache única vía ICacheService — LIMPIEZA
- **Context:** `AddMemoryCache()` residual junto a `AddCbpCache` (CACH-001/DI-002).
- **Decisión**: Eliminar `AddMemoryCache()`; usar EXCLUSIVAMENTE `ICacheService` de CBP.Caching. Cachear `PoliticaPwd` + `ConfigTenant` (CAD-001/002).
- **Estado**: **ACEPTADO** (S16).

### ADR-006 — Paginación de framework en tablas grandes — PROPUESTO
- **Context:** No se usa `GetPagedAsync`/`AsSplitQuery`; paginado manual + `ToList` en tablas altas (Q-AUD-002/003).
- **Decisión**: Migrar tablas altas a `GetSeekPagedAsync` (CBP) y usar `AsSplitQuery` en multi-Include.
- **Estado**: **PROPUESTO** (S19).

### ADR-007 — Seguridad de fase 1 (CFG-001/002 + SEC-005) — URGENTE
- **Context:** Fuga ciphertext (ConfigAppService:83), password plano en appsettings, MFA silenciado.
- **Decisión**: Eliminar `Console.WriteLine` del cipher, mover credenciales a User Secrets, propagar error MFA.
- **Estado**: **ACEPTADO** (Fase 1 Refactoring).

## 3. Correlación ADR → documento fuente

| ADR | Documento | Área | Estado |
|---|---|---|---|
| ADR-001 | S15-Authentication-Audit (F1) | Autenticación | ACEPTADO |
| ADR-002 | S15-Events + S15-Events-Coupling | Events | PROPUESTO |
| ADR-009 | S15-Logging-Audit | Logging | EN EVALUACIÓN |
| ADR-004 | S15-Logging-Context | Logging-contexto | PROPUESTO |
| ADR-005 | S15-Caching-Audit (CACH-001) | Caching | ACEPTADO |
| ADR-006 | S15-Data-QueryAudit | Data | PROPUESTO |
| ADR-007 | S15-Security-Logging + Configuration | Seguridad | ACEPTADO |

## 4. Reglas
- Mínimo por ADR: Contexto / Decisión / Consecuencia / Estado.
- No se duplican decisiones en docs de auditoría — referencia al ADR.
- Solo se define ADR para decisiones de adopción CBP o deuda de removida.

**Cierre S15**: decisiones consolidadas para ejecución en S16–S19 (ver Technical-Debt-Index y Refactoring-Plan).