# CBP.Logging — Validation & Acceptance Checklist

**Tipo**: Criterio oficial de aceptación del contrato de logging
**Versión del contrato**: 1.0 (CONGELADO)
**Especificación de referencia**: [CBP.Logging.Specification.md](CBP.Logging.Specification.md)
**Catálogo de referencia**: [Logging.EventCatalog.md](Logging/Logging.EventCatalog.md)
**Campaña**: S16 / S16 Release Candidate (RC1)

Este documento es el **criterio de aceptación permanente** para cualquier cambio
futuro en CBP.Logging o en los emisores que lo consumen. Su propósito es que ninguna
modificación (nueva instrumentación, refactor, cambio de sinks, actualización de
Serilog, etc.) rompa el contrato sin ser detectada.

Regla de gobernanza: antes de fusionar/entregar cualquier cambio que toque logging,
la checklist completa de este documento DEBE pasar.

---

## 1. Checklist de validación (criterio de aceptación)

### 1.1 Compilación e integridad

| # | Check | Evidencia |
|---|-------|-----------|
| 1 | `dotnet clean` + `dotnet build` sin errores | Build log |
| 2 | Sin warnings NUEVOS (NU1603 preexistentes aceptados) | Build log |

### 1.2 Suites de pruebas

| # | Check | Evidencia |
|---|-------|-----------|
| 3 | Unit tests PASS (xUnit, suite completa) | Test log |
| 4 | Contract tests PASS (`CacheLogContractTests`, 4/4) | Test log |
| 5 | Integration tests PASS (si aplican) | Test log |
| 6 | Playwright E2E PASS (Gate C) | Playwright report |

### 1.3 Contrato estructurado por evento

Para cada evento emitido, el evento estructurado DEBE contener:

| # | Propiedad estructurada | Constante CBP.Core | Check |
|---|------------------------|--------------------|-------|
| 7 | `eventName` emitido | `LoggingPropertyNames.EventName` | ☐ |
| 8 | `scope` emitido | `LoggingPropertyNames.Scope` | ☐ |
| 9 | `category` correcta | `LoggingPropertyNames.Category` | ☐ |
| 10 | `operation` correcta (desde `LoggingOperations`) | `LoggingPropertyNames.Operation` | ☐ |
| 11 | `method` = `nameof(...)` (diagnóstico) | `LoggingPropertyNames.Method` | ☐ |
| 12 | `source` correcta | `LoggingPropertyNames.Source` | ☐ |

### 1.4 Contexto de correlación

| # | Propiedad | Check |
|---|-----------|-------|
| 13 | `correlationId` propagado y consistente en todo el flujo | ☐ |
| 14 | `userId` propagado (cuando hay usuario autenticado) | ☐ |
| 15 | `tenantId` presente cuando el flujo es multi-tenant | ☐ |
| 16 | Sin regresión PascalCase: NO `CorrelationId`/`UserId`/`TenantId` como claves | ☐ |

### 1.5 Caché (cuando el evento es de caché)

| # | Propiedad | Check |
|---|-----------|-------|
| 17 | `cacheResult` correcto (`miss`/`hit`/`set`/`invalidation`) | ☐ |
| 18 | `key` de la caché presente | ☐ |
| 19 | `elapsedMs` presente | ☐ |
| 20 | EventName coherente con `cacheResult` (p.ej. `Cache_Hit`↔`hit`) | ☐ |

### 1.6 Vocabulario controlado

| # | Check |
|---|-------|
| 21 | `eventName` proviene de `LoggingEvents` (sin literales libres) | ☐ |
| 22 | `scope` proviene de `LoggingScopes` | ☐ |
| 23 | `operation` proviene de `LoggingOperations` | ☐ |
| 24 | `category` proviene de `LoggingCategories` | ☐ |
| 25 | `source` proviene de `LoggingSources` | ☐ |

---

## 2. Evidencia E2E de observabilidad (Gate C)

Playwright valida simultáneamente comportamiento funcional y contrato de
observabilidad. Para cada paso del flujo real se comprueban: HTTP Status, estado
visual, cambios persistidos y el evento de logging esperado.

| Flujo | EventName esperado | Scope esperado | HTTP | Visual | Persistido |
|-------|--------------------|----------------|------|--------|------------|
| Login | `Login_Succeeded` | `authentication` | 200 | login ok | sesión/JWT |
| MFA | `Mfa_Succeeded` | `authentication` | 200 | MFA ok | MFA validado |
| JWT emitido | `Jwt_Generated` / `Jwt_Validated` | `authentication` | — | — | token |
| Caché MISS (1ª llamada ConfigTenant/PoliticaPwd/Apps) | `Cache_Miss` (`cacheResult=miss`) | `cache` | 200 | datos | — |
| Caché HIT (repetición) | `Cache_Hit` (`cacheResult=hit`) | `cache` | 200 | mismos datos | — |
| Acción que invalida caché | `Cache_Invalidation` | `cache` | 2xx | cambio aplicado | datos modificados |
| Nueva consulta | `Cache_Miss` (re-MISS) | `cache` | 200 | datos actualizados | — |
| Disparo de evento (si aplica) | `Event_Published` | `domainEvents` | — | — | evento persistido |
| Email encolado | `Email_Queued` | `email` | — | — | EmailLog/colas |
| Logout | `Logout` | `authentication` | 200 | sesión cerrada | sesión revocada |

Regla: `correlationId` debe ser **consistente** desde Login hasta Logout dentro de
un mismo flujo, y el `eventName`/`scope` de cada paso debe coincidir con el catálogo
(`Logging.EventCatalog.md`).

---

## 2.1 Registro de CorrelationId del flujo (Gate C)

Cada escenario del Gate C DEBE registrar explícitamente el CorrelationId de su flujo
en el reporte de evidencia, de forma que todos los eventos del escenario contengan
**exactamente ese mismo valor**:

```
CorrelationId del flujo: <guid>

Login_Succeeded    → correlationId = <guid>   ✅
Mfa_Succeeded      → correlationId = <guid>   ✅
Cache_Hit          → correlationId = <guid>   ✅
Email_Queued       → correlationId = <guid>   ✅
Logout             → correlationId = <guid>   ✅
```

Si cualquier evento del escenario difiere del CorrelationId registrado, el escenario
**falla** (evidencia de propagación rota). Esto centraliza el diagnóstico: toda la
evidencia de un escenario gira alrededor de un único CorrelationId.

---

## 3. Ejecución

### 3.1 En cada merge/PR que toque CBP.Logging o emisores

```bash
cd D:\CODIGOS\PassPlat
dotnet build PassPlat.slnx          # 0 errores, sin warnings nuevos
dotnet test PassPlat.slnx           # 70/70 (incluye contract tests)
cd tests && npx playwright test --reporter=list   # Gate C (flujo completo)
```

### 3.2 Registro de validación

Cada ejecución de esta checklist debe registrarse en el Registry del sprint
(`Docs/Sprints/S16/S16-Sprint-Registry.md`) con fecha, commits y resultado
por apartado (1.1–1.6).

---

## 4. Cambios

| Cambio | Descripción | Versión |
|--------|-------------|---------|
| Inicial | Criterio oficial de aceptación RC1 (S16) | 1.0 |
