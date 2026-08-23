# S8 — EsSistema Implementation Summary

## Summary
Eliminado hardcode `idUsuario == 1` como mecanismo de determinación de `EsSistema` en todos los `AuthenticationContext` creation points. Reemplazado por `Usuarios.EsSistema` (columna existente) como fuente formal de verdad. Corregido `SwitchToPlatformAsync` que omitía `EsSistema`.

## Changes

### AuthService.cs — 7 puntos AuthenticationContext corregidos

| Flujo | Línea | Antes | Después | Fuente |
|-------|-------|-------|---------|--------|
| Login (SP) | 191 | `login.IdUsuario!.Value == 1` | `login.EsSistema` | LoginResult del SP |
| MFA | 246 | `usuario.Id == 1` | `usuario.EsSistema` | Usuario entidad (reordenado) |
| Refresh | 291 | `usuario.Id == 1` | `usuario.EsSistema` | Usuario entidad (reordenado) |
| GenerarAuthResponse | 378 | `login.IdUsuario!.Value == 1` | `login.EsSistema` | LoginResult del SP |
| PlatformLogin | 533 | `usuario.Id == 1` | `usuario.EsSistema` | Usuario entidad |
| SwitchTenant | 576 | `usuario.Id == 1` | `usuario.EsSistema` | Usuario entidad (reordenado) |
| **SwitchToPlatform** | 638 | *(omitido — default false)* | **`EsSistema: usuario.EsSistema`** | Usuario entidad |

### ExternalAuthService.cs
- **No modificado** — línea 288 ya usaba `result.EsSistema` correctamente.

### Null safety
- Agregados null checks para `usuario` en flows MFA, Refresh, SwitchTenant (movidos `ObtenerUsuarioBasicoAsync` antes de `AuthenticationContext` creation).

## Verification

### TEST A — Non-Id=1 system user
- **Setup**: `UPDATE Usuarios SET EsSistema = 1 WHERE Id = 19` (usuario `test_noemail_multi_484218_1`)
- **Login**: `POST /api/auth/login` → JWT
- **SwitchToPlatform**: `POST /api/auth/switch-to-platform` → JWT
- **Result**: JWT contiene `"is_system":"true"` ✅
- **Revert**: `UPDATE Usuarios SET EsSistema = 0 WHERE Id = 19` ✅

### TEST C — SwitchToPlatform is_system claim
- **User**: `sistema` (Id=1, EsSistema=1)
- **Flow**: Login → SwitchToPlatform
- **Result**: A1.9 Test #17 (Privilege escalation) PASS ✅

### TEST B — EsSistema=0 → is_system absent
- **User**: `test_noemail_multi_484218_1` (Id=19, EsSistema=0 después de revert)
- **Result**: Login JWT no contiene `is_system` claim ✅

## Regression Results

| Suite | Result | Notes |
|-------|--------|-------|
| Build | 0 errors, 2 pre-existing warnings | NU1603 (EF Core Design version) |
| xUnit | 66/66 ✅ | PassPlat.Aplicacion.Test |
| A1.8 | 24/24 ✅ | faseA18-multitenant-gate.spec.ts |
| A1.9 | 17/17 ✅ | faseA19-switch-to-platform.spec.ts |
| FASE12 | 23/23 ✅ + 2 skip | fase12-federacion-ui.spec.ts |
| FASE15 | 9/9 ✅ + 1 skip | fase15-oauth-certification.spec.ts |
| _diag | 1/1 ✅ | capture Blazor error |
| _dump | 1/1 ✅ | dump dashboard ejecutivo |

## Test Infrastructure Fix

### faseA18-multitenant-gate.spec.ts:103
- **Antes**: `const health = await api.get('http://localhost:5000/api/auth/login', ...)`
- **Después**: `const health = await api.get(`${API}/auth/login`, ...)`
- **Razón**: Hardcoded URL causaba `ECONNREFUSED` cuando API corre en puerto distinto a 5000.
- **No cambia asserts** — solo corrige test infrastructure URL.

## What Was NOT Modified
- `AuthenticationTokenIssuer.cs` — ya emite `is_system` claim desde `context.EsSistema`
- `AuthenticationContext.cs` — ya tenía `EsSistema: bool` property
- `ExternalAuthService.cs` — ya usaba `result.EsSistema`
- `AuthRepository.cs` — ya projectiona `EsSistema`
- `LoginResult.cs` — ya tiene `EsSistema` desde SP
- `Program.cs:75` — SystemOnly policy no necesita cambios
- `UsuariosController.cs` — ya usa `User.HasClaim("is_system", "true")`
- `CustomAuthenticationStateProvider.cs` — ya consume `is_system` claim
- `JwtTokenService.cs` — no necesita cambios (no KeyId)

## Fuente de Verdad
`Usuarios.EsSistema` (bit NOT NULL DEFAULT 0) — columna existente en DB, mapeada en EF, retornada por SPs, projectionada en repos. Única fuente para determinar `is_system` en JWT.