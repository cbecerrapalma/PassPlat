# S17-Closure.md — Cierre formal del Gate S17

**Estado**: ✅ **CLOSED / Gate PASS (2026-08-10)**
**Campaña**: S17 — Instrumentación framework CBP (JWT + Events) sobre baseline S16.4/RC1
**Documentos de referencia**: `S17-Sprint-Registry.md` · `S17-Phase2-Plan.md` · `S18-Discovery.md` · `S16-Sprint-Registry.md`
**Regla**: S17 se declara cerrado únicamente tras verificar build + tests + reconciliación documental.

---

## 1. Criterios de cierre

| Criterio | Estado | Evidencia |
|---|---|---|
| Instrumentación JWT | ✅ PASS | `JwtTokenService.cs` — `Jwt_Validated`/`Jwt_Expired` con catálogos |
| `Jwt_Validated` runtime | ✅ PASS | Evidencia en vivo (`PassPlat.WebAPI\bin\Debug\net10.0\Logs\passplat-20260810.log`) |
| `Jwt_Expired` implementación/tests | ✅ PASS | T2 (unit) |
| Instrumentación Events | ✅ PASS | `DomainEventDispatcher.cs` — `Event_Published`/`Event_Handled`/`Event_Failed` |
| `Event_Published` runtime | ✅ PASS | S18: vía `revocar-confianza` 17:03:29.973 (correlationId W3C) |
| `Event_Handled` runtime | ✅ PASS | S18: 17:03:29.980 (`DeviceRevoked por DeviceRevokedEventHandler`) |
| `Event_Failed` | ✅ contrato/tests | T5 + contrato dispatcher (NO declarado E2E certificado) |
| Tests S17 T1–T6 | ✅ 6/6 | Unit framework |
| Suite completa | ✅ 76/76 | `dotnet test PassPlat.slnx` (51s) |
| Build | ✅ 0 errores | `dotnet build PassPlat.slnx` — solo NU1603 pre-existente (2 warnings) |
| CorrelationId E2E | ✅ PASS | `00-bd0aabbd...` propagado request→dispatcher→handler→email |
| S18 integrado | ✅ PASS | `S18-Discovery.md` — causa raíz + certificación runtime |
| Hallazgo S17-F6 | ✅ Resuelto como diagnóstico | `S17-F6-EventIP-NoEmitido-Hallazgo.md` — CBP descartado |
| Deuda IP | ⏸️ Trasladada | `S19-Fx-IP-DETECTION-DETERMINISTIC` |
| Sql_SlowQuery | ⏸️ Backlog existente | Sin interceptor EF (P6 alcance cerrado) |
| Documentación reconciliada | ✅ | S17-Phase2/Registry/Closure + S16 trazabilidad + catálogo v1.1 + AGENTS.md |

---

## 2. Gate técnico (ejecutado 2026-08-10)

```text
dotnet build PassPlat.slnx   → 0 errores (2 warnings NU1603 pre-existentes) ✅
dotnet test PassPlat.slnx    → 76/76 PASS (70 baseline + 6 S17 T1–T6) ✅
```

> No se repitió la campaña E2E: la evidencia de runtime de S18 (17:03:29) constituye el
> runtime real exigido. Solo se habría investigado ante fallo de build/test o discrepancia
> artefacto↔código — no ocurrió.

---

## 3. Estado funcional post-cierre

```
S16.4 CLOSED ✅ → Gate C PASS → RC1 APPROVED
        │
        ▼
S17 CLOSED ✅ (build + 76/76 + documentación)
        │
        ├─ JWT instrumentation ........ PASS
        ├─ Events instrumentation ..... PASS
        ├─ Runtime JWT ................ PASS
        ├─ Runtime Event Published .... PASS
        ├─ Runtime Event Handled ...... PASS
        ├─ Event Failed ............... contrato/tests
        └─ reconciliación documental .. ✅ (registry + catálogo + trazabilidad + AGENTS)
                 │
                 ▼
        S19 → reparación IP (deuda independiente, no bloqueante)
```

---

## 4. Evidencia anexa

| Artefacto | Contenido |
|---|---|
| `PassPlat.WebAPI\bin\Debug\net10.0\Logs\passplat-20260810.log` | JWT/Background/Login en vivo (F6 S17) |
| `PassPlat.WebAPI\Logs\passplat-20260810.log` | `Event_Published`/`Event_Handled`/`Email_Queued` 17:03:29 (S18) |
| `PassPlat.Aplicacion.Test\Tests\Framework\S17\JwtTokenServiceInstrumentationTests.cs` | T1–T3 |
| `PassPlat.Aplicacion.Test\Tests\Framework\S17\DomainEventDispatcherInstrumentationTests.cs` | T4–T6 |

---

## 5. Deudas no bloqueantes

| Deuda | Sprint | Nota |
|---|---|---|
| Detección IP determinista (`esNueva`) + try/catch silenciosos | **S19-Fx-IP-DETECTION-DETERMINISTIC** | Independiente de S17/S18; causa raíz identificada en S18 |
| `Sql_SlowQuery` | Backlog | P6 alcance cerrado (sin interceptor EF) |
| `Event_Failed` observación E2E | Backlog/opcional | Punto de emisión y contrato verificados por T5 |

---

## 6. Declaración

Con los criterios de la sección 1 verificados (build 0 errores, suite 76/76 PASS y
reconciliación documental completada, incluida la integración de S18), el **Gate S17
queda formalmente CERRADO (2026-08-10)**. La instrumentación del framework CBP
(`Jwt_Validated`/`Jwt_Expired`/`Event_Published`/`Event_Handled`/`Event_Failed`) queda
certificada y las deudas trasladadas no bloquean ningún sprint posterior.