# S15-Authentication-Audit.md — Autenticacion / JWT / Sesion (F1)

# Estado          Borrador
# Tipo            ☑ Evidencia ☐ Análisis ☐ Decisión
# Fuente          CBP-Inventory
# Depende de      Inventory
# Influye en      Authentication-Flows, Certification, Refactoring
# Area            Autenticacion, autorizacion, sesion (F1)
# Framework CBP   CBP.Authentication.JwtBearer (JwtTokenService, JwtAuthenticationOperator, JwtOptions, IJwtTokenService), CBP.Authentication.Abstractions (AuthenticationMiddleware)
# Cobertura       Aplicacion | WebApi
# Evidencia       AuthenticationTokenService.cs · AuthenticationTokenIssuer.cs · SessionManager.cs · PermissionClaimBuilder.cs · AuthService.cs (8 flujos) · JwtTokenService.cs · JwtAuthenticationOperator.cs · Program.cs (UseCbpAuthentication)
# Resultado       PASS (reutilizacion real de CBP.Authentication con capa propia de claims/sesion)
# Cobertura       90 % (ver F11)
# Riesgo          Bajo
# Prioridad       Alta

---

## 1. Proposito

Auditar el flujo de autenticacion completo de PassPlat: login (password), login OAuth, refresh, logout, MFA, switch-tenant, platform login, y emision/validacion JWT. Determinar que componente de CBP se reutiliza realmente y cual es logica propia.

## 2. Regla general de auditoria (12 preguntas)
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Arquitectura de autenticacion (capa propia sobre CBP)

### 3.1 Componentes CBP reutilizados (PASS)

| Componente CBP | Reuso en PassPlat | Evidencia |
|---|---|---|
| `CBP.Authentication.JwtBearer.IJwtTokenService` | Inyectado en `AuthenticationTokenIssuer._jwtService` (Generar JWT y RefreshToken) | `AuthenticationTokenIssuer.cs:27,56` |
| `CBP.Authentication.JwtBearer.JwtOptions` | Inyectado (clock skew, key, issuer, audience) | `AuthenticationTokenIssuer.cs:28,59` |
| `CBP.Authentication.JwtBearer.JwtTokenService` | Genera JWT (`GenerateToken(claims)`), RefreshToken, `ValidateToken()` | `JwtTokenService.cs` (CBP) |
| `CBP.Authentication.JwtBearer.JwtAuthenticationOperator` | `AuthenticateAsync`/`ChallengeAsync`/`ForbidAsync` — usado por middleware CBP | `JwtAuthenticationOperator.cs` (CBP) |
| `CBP.Authentication.Abstractions.AuthenticationMiddleware` | `app.UseCbpAuthentication()` en pipeline | `Program.cs:240` |

### 3.2 Componentes propios (capa de negocio PassPlat)

| Componente propio | Funcion | Evidencia |
|---|---|---|
| `AuthenticationContext` | Contexto de autenticacion (IdUsuario, IdTenant, IdUsuarioTenant, IdApp, IdSesion, claims) | `Services/Authentication/AuthenticationContext.cs` |
| `AuthenticationTokenService` | Orquestador: `LoginAsync`, `OAuthAsync`, `RefreshAsync`, `EmitirTokensYCrearSesionAsync` | `Services/Authentication/AuthenticationTokenService.cs` |
| `AuthenticationTokenIssuer` | Construye claims de identidad (BuildIdentityClaims) + HashSHA256 refresh + delega `Generate` a CBP | `Services/Authentication/AuthenticationTokenIssuer.cs:55-60` |
| `SessionManager` | Persistencia de sesion (SP_Sesiones_Crear/Rotar/Revocar), deteccion de reuso de refresh | `Services/Authentication/SessionManager.cs` |
| `PermissionClaimBuilder` | Construye claims de permisos 3-branch (platform/tenant-with/without-membership) | `Services/Authentication/Claims/PermissionClaimBuilder.cs` |
| `AuthService` | Flujos de login: SP_Auth_Login, MFA, platform, switch, OAuth | `Services/SPro/AuthService.cs` |

## 4. Matriz de flujos de autenticacion

| Flujo | Entrada | Pipeline | CBP usado | Propio | Resultado |
|---|---|---|---|---|---|
| Login password | `AuthService.LoginAsync` → SP_Auth_Login | SP → `LoginConTokenAsync` → `AuthenticationTokenService.LoginAsync` → claims + emit | JwtTokenService.GenerateToken | SP, MFA check, PermissionClaimBuilder | **PASS** |
| Login MFA | `AuthService.CompletarLoginConMFAAsync` → SP_MFA_Validar | verifica codigo → emite tokens | GenerateToken/GenerateRefreshToken | MFA flow | PASS |
| Login OAuth | `ExternalAuthController` → `ExternalAuthService` → `AuthenticationTokenService.OAuthAsync` | SP_Auth_LoginExterno → IdenExt → JWT interno | GenerateToken | Provider factory, auto-link | PASS |
| Refresh | `AuthService.RefreshTokenAsync` → `AuthenticationTokenService.RefreshAsync` | rota refresh hash, detecta reuso (revoca) | GenerateToken/GenerateRefreshToken | SessionManager.RotateRefreshTokenAsync | PASS |
| Logout | `SesionesController` → `RevocarSesionAsync` | SP_Sesiones_RevocarTodas / revoca Jti | — | SessionManager.RevokeSessionAsync | PASS |
| Switch tenant | `AuthService.SwitchTenantAsync` | valida UsuarioTenant activo → JWT tenant-scope | GenerateToken | Context-switch, claims | PASS |
| Switch platform | `AuthService.SwitchToPlatformAsync` | revoca sesion tenant → JWT platform-scope (IdTenant null) | GenerateToken | claims platform | PASS |
| Platform login | `AuthService.PlatformLoginAsync` | Argon2id directo → JWT platform-scope | GenerateToken | Argon2id via CBP.Security | PASS |

