# S13 — F4: Matriz JWT Interno vs OAuth (Google)

**Fecha**: 2026-08-03
**Sprint**: S13 — Post-Login Authorization, CBP Compliance & App/Tenant Configuration
**Método**: Análisis de código (read-only). No se modificó ningún archivo de autenticación.

---

## 1. Objetivo

Verificar que los tokens JWT emitidos en el login **interno** (`SP_Auth_Login` →
`AuthService.LoginConTokenAsync`) y en el login **externo OAuth** (`SP_Auth_LoginExterno` →
`ExternalAuthService.LoginExternoAsync`) producen el mismo conjunto de claims para el mismo
usuario/tenant/app, de modo que la autorización de dashboards (F2/F3) se comporte idéntico
tras login por Google.

## 2. Estructura de emisión (ambos flujos)

```
LoginConTokenAsync ──► AuthenticationTokenService.LoginAsync ─┐
                                                               ├─► EmitirTokensYCrearSesionAsync
LoginExternoAsync ──► AuthenticationTokenService.OAuthAsync ──┘        │
                                                                       ├─► PermissionClaimBuilder.BuildPermissionClaimsAsync
                                                                       ├─► AuthenticationTokenIssuer.Generate
                                                                       └─► SessionManager.CreateSessionAsync
```

Ambos métodos públicos (`LoginAsync`, `OAuthAsync`) del `AuthenticationTokenService`
delegan en el **mismo** método privado `EmitirTokensYCrearSesionAsync`
(`AuthenticationTokenService.cs:76`). No existe una ruta de emisión diferenciada por origen.

## 3. Claims generadas por `AuthenticationTokenIssuer.Generate` (`AuthenticationTokenIssuer.cs:43`)

| Claim | Fuente | Login Interno | Login OAuth |
|-------|--------|---------------|-------------|
| `sub` (NameIdentifier) | `context.IdUsuario` | `login.IdUsuario` | `result.IdUsuario` (SP) |
| `IdApp` | `context.IdApp` | `idApp` recibido del controller | **`1` hardcodeado** ⚠️ |
| `jti` | `Guid.NewGuid()` | ✓ | ✓ |
| `TenantId` | `context.IdTenant` (si tiene valor) | `login.IdTenant` | `idTenant` recibido |
| `UsuarioTenantId` | `context.IdUsuarioTenant` (si tiene valor) | resuelto vía `ResolverIdUsuarioTenantAsync` | idem |
| `is_system` | `context.EsSistema` (si true) | `login.EsSistema` | `result.EsSistema` |
| `permiso` (×N) | `PermissionClaimBuilder` | dispatch por `IdTenant`/`IdUsuarioTenant` | idem |

## 4. `PermissionClaimBuilder` — independiente del Origen (`PermissionClaimBuilder.cs:19`)

El builder no evalúa `context.Origen`. El dispatch se basa exclusivamente en:

1. `IdTenant == null` → `ObtenerCodigosPermisosPlatformAsync(IdUsuario, IdApp)`
2. `IdTenant != null && IdUsuarioTenant.HasValue` → `ObtenerCodigosPermisosPorUsuarioTenantAsync(IdUsuarioTenant, IdApp)`
3. resto → `ObtenerCodigosPermisosPorUsuarioAsync(IdUsuario, IdTenant, IdApp)`

→ Para el mismo `IdUsuario`/`IdTenant`/`IdApp`/`IdUsuarioTenant`, las claims `permiso`
**son idénticas** en ambos orígenes.

## 5. Divergencias detectadas

| # | Divergencia | Severidad | Impacto | Decisión |
|---|-------------|-----------|---------|----------|
| D1 | `ExternalAuthService.cs:289` pasa `IdApp = 1` hardcodeado al `AuthenticationContext`, ignorando el `idApp` de la sesión OAuth (aunque `GenerateAuthorizationUrlAsync` recibe `idApp = 1` como default). | Baja (hoy) | Si la app de certificación es PASSPLAT (Id=1), no hay impacto. Otras apps con OAuth podrían recibir claims de permisos del App equivocado. | Documentar. No se modifica en S13 (fuera de alcance: no tocar `AuthenticationTokenIssuer`/flujo OAuth sin evidencia). |
| D2 | Contexto OAuth no propaga `IdDispositivo`/`IdIp` al token (`null`), mientras el login interno sí (`idDisp`/`idIP`). | Ninguna (no se emiten claims de disp/IP) | No afecta autorización por permiso. | Documentar. |
| D3 | `Origen` difiere (`Login` vs `OAuth`) — solo afecta telemetría/auditoría, **no** las claims. | Ninguna | — | Correcto por diseño. |

## 6. Conclusión F4

- **No existe capa de divergencia en la emisión de claims de permisos**: `LoginAsync` y
  `OAuthAsync` convergen en el mismo `EmitirTokensYCrearSesionAsync` +
  `PermissionClaimBuilder` (independiente de `Origen`).
- La autorización de los dashboards (F2/F3, gates por `permiso`) se comportará de forma
  **idéntica** tras login por Google para el mismo usuario/tenant/app.
- **Único hallazgo accionable** (fuera de alcance S13): `IdApp` hardcodeado a `1` en el
  contexto OAuth (`ExternalAuthService.cs:289`). Registrado como deuda técnica para sprint
  dedicado; no bloquea la certificación (App PASSPLAT = Id 1).

## 7. Evidencia de verificación previa (F1)

JWT interno decodificado (POST `https://localhost:5001/api/auth/login`):
`sub=8`, `IdApp=1`, `TenantId=3`, `UsuarioTenantId=4`, 7 claims `permiso`, `iss=aud=PassPlat`.

## 8. Estado

**F4 COMPLETADO** (análisis read-only). Sin cambios de código.
