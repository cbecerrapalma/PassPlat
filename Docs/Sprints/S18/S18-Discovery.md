# S18 — Discovery: `Event_*` no emitido en runtime (FASE 0)

**Tipo**: Investigación / Diagnóstico (read-only) + certificación FASE 1
**Fase**: F0 — DISCOVERY S18 → FASE 1 — Certificación `Event_*`
**Fecha**: 2026-08-10
**Estado**: ✅ Cerrada — causa raíz identificada y `Event_*` certificado en vivo

---

## 1. Contexto del hallazgo

S17-F6 reportó que `Event_Published` / `Event_Handled` / `Event_Failed` (catálogo
`CBP.Core.Logging.LoggingEvents`, scope `domainEvents`) nunca aparecían en runtime pese
a que el subsistema de eventos fue instrumentado. La certificación S16.4 declaró que
`Event_*` quedaba DEFERRED a `CBP.Events` como punto real de emisión, y S17 intentó
cerrarlo reproduciendo el flujo `trigger-new-ip`.

Las hipótesis históricas de S17-F6 (H1 `_olog` null, H2 excepción tragada, H3 sin
handlers, H4 routing DI) NO se comprobaron contra el código ni el runtime antes de tratarlas
como hechos.

## 2. Metodología

- **Orden MCP** (AGENTS.md): sequential-thinking (descomposición + priorización de
  hipótesis) → supermemory (contexto S16.2/17) → sharplens (análisis Roslyn del código
  real) → context7 (omitido: información local).
- **Modo**: solo lectura de código y datos. Sin modificar código fuente.
- **Evidencia consultada**:
  - Código fuente de `CBP.Events`, `CBP.Logging`, `PassPlat.Aplicacion`.
  - Logs de runtime: `PassPlat.WebAPI\bin\Debug\net10.0\Logs\passplat-20260805.log` … `passplat-20260810.log` (7 archivos).
  - Estado de datos en BD (tabla `IPs`).

## 3. Hallazgos verificados

### 3.1 El código del framework de eventos está correcto (se descartan H1-H4)

| Punto | Verificación | Resultado |
|-------|--------------|-----------|
| `EventPublisher.PublishAsync` | Delega en `_dispatcher.DispatchAsync` | ✅ Correcto |
| `DomainEventDispatcher` | Instrumentado (`EmitEventPublished`/`EmitEventOutcome`/`EmitEventFailed`); ctor `GetService<ILoggerService>()`; binder genérico `CreateHandlerDelegateCore<TEvent>` | ✅ Correcto |
| Registro DI `AddDomainEvents` | `AddSingleton(config)` + `AddScoped<IDomainEventDispatcher>` + `AddScoped<IEventPublisher>` | ✅ Correcto |
| `ILoggerService` | Singleton registrado por `AddCbpLogging`; `Jwt_Validated` (mismo logger) sí aparece en logs | ✅ Resoluble |
| Handlers reales | `NewIpDetectedEventHandler`, `SecurityAlertEventHandler`, `NewDeviceDetectedEventHandler`, `DeviceRevokedEventHandler` existen y se registran por ensamblado | ✅ Presentes |
| Sink Serilog | `appsettings.json` File sink con `outputTemplate="{Properties:j}"` | ✅ Listo para ver propiedades |

Ninguna de las hipótesis H1-H4 se sostiene con el código actual.

### 3.2 La causa real: el flujo del trigger nunca llega a publicar

**Ficha crítica — `IPRepository.ObtenerOCrear` (IPRepository.cs:33-52):**

- IP **existente**: setea `ip.UltUso = DateTime.Now` y la retorna → `UltUso` deja de ser
  null → `esNueva` queda false para siempre en invocaciones posteriores.
- IP **nueva**: `IP.Crear(...)` inicializa `FecPrimerUso = DateTime.Now` (T1) y acto seguido
  el repo setea `UltUso = DateTime.Now` (T2). **Son dos lecturas independientes del reloj**,
  sin garantía de igualdad.

**Heurística frágil — `IPService.DetectarNuevaIPAsync:66`:**

```csharp
var esNueva = ipEntity.UltUso == null || ipEntity.FecPrimerUso == ipEntity.UltUso;
```

Para que `PublishAsync` se ejecute, `FecPrimerUso` debe ser **bitwise igual** a `UltUso`.
Como ambas se asignan con llamadas separadas a `DateTime.Now`, la igualdad exacta es un
`race condition` dependiente de la resolución del reloj (no determinista).

**Evidencia de runtime (BD `IPs`):**

| Campo | Valor (trigger 03:27:45) |
|-------|--------------------------|
| `Id` | 2 |
| `Direccion` | `10.0.0.99` (IP fija del trigger) |
| `FecPrimerUso` | `2026-08-10 03:27:45.212` |
| `UltUso` | `2026-08-10 03:27:45.214` |
| Diferencia | **2 ms** → `esNueva = false` incluso en fila recién creada |

**Evidencia de logs (7 archivos `passplat-*.log`):**

- 3 invocaciones a `/api/dispconfiables/trigger-new-ip/3` (02:53:45, 03:24:36, 03:27:45),
  todas HTTP 200 (233ms, 154ms, 40ms).
- **0 ocurrencias** de `Event_Published` / `Event_Handled` / `Event_Failed` /
  `NewIpDetected` / `Email_Queued` en ningún log.