## 5. Hallazgos

| ID | Hallazgo | Evidencia | Clasificacion |
|---|---|---|---|
| **AUTH-001** | `IJwtTokenService` CBP se usa de verdad (no duplicado). No existe otro generador de JWT. | `AuthenticationTokenIssuer.cs:56` (`_jwtService.GenerateToken`) | PASS |
| **AUTH-002** | Validacion de token usa `JwtTokenService.ValidateToken` + `JwtAuthenticationOperator` (CBP) via middleware CBP. No se reinventa. | `Program.cs:240` UseCbpAuthentication | PASS |
| **AUTH-003** | Capa de claims y sesion es propia (correcta separacion: CBP = transport, PassPlat = dominio). | `PermissionClaimBuilder`, `SessionManager` | PASS (JUSTIFICAR mantencion) |
| **AUTH-004** | Refresh token rotation SI esta (SP_Sesiones actualiza hash). Reuso detectado y revocada. | `SessionManager.cs:47` | PASS |
| **AUTH-005** | Hash de refresh token en BD (hash SHA-256, no token plano) — correcto. | `AuthenticationTokenIssuer.HashSHA256` | PASS |
| **AUTH-006** | `AuthenticationTokenService.LoginAsync/OAuthAsync/RefreshAsync` orquestan 3 flujos con logica propia (casos limites MFA, platform). Coherente. | `AuthenticationTokenService.cs` | PASS |
| **AUTH-007** | `JwtOptions` CBP: key desde User Secrets; appsettings tiene `Jwt.SecretKey` vacio (dev). No se lee de config sensible. | `appsettings.json` (Jwt.SecretKey = "") | PASS (config segura) |
| **AUTH-008** | BUG-017.1.3 cerrado: `UseHttpsRedirection` envuelto en `!IsDevelopment` — el 401 era redirect 307 no bug de JWT. | `Program.cs:233` | PASS (documentado) |
| **AUTH-009** | `AuthService` sin `ILogger` estructurado para flujos de login no-observados? — en realidad SI tiene `_logger`. Verificacion: AuthService.cs tiene ILogger. | grep AuthService | PASS |

## 6. Metricas de cobertura

| Metrica | Valor |
|---|---|
| Flujos de autenticacion | 8 (login, MFA, OAuth, refresh, logout, switch, switch-platform, platform) |
| Flujos que usan CBP.Authentication | 7/8 (logout es revocacion, no JWT) |
| Generadores JWT propios | 0 (todos via IJwtTokenService) |
| Validadores JWT propios | 0 (todos via CBP JwtAuthenticationOperator) |
| Claims de permisos | Propios (3-branch) — JUSTIFICADO (dominio tenant) |

## 7. Resultado F1
- **PASS**: autenticacion reutiliza CBP.Authentication correctamente (transport JWT), con capa propia solo en claims de permisos + sesion + MFA (dominio PassPlat).
- **Integracion CBP**: alta (JwtTokenService, JwtAuthenticationOperator, AuthenticationMiddleware).
- Duplicacion: **0** (no hay JWT propio).
- Insumo F12 → acciones y trazabilidad migradas a `S15-CBP-Refactoring-Plan.md` (Nivel 3). Este doc conserva SOLO evidencia N1.

### 7.1 Clasificacion dual y severidad/prioridad de los hallazgos principales

| ID | Resultado | Accion | Severidad | Prioridad | Confidence |
|---|---|---|---|---|---|
| AUTH-001 | PASS | REUTILIZAR | — | — | Alta |
| AUTH-002 | PASS | REUTILIZAR | — | — | Alta |
| AUTH-003 | PASS | JUSTIFICAR (capa dominio propia) | — | — | Alta |
| AUTH-004 | PASS | REUTILIZAR | — | — | Alta |
| AUTH-005 | PASS | REUTILIZAR | — | — | Alta |
| AUTH-006 | PASS | REUTILIZAR | — | — | Alta |
| AUTH-007 | PASS | REUTILIZAR (config segura) | — | — | Alta |
| AUTH-008 | PASS | REUTILIZAR (documentado) | — | — | Alta |
| AUTH-009 | PASS | REUTILIZAR | — | — | Alta |

### 7.2 Cierre uniforme S15 — Metricas de madurez

| Metrica | Valor |
|---|---|
| Cobertura CBP | 90 % |
| Architecture Score | 88 / 100 |
| Confidence | Alta |
| Technical Debt generado | TD-AUTH-001..009 (ninguno critico; debito deuda menor de juicio) |

**Ver tambien**: `S15-Authentication-Flows-Audit.md` (casos por flujo) para el detalle transaccional de cada flujo.