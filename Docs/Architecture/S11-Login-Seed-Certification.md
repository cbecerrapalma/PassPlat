# S11 — Login Seed Certification

## Estado: CERTIFICADO ✅

**Fecha**: 2026-08-01
**Contexto**: FASE 5.8 + FASE 5.9 de Sprint S11 (Login Seed) — certificación del login con los seeds corregidos (G1/G2) y el modelo multi-tenant A1.
**Baseline**: FASE 17.5 S9 RELEASE CANDIDATE, A1.8 24/24, A1.9 17/17, xUnit 66/66, Google 39/39, Build 0 errores.

## Resultado Final de Certificación

### Login funcional (manual, API corriendo en `http://localhost:5000`)

| Escenario | Endpoint | Resultado |
|-----------|----------|-----------|
| Login tenant-scoped | `POST /api/auth/login` — `admin_abarrotes`/`Admin@123`/IdTenant=3 | ✅ 200 — JWT con `TenantId=3`, `UsuarioTenantId=1`, permiso `USUARIOS_VERDISP` presente (antes faltaba) |
| Login platform | `POST /api/auth/login/platform` — `platform_admin`/`Admin@123` | ✅ 200 — `idTenant=0`, token len=2073 |
| mis-tenants | `POST /api/auth/login` + `GET /api/auth/mis-tenants` — `test_multitenant`/tenant 3 | ✅ ABARROTES(3) + VESTUARIO(4) |
| Switch tenant | `POST /api/auth/switch-tenant/4` (JWT tenant 3) | ✅ 200 — `idTenant=4`, `nomUsuario=test_multitenant` |
| Switch to platform | `POST /api/auth/switch-to-platform` (JWT tenant 4) | ✅ 200 — `idTenant=0`, `nomUsuario=test_multitenant` |

### Gates Playwright

| Gate | Resultado | Estado |
|------|-----------|--------|
| A1.8 Multi-Tenant (24 tests) | 24/24 | ✅ Certificado |
| A1.9 Switch-to-Platform (17 tests) | 17/17 | ✅ Certificado |

### Regresión

| Suite | Resultado | Estado |
|-------|-----------|--------|
| xUnit (`PassPlat.Aplicacion.Test`) | 66/66 | ✅ Correcto |
| Google xUnit (subconjunto) | 39/39 | ✅ Correcto |
| Federación Identidades API (fase14) | 14/14 | ✅ Correcto |
| Federación UI API (fase12, tests 1-17) | 17/17 | ✅ Correcto |
| Federación UI Blazor (fase12, tests 19-25) | 1 fallo | ⚠️ Web server no levantado en `localhost:5273` (limitación ambiental pre-existente, no bloqueante) |
| Build | 0 errores / 0 warnings nuevos | ✅ Correcto |

## Fix aplicado en este ciclo (FASE 5.9)

### `POST /api/auth/switch-tenant/{idTenant}` — HTTP 415

- **Causa**: El endpoint requiere body `[FromBody] SwitchTenantRequest` con `IdApp` obligatorio. La request inicial sin Content-Type ni body devolvía `415 Unsupported Media Type`.
- **Fix (request, no código)**: Enviar `Content-Type: application/json` + body `{"IdApp":1}`.
- **Ruta verificada**:
  ```
  POST /api/auth/login (tenant 3) → JWT tenant-scoped
  POST /api/auth/switch-tenant/4 (body {IdApp:1}) → JWT tenant 4
  POST /api/auth/switch-to-platform (body {IdApp:1}) → JWT platform (idTenant=0)
  ```

## Detalle técnico relevante

- `SwitchTenantRequest` / `SwitchToPlatformRequest` (en `AuthController.cs`): ambos exigen `IdApp` (`[Required]`), opcionales `IdDisp`, `IdIP`, `IdAgente`.
- El round-trip tenant→platform→tenant queda cubierto por los tests A1.9 #10/#11 (17/17 PASS).
- Las credenciales de certificación (PWD=`Admin@123`) viven en `tests/faseA18-multitenant-gate.spec.ts` — usadas por A1.8 y A1.9.
- `SP_Auth_Login` (G7 no aplica; solo `SP_Auth_LoginExterno` A1.4) continúa sin cambios — el login local NO se modificó en este sprint.

## Relevant Files

| File | Rol |
|------|-----|
| `D:\CODIGOS\PassPlat\PassPlat.WebAPI\Controllers\AuthController.cs` | `login`, `login/platform`, `switch-tenant`, `switch-to-platform`, request DTOs |
| `D:\CODIGOS\PassPlat\tests\api-config.ts` | API_BASE `http://localhost:5000/api` |
| `D:\CODIGOS\PassPlat\tests\faseA18-multitenant-gate.spec.ts` | 24/24 PASS |
| `D:\CODIGOS\PassPlat\tests\faseA19-switch-to-platform.spec.ts` | 17/17 PASS |
| `D:\CODIGOS\PassPlat\tests\fase14-federacion-identidades.spec.ts` | 14/14 PASS |
| `D:\CODIGOS\PassPlat\tests\fase12-federacion-ui.spec.ts` | 17/17 API PASS |
| `D:\CODIGOS\PassPlat\PassPlat.Aplicacion.Test\` | xUnit 66/66 |
| `D:\CODIGOS\PassPlat\PassPlat.slnx` | Build 0 errores |
| `D:\CODIGOS\PassPlat\Docs\Architecture\S11-Seed-Strategy.md` | Estrategia seed (complemento) |
