# S14 — F10: CBP.Events Analysis

> Sprint S14 · FASE F10 (read-only) · Auditoría de uso de `CBP.Events` en dominios críticos.

---

## Framework `CBP.Events`

- **Base**: `EventBase` (no `DomainEvent`) — genérico para cualquier capa.
- **Dispatcher**: `DomainEventDispatcher` — modos paralelo/secuencial.
- **CorrelationId**: Auto-generado, propagable via `WithCorrelationId()`.
- **Suscripción**: Convención `IEventHandler<TEvent>` registrada en DI.

---

## Auditoría de uso en PassPlat

### Búsqueda de eventos publicados

```bash
grep -r "DomainEventDispatcher\|EventBase\|IEventHandler" --include="*.cs" PassPlat.Aplicacion/
```

### Hallazgos

| Área | Eventos publicados | Dispatcher | Handlers |
|------|-------------------|------------|----------|
| **Login/Autenticación** | ❌ Ninguno | No | No |
| **OAuth** | ❌ Ninguno | No | No |
| **Email** | ❌ Ninguno (usa `IEmailQueue` + `EmailBackgroundService`) | No | No |
| **Cambio Tenant** | ❌ Ninguno | No | No |
| **Configuración** | ❌ Ninguno | No | No |
| **Sesiones** | ❌ Ninguno | No | No |
| **Seguridad (Bloqueos, MFA, Pwd)** | ❌ Ninguno | No | No |
| **Auditoría** | ❌ Ninguno (usa SPs + tablas `AuditoriaPwd`, `AudIdenExt`, `EmailLog`) | No | No |

---

## Integración `CBP.Events` actual

### Registro en DI
```csharp
// PassPlat.Aplicacion/AplicacionDependencyInjection.cs
services.AddScoped<DomainEventDispatcher>();
// No hay IEventHandler<T> registrados
```

### Eventos definidos en PassPlat
```bash
# No hay clases heredando de EventBase en PassPlat.Aplicacion/
# Solo eventos de CBP.Core.CBP.Events (base)
```

---

## Gap identificado

**CBP.Events no se usa en PassPlat** — toda la comunicación entre dominios es:
1. **Síncrona** — llamadas directas a servicios/repositorios.
2. **Tablas de auditoría** — `AuditoriaPwd`, `AudIdenExt`, `EmailLog`, `HistorialIdenExt` (persistencia directa).
3. **Email queue** — `IEmailQueue` + `EmailBackgroundService` (polling DB).
4. **Notificaciones** — Tabla `Notificaciones` (polling UI).

---

## Evaluación de necesidad

| Dominio | Evento candidato | Valor | Complejidad |
|---------|------------------|-------|-------------|
| Login exitoso | `UserLoggedInEvent` | Auditoría, notificaciones, métricas | Baja |
| Login fallido | `UserLoginFailedEvent` | Seguridad, alertas | Baja |
| OAuth vinculado | `ExternalIdentityLinkedEvent` | Auditoría, email, métricas | Media |
| Tenant switched | `TenantSwitchedEvent` | Limpieza caché, permisos | Media |
| Password cambiado | `PasswordChangedEvent` | Auditoría, email, invalidar sesiones | Media |
| Usuario bloqueado | `UserBlockedEvent` | Seguridad, email admin | Baja |
| Config cambiada | `ConfigChangedEvent` | Invalidar cachés | Media |

---

## Decisión S14

### NO implementar eventos en S14

**Razones:**
1. **Fase read-only** — S14 F1–F11 son auditoría/documentación.
2. **Arquitectura actual funcional** — Tablas auditoría + Email queue + Notificaciones cubren necesidades.
3. **Complejidad incremental** — Introducir `DomainEventDispatcher` + handlers + testing requiere sprint dedicado.
4. **Deuda técnica conocida** — Documentada en S13 CBP audit score 9/10 ("CBP.Events unused gap").

### Recomendación

- **Documentar gap** en `S14-CBP-Events-Analysis.md`.
- **Backlog próximo sprint**: Implementar eventos críticos (Login, TenantSwitch, PasswordChange) con `DomainEventDispatcher` paralelo.
- **Patrón**: `EventBase` + `IEventHandler<T>` + registro DI + tests de integración.

---

## Conclusión

**GAP DOCUMENTADO** — `CBP.Events` disponible en framework pero **no usado en PassPlat**.  
Comunicación entre dominios: síncrona + tablas auditoría + Email queue.  
Próximo sprint: evaluar eventos de alto valor (Login, TenantSwitch, PasswordChange).