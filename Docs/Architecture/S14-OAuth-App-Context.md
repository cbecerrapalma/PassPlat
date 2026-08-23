# S14 — F3: OAuth App Context (Fix `IdApp` hardcodeado)

> Sprint S14 · FASE F3 · Único cambio funcional del sprint.
> Cierra la deuda técnica D1 documentada en `S13-F4-JWT-Internal-vs-OAuth.md:59`.

---

## Bug corregido

**`ExternalAuthService.LoginExternoAsync`** construía el `AuthenticationContext` para emitir el
JWT interno post-OAuth con `IdApp = 1` **hardcodeado**, ignorando el `idApp` real recibido como
parámetro (y almacenado en `OAuthSession` durante `GenerateAuthorizationUrlAsync`).

```
Antes (ExternalAuthService.cs:289):
  new AuthenticationContext(result.IdUsuario!.Value, idTenant, 1, ...)

Después:
  new AuthenticationContext(result.IdUsuario!.Value, idTenant, idApp, ...)
```

**Impacto del bug**: el JWT emitido tras login OAuth llevaba claim `IdApp=1` (PASSPLAT) aunque el
usuario iniciara desde otra aplicación. Esto alimentaba a `PermissionClaimBuilder` con los
permisos de la app equivocada (pese a que `IdTenant`/`IdUsuarioTenant` eran correctos).

## Fuente de verdad del IdApp

| Capa | Fuente |
|------|--------|
| UI Blazor (`Login.razor:660-661`) | `Auth.AppId` del contexto JWT → query `idApp={idApp}` en `api/auth/externo/{codigo}/authorize` |
| Controller (`ExternalAuthController.cs:240`) | `[FromQuery] int idApp = 1` (default si se omite) → pasa a servicio |
| Servicio (`GenerateAuthorizationUrlAsync`) | Parámetro `idApp` → guarda en `OAuthSession.IdApp` (cache) |
| Callback (`LoginExternoAsync`) | Lee `session.IdApp` y **ahora** lo propaga al `AuthenticationContext` ✅ |

La UI ya enviaba el `idApp` correcto; el defecto era solo la propagación en el servicio.

## Decisión sobre defaults restantes

Se **mantienen** los defaults `idApp = 1` en:
- `ExternalAuthService.cs:33` (firma interfaz) y `:329` (implementación)
- `ExternalAuthController.cs:240` (`[FromQuery]`)

**Motivo**: son cláusulas de compatibilidad del contrato HTTP/API (query param opcional). No
causan regresión porque el único caller (UI Blazor) **siempre** envía `idApp` explícito. El fix
estructural (contexto JWT con IdApp real) es el que garantiza la coherencia
`JWT.IdApp == App del contexto`.

## Verificación

- `dotnet build PassPlat.slnx` → **0 errores** (solo warnings pre-existentes NU1603).
- Smoke: WebAPI relanzada en `https://localhost:5001` (PID 5408), Web `https://localhost:7275` OK.
- La coherencia `JWT.IdApp == App UI` se certificará en F4 (multi-app) y F14 (E2E).

---

*Siguiente paso*: **F4 — Certificación OAuth multi-App** (`tests/s14-oauth-multi-app.spec.ts`).