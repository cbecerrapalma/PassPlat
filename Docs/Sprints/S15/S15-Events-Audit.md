# S15-Events-Audit.md — Eventos de Dominio / Domain Events (F3)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Events-Coupling, Certification
# Area            Eventos de dominio (F3)
# Framework CBP   CBP.Events (EventBase, IDomainEvent, IDomainEventDispatcher, DomainEventDispatcher, IEventPublisher, EventPublisher, AddDomainEvents, AddEventHandlersFromAssembly, CorrelationId)
# Cobertura       Aplicacion | Dominio
# Evidencia       IPEvents.cs · DispConfiableEvents.cs · AuthenticationEvents.cs · IPEventPublisher (sealed static) · DispConfiableEventPublisher (sealed static) · No AppDomainEvents registrado · EmailQueue.EnqueueAsync
# Resultado       FAIL (eventos definidos con CBP base pero NO se consume DomainEventDispatcher; publicadores static sin DI/handlers)
# Cobertura       20 % (ver F11)
# Riesgo          Medio
# Prioridad       Alta

---

## 1. Proposito

Auditar como PassPlat implementa eventos de dominio/notificacion: usa el pipeline de CBP.Events (`DomainEventDispatcher`, `IEventPublisher`, handlers) o los emite de forma directa/sincrona via publicadores staticos y cola de email.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Situacion actual

### 3.1 Eventos definidos en PassPlat (heredan `CBP.Events.EventBase`)

| Evento | Clase | Ruta |
|---|---|---|
| `NewIpDetectedEvent` | record : EventBase | `Aplicacion\Services\Security\IPEvents.cs` |
| `SecurityAlertEvent` | record : EventBase | `Aplicacion\Services\Security\IPEvents.cs` |
| `DispConfiableEvents` (NewDevice, DeviceRevoked) | record : EventBase | `Aplicacion\Services\Security\DispConfiableEvents.cs` |
| `AuthenticationEvents` (Login, OAuth, etc.) | EventBase | `Aplicacion\Services\Authentication\AuthenticationEvents.cs` |

Son correctos: `EventBase`, `EventType`, `FechaDeteccion`, `CorrelationId` disponible. Esto es **REUTILIZAR el modelo EventBase** pero el dispatcher no se usa.

### 3.2 Publicacion actual: publicadores staticos NOT in DI

| Publisher | Tipo | Evidencia | Uso |
|---|---|---|---|
| `IPEventPublisher` | `public static class` (no inyectable) | `IPEvents.cs:32` | LLamado `IPService.cs:74,113` |
| `DispConfiableEventPublisher` | `public static class` | `DispConfiableEvents.cs:32` | LLamado `DispConfiableService.cs:70,166` |

Ambos publican directamente encolando a `IEmailQueue.EnqueueAsync(new EmailJob(...))` — NO pasan por `DomainEventDispatcher` ni por `IEventPublisher`.

### 3.3 Dispatcher NO registrado

| Item | Estado | Evidencia |
|---|---|---|
| `AddDomainEvents()` | NO registrado en Program.cs DI | grep Program/AplicacionDI = 0 |
| `AddEventHandlersFromAssembly` | NO usado | grep = 0 |
| `IDomainEventDispatcher` | NO inyectado en ningun servicio | grep DomainEventDispatcher en PassPlat = 0 (solo la definicion CBP) |
| `IEventPublisher` | NO inyectado | grep = 0 |
| La difusion de eventos | se hace via cola de EMAIL directamente (sincrono en el servicio) | cada EventPublisher llama emailQueue.EnqueueAsync |

