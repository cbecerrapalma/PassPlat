# S15-Events-Coupling-Audit.md — Acoplamiento de Eventos (documento compañero de F3)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Events-Audit
# Depende de      Events-Audit
# Influye en      Refactoring, Certification
# Área            Cuantificación del acoplamiento sincrónico de la capa de eventos al transporte de email
# Framework CBP   CBP.Events (EventBase, IDomainEventDispatcher, IEventPublisher), CBP.Emails (IEmailQueue)
# Cobertura       Aplicacion | Dominio
# Evidencia       IPEvents.cs (IPEventPublisher static, usando IEmailQueue param con EmailJob) · DispConfiableEvents.cs (DispConfiableEventPublisher static) · AuthenticationEvents.cs · servicios que encolan (IPService, DispConfiableService)
# Resultado       FAIL (acoplamiento funcional alto: los eventos CBP (EventBase) se «publican» solo como trigger para encolar EmailJob; ningún consumidor distinto al email; no se usa DomainEventDispatcher)
# Cobertura       20 %

---

## 1. Proposito

Documento compañero de `S15-Events-Audit.md`. Objetivo: **cuantificar y clasificar el acoplamiento** entre eventos de dominio (modelados en `CBP.Events.EventBase`) y el efecto secundario (email). Ver si existe desacople real o si «evento» = «encolar email». Proporciona la base introvertible para Fase F/Duplication y Refactoring Plan F12.

## 2. Metodo (estructura obligatoria)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Publicadores y su acoplamiento al email (verificado)

| Publicador | Tipo | Efecto | Acoplamiento a Email | Desacople (handler) | Confidence |
|---|---|---|---|---|---|
| `IPEventPublisher.PublishNewIpAsync` | `public static class` | `emailQueue.EnqueueAsync(new EmailJob(EmailJobKind.NewIp,...))` | **Alto (directo)** | sin handler | Alta |
| `IPEventPublisher.PublishAlertAsync` | static | `EnqueueAsync(EmailJob SecurityAlert)` | Alto | sin handler | Alta |
| `DispConfiableEventPublisher.PublishNewDeviceAsync` | static | `EnqueueAsync(EmailJob NewDevice)` | Alto | sin handler | Alta |
| `DispConfiableEventPublisher.PublishDeviceRevokedAsync` | static | `EnqueueAsync(EmailJob DeviceRevoked)` | Alto | sin handler | Alta |
| `AuthenticationEvents` | EventBase (no publicador) | no se emite aún | — | — | Media |
| `EmailBackgroundService` | HostedService | procesa cola (mail) | — | transporte | Alta |

Todos comparten el **mismo patrón**: reciben `IEmailQueue` (por parámetro o estático) y encolan `EmailJob`; **no** hay `IDomainEventDispatcher` que invoque handlers de dominio. Unico consumidor = email.

## 4. Escalmetro de acoplamiento

| Nivel | Significado | Estado PassPlat |
|---|---|---|
| 0 | evento emitido, sin suscriptor | AuthenticationEvents (no emitido) |
| 1 | suscriptor único interno (email) | NewIp, Alert, NewDevice, DeviceRevoked |
| 2 | suscriptor + comunicación asíncrona con bus | no presente |
| 3 | handlers múltiples / SAGA / side effects | no presente |

**PassPlat actualmente en Nivel 1**: el evento solo dispara 1 email como efecto; sin bus, sin reintento multi-handler, sin reglas de negocio adicionales. El `IEmailQueue` funciona como transporte, no como evento decorplado.

## 5. Hallazgos de acoplamiento

| ID | Hallazgo | Evidencia | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| **COUP-001** | Los 4 publicadores static encolan `EmailJob` **directamente** con el `EmailJobKind`, sin emitir un evento reutilizable y sin handler que recompute. El «evento» y la «notificacion email» son el mismo concepto (acoplado). | `IPEvents.cs:27` `PublishNewIpAsync` al `EnqueueAsync(new EmailJob...)` | **FAIL** | REEMPLAZAR | Alta |
| **COUP-002** | Eventos definidos como `CBP.Events.EventBase` **nunca se replican en un bus**; no hay `DomainEventDispatcher`, `IEventPublisher`, handlers. Solo sirven como tipado para derivar el EmailJobKind. | grep Dispatcher = 0 | **FAIL** | REEMPLAZAR | Alta |
| **COUP-003** | `IEmailQueue` se pasa como **parámetro de método estático** (no inyectado) — el publisher no es testable ni swappable sin tocar el servicio. | `IPEvents.cs:27` (firma con `IEmailQueue emailQueue`) | **FAIL** | REEMPLAZAR (DI) | Alta |
| **COUP-004** | Múltiples eventos → múltiples `EmailJobKind` (security alert, new-device, new-ip, device-revoked, role-*, tenant-*, app-*, mfa-*): la cola de email ya es el mapa real de eventos; no hay capa de eventos de dominio neutral | `PassPlatEmailService.cs:162-181` (22 kinds) | WARNING | JUSTIFICAR | Media |

## 6. Coste de acoplamiento (por decisión)

| Tipo de cambio | Esfuerzo | Riesgo |
|---|---|---|
| Agregar un 2º consumidor de un evento (p.ej. auditoría) | Alto (hay que refactorizar publisher → bus) | Medio |
| Enviar al email en otro formato | Alto | Medio |
| Testear un evento aislado | Alto (static + cola directa) | Alto |

## 7. Estado de el refactor deseado (nivel 2)

Para ir a acoplamiento débil haría falta:
1. `publisher` (no void si se emite) → emitir a `IDomainEventPublisher` (CBP).
2. Registrar `AddDomainEvents()` + handler por evento (`AddEventHandlersFromAssembly`).
3. El handler del email se inscribe al evento (no el publisher sea un EmailQueue).

**Beneficio neto**: testeabilidad, handler 2º para auditoría/denuncia sin tocar email, CorrelationId propagado por el bus.

## 8. Resultado (acoplamiento)

- **Acoplamiento ALTO**: cada evento==EmailJob; no hay capa de separación; publicers static no depesan.
- Riesgo de evolución: **Alto** si el negocio requiere 2º consumidor o auditoría de eventos.
- Backlog F12: convertir publishers→`IEventPublisher` + handlers; diferencia email como transporte.

## 9. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP | 15 % (solo EventBase, sin bus) |
| Architecture Score | 30 / 100 |
| Confidence | Alta |
| Technical Debt | TD-COUP-01..04 (crítico de acoplamiento) |