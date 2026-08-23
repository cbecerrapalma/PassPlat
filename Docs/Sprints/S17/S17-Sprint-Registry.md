# S17-Sprint-Registry.md — Registro de Trazabilidad S17 (instrumentación framework CBP)

# Estado          Cerrado formalmente (2026-08-10)
# Nivel           Ejecución (implementación en framework CBP + certificación)
# Origen          S16.4 (P3 Events DEFERRED → CBP.Events; Jwt_Validated → CBP.Authentication.JwtBearer) + S17-Phase2-Plan (Opción A)
# Regla           NO tocar CBP.Events/CBP.Authentication.JwtBearer fuera del alcance certificado. Contrato CBP.Logging v1.0 sin cambios.

---

## Flujo de trazabilidad

```
S16.4 → Event_* DEFERRED → S17 instrumentación CBP.Events/CBP.JwtBearer → S18 certificación runtime → Event_* = certificado
        Jwt_Validated → S17 instrumentación JwtTokenService → certificado en vivo
```

---

## Objetivo
Instrumentar el framework CBP (`CBP.Events.DomainEventDispatcher`, `CBP.Authentication.JwtBearer.JwtTokenService`)
con el contrato `ILoggerService` (opcional, DIP-compatible) para emitir `Event_Published/Handled/Failed` y
`Jwt_Validated/Jwt_Expired`, cerrando las deudas registradas de S16.4 con evidencia en vivo, sin romper hosts sin logging.

---

## Decisiones de arquitectura

| Decisión | Valor | Fecha |
|---|---|---|
| Alternativa | **Opción A** — framework → CBP (Core), `ILoggerService` opcional vía `GetService` | 2026-08-10 |
| Proyectos | `CBP.Authentication.JwtBearer`, `CBP.Events` (+ProjectRef → `CBP`) | 2026-08-10 |
| Contratos públicos | Sin cambios (aditivos internos / overload ctor JWT) | 2026-08-10 |
| Contrato CBP.Logging v1.0 | Sin cambios | 2026-08-10 |
| Documento de plan | `S17-Phase2-Plan.md` (aprobado) | 2026-08-10 |

---

## Registro de tareas

| ID | Descripción | Resultado | Estado |
|---|---|---|---|
| S17-001 | ProjectRef `CBP` en `CBP.Authentication.JwtBearer.csproj` y `CBP.Events.csproj` | Build OK | ✅ F3 |
| S17-002 | JwtTokenService: overload ctor `ILoggerService?` + `Jwt_Validated`/`Jwt_Expired` vía catálogos | 0 errores | ✅ F3 |
| S17-003 | DomainEventDispatcher: `_olog` vía GetService (ctor DI) / null (manual) + `Event_Published`/`Event_Handled`/`Event_Failed` | 0 errores | ✅ F3 |
| S17-004 | Fix binder pre-existente `CreateHandlerDelegateCore<TEvent>` (sin él ningún handler real se ejecutaba) | T4–T6 PASS | ✅ F3 |
| S17-005 | Build `PassPlat.slnx` | 0 errores, solo NU1603 pre-existente | ✅ T8 |
| S17-006 | Suite xUnit | **76/76 PASS** (70 baseline + 6 S17 T1–T6) | ✅ T9 |
| S17-007 | Contract tests `CacheLogContractTests` | 4/4 PASS (incluido en 76/76) | ✅ T7 |
| S17-008 | Grounding DI (`AddCbpLogging` singleton, JWT singleton, dispatcher scoped) | Sin mismatch | ✅ F5 |
| S17-009 | Evidencia en vivo JWT/Background | `Jwt_Validated`, `Jwt_Generated`, `Login_Succeeded`, `Background_*` en log | ✅ F6 |
| S17-010 | Evidencia en vivo Event_* | `Event_Published` + `Event_Handled` vía `revocar-confianza` (S18) | ✅ F6/S18 |

---

## Pruebas S17 (F3/F4)

