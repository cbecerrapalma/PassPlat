# S17-F6 — Hallazgo: Event_* (greenfield domain events) no emitidos en vivo

- **Tipo**: N1 Evidencia / N3 Hallazgo (resuelto como diagnóstico)
- **Fuente**: S17 FASE 3–6 (instrumentación `Event_Published`/`Event_Handled`/`Event_Failed` en `CBP.Events.DomainEventDispatcher`)
- **Fecha**: 2026-08-10
- **Estado**: ✅ **RESUELTO como diagnóstico — CBP descartado como causa (S18)**

---

## Resumen

Durante la certificación en vivo (F6) de la instrumentación de eventos de dominio en la FASE 3 del sprint S17, se detectó que **no se emiten eventos estructurados `Event_*` en runtime**, pese a que:

- El `DomainEventDispatcher` está instrumentado con `EmitEventPublished`/`EmitEventOutcome`/`EmitEventFailed` (`CBP.Events`).
- Los tests unitarios T1–T6 **pasan (6/6)** y la suite total pasa **76/76**.
- La invocación por HTTP del trigger de detección de IP nueva (`POST /api/dispconfiables/trigger-new-ip/3`) retorna **200** y el flujo completo de login/JWT funciona (evidencia en `passplat-20260810.log`).

## Síntomas observados

1. `POST /api/dispconfiables/trigger-new-ip/3` → HTTP 200 (`{"mensaje":"NewIp event queued"}`), auth OK (`AUTHZ OK`, `JWT validado` en logs).
2. La tabla `IPs` recibe la fila creada (`10.0.0.99`, `Id=2`, `FecPrimerUso==UltUso`), lo que confirma que `IPService.DetectarNuevaIPAsync` se ejecuta y llega a la rama `if (esNueva)`.
3. **No aparece ningún log con `eventName":"Event_Published"`, `Event_Handled` ni `Event_Failed`** en `Logs\passplat-20260810.log`.

## Causas posibles (hipótesis — NO investigadas en esta sesión)

| # | Hipótesis | Detalle |
|---|-----------|---------|
| H1 | `_olog` es `null` en el dispatcher en runtime | `DomainEventDispatcher` es `scoped`; `ILoggerService` se obtiene vía `serviceProvider.GetService<ILoggerService>()` en el constructor. Si el `IServiceProvider` (root) no lo resuelve → no-op silencioso. La instrumentación JWT (`Jwt_Validated`) SÍ aparece — usa `ILoggerService` inyectado en `JwtTokenService` (`singleton`). |
| H2 | El evento no se publica realmente | `PublishAsync` lanza excepción silenciosa atrapada por el `try/catch` de `IPService.DetectarNuevaIPAsync` (L86-93). El catch traga sin log. |
| H3 | Los handlers no existen para este evento | `AddEventHandlersFromAssembly` registra handlers de `PassPlat.Aplicacion` (`NewIpDetectedEventHandler` etc.). Si no hay handler, `hasHandlers=false` → `Result.Success()` sin emitir outcome (pero `Event_Published` se emitiría igualmente en `DispatchAsync`). |
| H4 | `DiagnosticAuthMiddleware` (WRN) + pipeline rápido (40ms) no deja paso al log | Poco probable; `EmitEventPublished` se invoca al inicio de `DispatchAsync` antes del dispatch de handlers. |

## Impacto

- **No bloqueante** para la certificación de la FASE 3 (instrumentación de código compila + tests 6/6 + JWT en vivo OK).
- La **evidencia en vivo** de `Event_*` queda **pendiente** para el sprint de reparación.
- El código fuente de PassPlat (`IPService`, `DispConfiableService`, handlers) **no se modificó** en esta sesión.

## Acciones ya realizadas

1. Fix del binder pre-existente (`GetHandlerDelegate`/`Delegate.CreateDelegate` → helper genérico `CreateHandlerDelegateCore<TEvent>`) — aplicado en `CBP.Core/CBP.Events/DomainEventDispatcher.cs`. Este fix **sí** es necesario y quedó validado por T4–T6.
2. Flujo login/acceso **validado en vivo** post-fix:
   - `POST /api/auth/login/platform` → 200 (platform_admin)
   - `POST /api/auth/login` → 200 (admin_abarrotes, tenant ABARROTES)
   - `GET /api/accesos/usuario/3` (Bearer) → 200
   - `GET /api/auth/mis-tenants` (Bearer) → 200 (`{"id":2,"codigo":"ABARROTES"...}`)

## Prerrequisito de la prueba (reproducir): IP nueva

Para forzar `esNueva=true` (que `FecPrimerUso == UltUso` o `UltUso IS NULL`):

```sql
SET QUOTED_IDENTIFIER ON;
DELETE FROM IPs WHERE Direccion = '10.0.0.99'; -- y/o usar una dirección nueva
```

## Asignación

- **CBP.Events**: DESCARTADO como causa. `Event_Published`/`Event_Handled` certificados en vivo vía flujo incondicional `DeviceRevokedEvent` en S18 (evidencia `PassPlat.WebAPI\Logs\passplat-20260810.log` 17:03:29; `correlationId` W3C consistente request→dispatcher→handler→email). `Event_Failed` cubierto por tests/contrato T5.
- **Reparación del almacenamiento/detección de IP**: deuda independiente → **S19-Fx-IP-DETECTION-DETERMINISTIC** (heurística `esNueva` no determinista + try/catch silenciosos).

## Evidencia

- `PassPlat.WebAPI\bin\Debug\net10.0\Logs\passplat-20260810.log`
- `PassPlat.Aplicacion\Services\BBDD\IPService.cs` (L60-97, `DetectarNuevaIPAsync`)
- `CBP\CBP.Core\CBP.Events\DomainEventDispatcher.cs` (instrumentación L307-386)
- `PassPlat.WebAPI\Controllers\DispConfiablesController.cs` (trigger L62-79)

## Cierre temporal -->

## Cierre

- **Decisión**: La reparación del almacenamiento/detección de IP queda **PAUSADA** por exceso de tiempo invertido. Se documenta este hallazgo.
- **Resolución (S18)**: investigado y cerrado — H1–H4 descartadas contra el código real; causa raíz = heurística `esNueva` no determinista (`FecPrimerUso == UltUso`, dos lecturas independientes de `DateTime.Now`) + trigger con IP fija `10.0.0.99` → `PublishAsync` nunca se invoca. `Event_*` certificado en vivo vía `DeviceRevokedEvent` (flujo incondicional). Ver `S18-Discovery.md`.