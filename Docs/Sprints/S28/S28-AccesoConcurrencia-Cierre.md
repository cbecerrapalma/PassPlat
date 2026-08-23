# S28 — Cierre DEUDA-001 (Concurrencia Acceso) + DEUDA-003 (Higiene CS8602)

**Estado**: ✅ **CERRADO / GATE PASS** (2026-08-14)
**Alcance**: S28.2 implementación → S28.3 tests (T1–T8c) → S28.4 E2E concurrencia → S28.5 documentación.
**Fuente**: `Docs/Sprints/S27/S27-Dependency-Debt-Discovery.md` (DEUDA-001 P0, DEUDA-003 P1).

---

## 1. Goal

Resolver la carrera read-then-write en `AccesoRepository.AsignarAccesoAsync` (DEUDA-001, P0) y eliminar los 3 warnings CS8602 pre-existentes de ConfProvIden (DEUDA-003, P1), sin romper el baseline certificado (S21/S22, A1 multi-tenant, CBP). El doble `SaveChangesAsync` detectado en el flujo de acceso quedó dentro del scope (confirmado por el usuario).

## 2. Implementación (S28.2)

| Fix | Archivo | Detalle |
|-----|---------|---------|
| 1. Eliminar trigger fantasma | `PassPlat.Datos\Configurations\Core\AccesoConfiguration.cs` | `HasTrigger("TR_Accesos_ValidarTenant")` → `ToTable("Accesos")` con comentario alineando A1 (`007_Triggers.sql:19`, BD viva `sys.triggers` = 0). El trigger fue DROPPED en A1 y reemplazado por la FK compuesta `FK_Accesos_UsuarioTenant`. |
| 2. Catch 2601/2627 en servicio | `PassPlat.Aplicacion\Services\SPro\AccesoService.cs` | `AsignarAccesoAsync` envuelve `_uow.SaveChangesAsync(ct)` en `try/catch (DbUpdateException ex) when (EsViolacionIndiceUnicoAcceso(ex))` → `Result<AccesoDto>.Failure("ACCESO_DUPLICADO", "El usuario ya tiene un acceso activo para esta aplicación y rol")`. Helpers: `EsViolacionIndiceUnicoAcceso` (recorre InnerException; `SqlException` 2601/2627 + `EsIndiceUnicoDeAcceso`) y `EsIndiceUnicoDeAcceso` (`Message.Contains("Accesos")`). Se añadió `using Microsoft.EntityFrameworkCore;`. |
| 3. Doble `SaveChangesAsync` | `PassPlat.WebAPI\Controllers\AccesosController.cs` | Eliminado el `await _uow.SaveChangesAsync(ct)` redundante en `Asignar` (el servicio ya commitea en su L84; el commit de controlador era un no-op). `Revocar` conserva su commit (es el único del flujo). |
| 4. CS8602 ×3 | `ConfProvIdenService.cs` + `CrearConfProvIdenValidator.cs` | `dto.ClientSecret is { } clientSecret` con `Trim()`; guardas null-safe en Callback/RedirectUri (`uri is not null && uri.StartsWith("https://")`). |

**Nota F9**: no se añadió `Microsoft.Data.SqlClient` a `PassPlat.Aplicacion` — el tipo se usa fully-qualified (`Microsoft.Data.SqlClient.SqlException`) vía dependencia transitiva de `PassPlat.Datos`.

## 3. Tests (S28.3) — 10/10 PASS

`PassPlat.Aplicacion.Test\Tests\S28\S28ImplementationTests.cs`:

| Test | Verifica |
|------|----------|
| T1 | `AsignarAcceso_Exito` — mock `SaveChangesAsync` → 1; DTO mapeado (Id=9, IdTenant=2, IdApp=1, IdRol=3, Activo) |
| T2 | `DuplicadoSqlError2601` → Failure `ACCESO_DUPLICADO` (mensaje menciona "ya tiene un acceso activo") |
| T3 | `DuplicadoSqlError2627` → Failure `ACCESO_DUPLICADO` (misma discriminación) |
| T4 | Duplicado de OTRA tabla (mensaje "Usuarios") → `DbUpdateException` se propaga (regresión contra la heurística) |
| T5 | `DbUpdateException` con inner no-Sql (`InvalidOperationException`) → se propaga |
| T6 | `AccesoConfiguration` — introspection EF: sin anotación Trigger + índice único `(IdUsuario, IdApp, IdRol)` |
| T7 | `Actualizar_ClientSecretConEspacios` → cifra con `Trim()` |
| T8 | `Actualizar_ClientSecretNulo` → `Times.Never` (no cifra) |
| T8b | `ActualizarConfProvIdenValidator` — null-safety Callback/RedirectUri + regla https |
| T8c | `CrearConfProvIdenValidator` — reglas Google (callback https, clientId `.apps.googleusercontent.com`, scopes openid/profile/email) |

`SqlException` se construye vía reflexión (sin ctor público): ctor interno 4-param `(string, SqlErrorCollection, Exception, Guid)`.

## 4. E2E Concurrencia (S28.4) — PASS en SQL Server real

API `http://localhost:5259`, token `admin_abarrotes`/`Admin@123` (tenant 2, permiso `ACCESOS_ASIGNAR`).

- **Combo limpio (user 5, app 2, rol 4)**: 2 POST concurrentes a `POST /api/accesos/asignar`:
  - Ambos HTTP 200 (id=7) — repositorio idempotente: el 2º encuentra la fila del 1º vía `FirstOrDefaultAsync` de `AsignarAccesoAsync` (L96-99) y la reutiliza.
  - BD: **exactamente 1 fila** (`SELECT COUNT(*) = 1`). Invariante de índice único `(IdUsuario, IdApp, IdRol)` mantenida.