**Resultado**: `PublishAsync` nunca se invoca en el flujo trigger → no existe fallo en
CBP.Events, DI, handlers ni logging. Es un defecto de **heurística de detección de IP
nueva** + diseño del trigger con IP fija.

### 3.3 Agravantes

- Los `try/catch` de `IPService` (L86-93, L124-131) y `DispConfiableService` (L79-85,
  L180-186) tragan excepciones **sin registrar nada**, lo que ocultaría un fallo en
  `IEventPublisher` de forma silenciosa.
- El trigger `trigger-new-ip/{idUsuario}` usa la IP fija `"10.0.0.99"`; tras la primera
  ejecución, la fila existe y `UltUso` se refresca → el evento ya no puede dispararse sin
  borrar la fila antes.

## 4. Flujo alternativo certificable

Existen publishers **incondicionales** que alcanzan el dispatcher sin la heurística frágil
y con handler registrado:

| Evento | Publisher | Endpoint | Handler |
|--------|-----------|----------|---------|
| `NewDeviceDetected` | `DispConfiableService.DetectarNuevoDispositivoAsync` (si `EsConfiable=false`) | `POST /api/dispconfiables/trigger-new-device/{idUsuario}?idDisp=X` | `NewDeviceDetectedEventHandler` |
| `DeviceRevoked` | `DispConfiableService.RevocarConfianzaAsync` (incondicional) | `POST /api/dispconfiables/revocar-confianza/{idUsuario}/{idDisp}` | `DeviceRevokedEventHandler` |

**Recomendación**: certificar `Event_*` mediante `DeviceRevokedEvent`
(`revocar-confianza`), cuya publicación no depende de condiciones de reloj. El fix de la
heurística `esNueva` se traslada como deuda explícita (independiente de CBP).

## 5. Decisiones tomadas

1. **No tocar CBP por este hallazgo** — no hay defecto en el framework de eventos.
2. **Trasladar a deuda** el fix de `esNueva`: detectar IP nueva de forma determinista
   (p. ej. `UltUso == null` como único criterio, sin comparar timestamps) y corregir los
   `try/catch` vacíos para registrar fallos de publicación.
3. **Certificar `Event_*`** en FASE 1 vía `DeviceRevokedEvent` (flujo incondicional).
4. Preservar `{Properties:j}`, binder `CreateHandlerDelegateCore<TEvent>`, y contrato
   `CBP.Logging` (sin cambios).

## 6. FASE 1 — Certificación `Event_*` con flujo alternativo (✅ COMPLETADA)

**Flujo**: `POST /api/dispconfiables/revocar-confianza/3/1` (usuario `admin_abarrotes`,
tenant 2, app 1) → `DispConfiableService.RevocarConfianzaAsync` → **`DeviceRevokedEvent`**
publicado incondicionalmente → handler `DeviceRevokedEventHandler` → `IEmailQueue`.

**Evidencia en vivo** (log `PassPlat.WebAPI\Logs\passplat-20260810.log`, 17:03:29):

| Timestamp | Evento | Propiedades clave |
|-----------|--------|-------------------|
| 17:03:29.919 | `Jwt_Validated` | `correlationId=00-bd0aabbd...-00` (W3C) |
| 17:03:29.973 | **`Event_Published`** | `scope=domainEvents`, `operation=Publish`, `category=application`, `userId=3`, `tenantId=2`, correlationId idéntico |
| 17:03:29.979 | **`Email_Queued`** | `scope=email`, `operation=Queue`, correlationId idéntico |
| 17:03:29.979 | EmailBackgroundService | "Omitiendo EmailJob DeviceRevoked para usuario 3: sin email configurado" (usuario sin email → skip correcto, no error) |
| 17:03:29.980 | **`Event_Handled`** | `DeviceRevoked por DeviceRevokedEventHandler`, `scope=domainEvents`, correlationId idéntico |
| 17:03:30.050 | HTTP 204 en 132ms | Response OK |

**Resultado**: `Event_Published` y `Event_Handled` EMITIDOS en runtime real, con
`correlationId` W3C propagado a través de request → dispatcher → handler → email queue.
El subsistema de eventos CBP **funciona correctamente de extremo a extremo**.

**Observación**: no se observó `Event_Failed` en este flujo (el handler no falló). El caso
`Event_Failed` quedaría cubierto por el propio dispatcher ante un handler que retorne
`Result.Failure` (p. ej. `NewIpDetectedEventHandler` con `NOTIFY_ERROR`).

## 7. Archivos relevantes

| Archivo | Rol |
|---------|-----|
| `PassPlat.Datos/Repositories/IPRepository.cs` | `ObtenerOCrear` refresca `UltUso` siempre |
| `PassPlat.Aplicacion/Services/BBDD/IPService.cs` | `esNueva` no determinista (L66), try/catch vacíos |
| `PassPlat.WebAPI/Controllers/DispConfiablesController.cs` | `trigger-new-ip` IP fija; `revocar-confianza` disponible |
| `PassPlat.Aplicacion/Services/SPro/DispConfiableService.cs` | `RevocarConfianzaAsync` publica incondicionalmente |
| `PassPlat.Aplicacion/Services/Security/DispConfiableEventHandlers.cs` | `DeviceRevokedEventHandler` |
| `CBP.Core/CBP.Events/DomainEventDispatcher.cs` | Instrumentación `Event_*` (verificada, sin cambio) |
| `PassPlat.WebAPI/bin/Debug/net10.0/Logs/passplat-*.log` | Evidencia de runtime (0 eventos) |