## 4. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **EVENT-001** | Eventos definidos heredan correctamente de `CBP.Events.EventBase` (reuso de modelo CBP). | `IPEvents.cs:1 (using CBP.Events)`, `EventBase` herencia | PASS |
| **EVENT-002** | **Los eventos NO se despachan por DomainEventDispatcher** — solo encola email, no hay handlers/eventos de dominio desacoplados. Se pierde: auditabilidad, desacoplamiento, reproducibilidad, manejo de fallos parciales de handlers. | grep DomainEventDispatcher/AddDomainEvents = 0; solo static Publishers | **FAIL** |
| **EVENT-003** | Publicadores son **static sealed** (no inyectables via DI) — patrón "static helper" que viola CBP DI y dificulta test/mocks, ademas de que subscriben el singleton (no DIs). | `IPEvents.cs:32 public static class` | WARNING |
| **EVENT-004** | El tipo de evento se encadena a EmailJob (no hay lista neutral de eventos desacoplados del email). Falta el `CorrelationId` correctamente propagado: en el Publisher se pasa pero el `EmailJob` no usa el `DomainEvent` directamente. | Publisher encola directamente EmailJob ldtoIEmailQueue | WARNING |
| **EVENT-005** | `AuthenticationTokenService` / AuthService no emiten event domain; la notificacion (email) se emite desde el servicio de negocio directamente (sin evento intermedio). | no hay handlers de AuthenticationEvents | WARNING |
| **EVENT-006** | `EmailBackgroundService`/`EmailQueue` son el transporte; no usan `DomainEventDispatcher` ni `Emitir a cola via evento`. La cola es el efecto secundario, no el event bus. | `IEmailQueue.EnqueueAsync` | JUSTIFICAR |
| **EVENT-007** | No hay `CorrelationId` propagation formal via EventBase.From; cada publicador lo pasa manualmente (funciona pero duplica). | `PublishNewIpAsync(... correlationId ...)` | WARNING |

## 5. Clasificacion general
- **Aplicado de CBP.Events**: solo `EventBase` (tipo base). Dispatcher/handlers/publisher DI desusados: 0% de la capacidad del framework.
- **Capa propia paralela**: `IPEventPublisher`, `DispConfiableEventPublisher`, `IEmailQueue` — reimplementan observacion acoplada a email.
- Duplicacion funcional: **ACOPLADA** — eventos redundantes del `DomainEventDispatcher` con los publicadores staticos.

## 6. Resultado F3
- **FAIL**: El subsistema de eventos no usa `CBP.Events.DomainEventDispatcher` ni `IEventPublisher`. Solo se usa `EventBase` para tipar eventos encolados a email.
- Cobertura CBP en sector "Events": `0%` (no se llama `AddDomainEvents`).
- `EVENT-002` y `EVENT-003` principal riesgo: no desacopla la notificacion del efecto, no handler, no reusar la clase.

### Insumo para F12
- Refactorizar los `sealed static` Publishers → inyectar `IEventPublisher` (o usar `IDomainEventDispatcher` de CBP) cuando sea necesario desacoplar.
- Evaluar si el email es el unico consumidor: si el evento solo produce email, se puede conservar el `IEmailQueue` (JUSTIFICAR) pero opcionalmente transporte de SBC etc en el futuro.
- Para la matriz F11: area Events cobertura en "Datos/DomainEvent despachado" = FAIL, se justifica para la notification shoes (email) usar SIN dispatcher mientras no haya MÁS consecuentes.

### Scoring (ver F11)
- Integración CBP: 0.5 (define via EventBase) 
- Duplicación: alta (static publishers duplican Queue)
- Puntaje area: bajo (a ajustar con la matriz global).

### 6.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| EVENT-001 | PASS | REUTILIZAR (EventBase) | — | — | Alta |
| EVENT-002 | **FAIL** | REEMPLAZAR (usar DomainEventDispatcher/handlers) | **Alta** | **P1** | Alta |
| EVENT-003 | WARNING | REEMPLAZAR (publishers DI no static) | Media | P2 | Alta |
| EVENT-004 | WARNING | REEMPLAZAR (desacoplar email del evento) | Media | P2 | Media |
| EVENT-005 | WARNING | EXTENDER (emitir eventos de auth) | Media | P2 | Media |
| EVENT-006 | PASS | JUSTIFICAR (colmilla como transporte) | — | — | Alta |
| EVENT-007 | WARNING | EXTENDER (propagar CorrelationId) | Baja | P3 | Media |

### 6.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 20 % (solo EventBase) |
| Architecture Score | 38 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-EVENT-001..007 |

**Ver tambien**: `S15-Events-Coupling-Audit.md` — cuantificacion del acoplamiento sincrono eventos→email.