| ID | Prueba | Resultado |
|---|---|---|
| T1 | `ValidateToken` éxito → `Jwt_Validated` | ✅ |
| T2 | `ValidateToken` expirado → `Jwt_Expired` + retorno null | ✅ |
| T3 | `ValidateToken` inválido → SIN `Jwt_Validated` (anti-falsificación) | ✅ |
| T4 | Dispatcher (modo DI) éxito → `Event_Published` + `Event_Handled` (1/handler) | ✅ |
| T5 | Handler falla → `Event_Failed` + Result.Failure conservado | ✅ |
| T6 | Modo manual (Dictionary) sin ILoggerService → no-op sin fallo | ✅ |
| T7 | Contract `CacheLogContractTests` 4/4 | ✅ |
| T8 | `dotnet build PassPlat.slnx` 0 errores / 0 warnings nuevos | ✅ |
| T9 | `dotnet test PassPlat.slnx` | ✅ 76/76 |
| T10 | E2E | ✅ PASS — `Event_*` certificado parcialmente en vivo (Published + Handled vía `revocar-confianza`); `Event_Failed` cubierto por pruebas/contrato, NO declarado E2E certificado |

---

## Evidencia en vivo

| Evento | Ruta | Evidencia |
|---|---|---|
| `Jwt_Validated` | GET /api/apps (Bearer) | `PassPlat.WebAPI\bin\Debug\net10.0\Logs\passplat-20260810.log` |
| `Jwt_Generated` | login platform/tenant | idem |
| `Login_Succeeded` | `/api/auth/login` | idem |
| `Background_JobStarted/Finished` | jobs al arranque | idem |
| `Event_Published` | POST /api/dispconfiables/revocar-confianza/3/1 | `PassPlat.WebAPI\Logs\passplat-20260810.log` 17:03:29.973 (scope=domainEvents, operation=Publish, correlationId `00-bd0aabbd...`) |
| `Event_Handled` | idem | 17:03:29.980 (`DeviceRevoked por DeviceRevokedEventHandler`) |
| `Email_Queued` | idem (handler → IEmailQueue) | 17:03:29.979 (correlationId idéntico) |

---

## Matriz de cierre de hallazgos

| Hallazgo | Estado | Resolución |
|---|---|---|
| S16.4 P3 `Event_*` DEFERRED | ✅ RESUELTO | S17 instrumentación + S18 certificación runtime (Published/Handled en vivo; Failed por contrato) |
| S16.4 `Jwt_Validated` pendiente | ✅ RESUELTO | S17 instrumentación JwtTokenService + evidencia en vivo |
| S17-F6 EventIP NoEmitido | ✅ RESUELTO como diagnóstico | CBP descartado (S18); causa raíz heurística `esNueva` no determinista + IP fija; reparación IP → deuda `S19-Fx-IP-DETECTION-DETERMINISTIC` |
| `Event_Failed` runtime | ⚠️ No observado (handler no falló) | Cubierto por T5 + contrato del dispatcher (handler retorna Result.Failure → `Event_Failed`) |

---

## Gates

| Gate | Estado |
|---|---|
| F3 Implementación | ✅ ProjectRefs + JwtTokenService + DomainEventDispatcher + fix binder |
| F4 Build + tests | ✅ Build 0 errores (NU1603 pre-existente) · 76/76 |
| F5 Grounding DI | ✅ Sin mismatch de lifetime |
| F6 Evidencia en vivo | ✅ JWT + Background (S17) · Event_* (S18) |
| F7 Documentación | ✅ `Logging.EventCatalog.md` v1.1 actualizado |
| F8 Cierre de sesión / Gate | ✅ **S17 = CLOSED / Gate PASS** (`S17-Closure.md`) |
| S18 integrado | ✅ `S18-Discovery.md` integra la certificación Event_* completa |

---

## Deudas no bloqueantes (tras cierre)

| Deuda | Sprint | Estado |
|---|---|---|
| Reparación detección IP determinista (`esNueva`) + try/catch silenciosos | **S19-Fx-IP-DETECTION-DETERMINISTIC** | ⏸️ Trasladada |
| `Sql_SlowQuery` (sin interceptor EF, P6 alcance cerrado) | Backlog | ⏸️ Existente |
| `Event_Failed` observación E2E opcional | Backlog/opcional | ⏸️ No bloqueante |

---

## Trazabilidad upstream (S16 — sin reabrir)

S16.4 cerró con P3 Events ⏳ DEFERRED y `Jwt_Validated` ⏳. Este registro confirma que ambas deudas fueron
resueltas posteriormente por S17 (instrumentación) + S18 (certificación runtime). La fila correspondiente de
`S16-Sprint-Registry.md` se actualizó solo como trazabilidad documental (no se reabre S16.4).