- **Combo previo (user 4, app 2, rol 6)**: idéntico — 1 fila real, ambos 200.
- **Ventana de carrera simultánea**: la discriminación de excepción 2601/2627 (catch → `ACCESO_DUPLICADO`) queda cubierta determinísticamente por T2/T3; por vía HTTP es extremadamente estrecha porque el check de existencia del repo neutraliza la mayoría de razas reutilizando la fila.

Índices únicos verificados en BD viva `sys.indexes`: `UQ_Accesos_UsrAppRol`, `UX_Accesos_Platform_UsrAppRol` (ambos en `IdUsuario,IdApp,IdRol`) → el mensaje SQL de violación contiene "Accesos", confirmando la heurística `EsIndiceUnicoDeAcceso`.

## 5. Observación de certificación E2E (registrada S29.0.0)

El resultado E2E es válido y demuestra la **invariante final**: 2 requests concurrentes → 1 fila `Acceso`. Pero participan **dos mecanismos distintos** que conviene no confundir:

| Mecanismo | Descripción | Qué certifica |
|-----------|-------------|---------------|
| **1. Idempotencia por lectura previa** | `AccesoRepository.AsignarAccesoAsync` hace `FirstOrDefaultAsync` antes de insertar; si la fila ya existe, **reutiliza la fila existente** (L96-99). | Explica por qué ambos POST terminan HTTP 200 y por qué solo hay 1 fila. |
| **2. Protección ante carrera real** | El índice único `UQ_Accesos_UsrAppRol` / `UX_Accesos_Platform_UsrAppRol` `(IdUsuario, IdApp, IdRol)` es la barrera de integridad; el catch `2601/2627 → ACCESO_DUPLICADO` decide el ganador cuando dos requests alcanzan simultáneamente el INSERT/UPDATE. | La discriminación defensiva está cubierta **determinísticamente por T2/T3** (tests) y la invariancia de datos por E2E. |

**Non-claim explícito**: S28 **NO** certifica "concurrencia 100% reproducida con excepción 2601/2627 en vivo por vía HTTP". La ventana de carrera simultánea es extremadamente estrecha porque Mecanismo 1 neutraliza la mayoría de razas. El reporte no demuestra esa reproducción experimental, y no debe citarse como si lo hiciera. La combinación T2/T3 (2601/2627) + E2E (2 POST → 1 fila) es suficiente para cerrar S28, pero la reproducción en vivo del `DbUpdateException` queda como verificación no cubierta.

### Invariante de commit único

El fix del doble `SaveChangesAsync` dejó el flujo así (S28.2 · Fix 3):

```
Controller
   ↓
Service
   ↓
SaveChanges          ← único responsable del commit (acciones que encapsulan AsignarAccesoAsync)
```

**Regla**: una operación que encapsula `AsignarAccesoAsync` debe tener **un único responsable del commit**. No reintroducir el doble `SaveChangesAsync` (service + controller) — la redundancia produce un commit no-op y diluye el punto de fallo. (Incluida en AGENTS.md Common Pitfalls.)

## 6. Resultados

| Métrica | Valor |
|---------|-------|
| Build | 0 errores (20 warnings NU1903 pre-existentes en CBP.Excel) |
| Tests S28 | 10/10 PASS |
| Regresión total | **153/153 PASS** (97 Aplicacion + 56 Architecture) |
| E2E concurrencia | PASS (1 fila bajo 2 POST concurrentes) |
| `Usuario.IdTenant` como contexto | sin cambios (A1 invariante) |

## 7. Decisiones clave

- **Criterio de duplicado**: índice único `(IdUsuario, IdApp, IdRol)` como árbitro determinista; catch discrimina solo 2601/2627 cuyo mensaje mencione "Accesos" (convención `DispConfiablesController.cs:101-107`) — no traga otros `DbUpdateException`.
- **Fijación del doble SaveChanges**: el servicio es el único commit del flujo `Asignar`; `Revocar` mantiene su commit en controlador (único del flujo). No se toca el contrato A1.
- **Eliminación `HasTrigger`** alineada con evidencia S27: trigger ausente en `sys.triggers`, DROPPED en A1.
- **Capa de catch**: servicio (no controlador) — consistente con los tests T1-T5 y el patrón Result de AppService; evita acoplar excepciones SqlClient al WebAPI.

## 8. Deudas restantes

| Deuda | Estado |
|-------|--------|
| DEUDA-002 (F3 skew EF) · 004 (F8) · 005/006/007 (UoW/Query/Detach) | APLAZADAS (requieren tocar CBP) |
| DEUDA-008 (Reflection background) · 009 (catches silenciosos) | P2 pendientes |
| DEUDA-010 (UnitTest1) · 011 (Sql_SlowQuery) · 012 (templates email) | P3 pendientes |

## 9. Reglas respetadas

- Build 0 errores tras cada cambio.
- 10/10 tests S28 + 153/153 regresión (sin romper baseline S26/S27/S21/S22).
- Regla F9 (sin types EF/SqlClient en Aplicacion) respetada — `Microsoft.Data.SqlClient` fully-qualified.
- Patrón Result propagado: repositorio → servicio → `FromResult`. `ACCESO_DUPLICADO` es un `Result.Failure`, nunca excepción hacia el cliente.
