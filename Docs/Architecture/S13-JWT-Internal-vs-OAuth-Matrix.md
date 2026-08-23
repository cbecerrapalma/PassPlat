# S13 — F4: Matriz JWT Login Interno vs OAuth Externo

- **Sprint**: S13 — Post-Login Authorization, CBP Compliance & App/Tenant Configuration
- **Fase**: F4 — Matriz JWT interno vs Google (identificación de capa de divergencia)
- **Fecha**: 2026-08-03
- **Método**: Análisis estático de código (sin cambios de código)

## Conclusión principal

**Ambos flujos emiten el JWT por la MISMA ruta de código**, por lo que las claims estructurales
son idénticas. No existe un emisor separado para OAuth. La divergencia funcional está
**solo en la construcción del `AuthenticationContext`** (capa Servicio) y afecta el **contenido
de valores** (`IdApp`, `IdDispositivo`, `IdIp`), no la estructura de claims.

## Ruta de emisión compartida

```
Login interno:   AuthService.LoginConTokenAsync → _tokenService.LoginAsync
OAuth externo:   ExternalAuthService.LoginExternoAsync → _tokenService.OAuthAsync
                              │
                              ▼
                  AuthenticationTokenService (LoginAsync/OAuthAsync)
                              │  ambos delegan en
                              ▼
                  EmitirTokensYCrearSesionAsync (idéntico para ambos)
                              │ 1. _claimBuilder.BuildPermissionClaimsAsync
                              │ 2. _tokenIssuer.Generate(context, claims)
                              │ 3. _sessionManager.CreateSessionAsync
                              ▼
                  AuthenticationTokenIssuer.Generate
                              │ _jwtService.GenerateToken(claims)
                              ▼
                  JWT con BuildIdentityClaims(context, jti) + permiso claims
```

**Archivos**: `AuthenticationTokenService.cs:27-43`, `AuthenticationTokenIssuer.cs:26-62`.

## Claims emitidas (idénticas en ambos flujos)

| Claim | Origen | Valor |
|-------|--------|-------|
| `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` (`sub`) | `BuildIdentityClaims` | `context.IdUsuario` |
| `IdApp` | `BuildIdentityClaims` | `context.IdApp` |
| `jti` | `BuildIdentityClaims` | GUID random |
| `TenantId` | `BuildIdentityClaims` | solo si `IdTenant.HasValue` |
| `UsuarioTenantId` | `BuildIdentityClaims` | solo si `IdUsuarioTenant.HasValue` |
| `is_system` | `BuildIdentityClaims` | solo si `EsSistema` |
| `permiso` (N claims) | `PermissionClaimBuilder` | códigos de permisos efectivos |

El `PermissionClaimBuilder` (`PermissionClaimBuilder.cs:19-56`) **no depende del `Origen`**:
ramifica por `IdTenant`/`IdUsuarioTenant` únicamente (3-branch dispatch A1.5.2).
Por tanto, para el mismo usuario/tenant/app, las claims de permiso son idénticas en ambos flujos.

## Comparación del AuthenticationContext (única divergencia)

| Campo | Interno (`AuthService.GenerarAuthResponseAsync` L376-383) | Externo (`ExternalAuthService.LoginExternoAsync` L288-293) | ¿Coinciden? |
|-------|----------------------------------------------------------|------------------------------------------------------------|-------------|
| `IdUsuario` | `login.IdUsuario.Value` (SP_Auth_Login) | `result.IdUsuario.Value` (SP_Auth_LoginExterno) | Sí (valor) |
| `IdTenant` | `login.IdTenant.Value` | `idTenant` (param) | Sí (valor) |
| **`IdApp`** | `idApp` (param de la request) | **hardcode `1`** | **NO — externo fija 1** |
| `IdDispositivo` | `(short?)idDisp` (param) | `null` | **NO — externo nulo** |
| `IdIp` | `idIP` (param) | `null` | **NO — externo nulo** |
| `Origen` | `AuthenticationOrigin.Login` | `AuthenticationOrigin.OAuth` | Diferente (by design) |
| `EsSistema` | `login.EsSistema` | `result.EsSistema` | Sí (semántica) |
| `IdUsuarioTenant` | `ResolverIdUsuarioTenantAsync` | `ResolverIdUsuarioTenantAsync` | Sí (mismo repo) |

## Hallazgos

1. **`IdApp` hardcodeado a 1 en el flujo externo** (`ExternalAuthService.cs:289`).
   - La URL de autorización usa `GenerateAuthorizationUrlAsync(providerCode, idTenant, idApp = 1, ...)`
   (default 1 en la firma), y el `OAuthSession.IdApp` guarda ese valor (L375).
   - El login externo recibe `idApp` pero **lo ignora** para construir el `AuthenticationContext`
   (L289 pasa literal `1`).
   - **Impacto**: si la App de federación fuera distinta de 1, el JWT OAuth llevaría `IdApp=1`
   y los permisos se resolverían contra la App 1 (PASSPLAT). Con el catalogo actual (única App PASSPLAT=1)
   **no hay impacto en runtime**. Clasificación: `CONFIG` latente (no BUG activo).

2. **`IdDispositivo`/`IdIp` nulos en el flujo externo**.
   - El login externo no registra dispositivo/IP en el contexto de token (aunque el SP
   `SP_Auth_LoginExterno` sí recibe `idDisp`/`idAgente`/`ip` para auditoría).
   - **Impacto**: sin claims de contexto de dispositivo; solo afecta telemetría/sesión, no permisos.

3. **MFA en login externo**: cuando `MFARequerido` (L230-242), el DTO devuelto NO contiene
   token (solo `IdMFAPrincipal`/`IdUsuario`/`IdTenant`) — consistente con el flujo interno.

## Verificación en runtime (evidencia)

- JWT interno certificado en F1 sobre `https://localhost:5001`: `sub=8`, `IdApp=1`, `TenantId=3`,
  `UsuarioTenantId=4`, 7 permisos, `iss=aud=PassPlat`, 60 min.
- JWT externo: estructura idéntica esperada; valores dependerán del usuario vinculado
  (a certificar en F12/F13 con login Google real).

## Decisión

- **No se modifica código en F4** (fase de análisis; el plan exige `STOP`/evidencia para tocar
  el núcleo auth).
- Se registra como **deuda técnica conocida** para revisión futura si se incorporan
  Apps adicionales a la federación (PENDIENTE — no bloqueante).
