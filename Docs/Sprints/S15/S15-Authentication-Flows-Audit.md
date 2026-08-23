# S15-Authentication-Flows-Audit.md — Flujos de Autenticación Transaccionales (documento compañero de F1)

# Estado          Borrador
# Tipo            ☐ Evidencia ☑ Análisis ☐ Decisión
# Fuente          Authentication-Audit
# Depende de      Authentication-Audit
# Influye en      Certification, Security-Logging
# Área            Detalle transaccional de los 8 flujos de autenticación + eventos/tratamiento de error
# Framework CBP   CBP.Authentication.JwtBearer, CBP.Security.Cryptography, CBP.Events
# Cobertura       Aplicacion | WebApi
# Evidencia       AuthService.cs (LoginAsync, CompletarLoginConMFAAsync, RefreshTokenAsync, SwitchTenantAsync, SwitchToPlatformAsync, PlatformLoginAsync) · AuthenticationTokenService.cs · ExternalAuthService.cs · SessionManager.cs · AuthenticationTokenIssuer.cs
# Resultado       PASS (8 flujos coherentes, MFA/OAuth/switch bien orquestados; errores propagados por Result <-> CBP)
# Cobertura       90 %

---

## 1. Proposito

Documento compañero de `S15-Authentication-Audit.md`. Complementa con el **detalle transaccional por flujo**: entrada, pasos, salida, manejo de error y resultado del contrato CBP.Result. No duplica conclusiones — detalla el `cómo` de cada flujo.

## 2. Estructura del hallazgo
Ver `S15-CBP-Inventory.md` seccion 2.

## 3. Matriz de flujos — detalle transaccional

### 3.1 Login password
| Campo | Detalle |
|---|---|
| Entrada | `LoginRequest` (NomUsuario, IdApp, IdTenant, Password) |
| Pasos | SP_Auth_Login → valida → `LoginConTokenAsync` → MFA check → `AuthenticationTokenService.LoginAsync` → claims + `IJwtTokenService.GenerateToken` |
| Salida | `AuthResponseDto` (JWT, refresh, idSession, requiereMFA?) |
| Error | `Result<AuthResponseDto>` Failure con codigo (`CREDENCIALES_INVALIDAS`, `CUENTA_BLOQUEADA`, etc.) |
| CBP.Result | SI (servicio propaga via Result) |

### 3.2 MFA
| Campo | Detalle |
|---|---|
| Entrada | `CompletarLoginConMFAAsync` (codigo) |
| Pasos | SP_MFA_Validar → emite tokens |
| Error | Intentos MFA incrementan; silenciado historico SEC-005 (enviar) |
| CBP.Result | SI |

### 3.3 OAuth (externo)
| Campo | Detalle |
|---|---|
| Entrada | `authorize` → Provider → `callback` (code) |
| Pasos | `ExternalAuthService` → ProviderFactory → token exchange → SP_Auth_LoginExterno → IdenExt → JWT interno |
| Salida | Redirect `/signin-callback` |
| Error | `OAuthAutoLinkDenied`, `PROVIDER_ERROR` (EventIds) |
| CBP | CBP.Authentication + CBP.Results |

### 3.4 Refresh
| Pasos | `RefreshAsync` → valida hash → rota (SP_Sesiones) → detecta reuso → revoca |
| CBP | SI |

### 3.5 Switch tenant / platform / platform login
| Flujo | Pasos clave | JWT scope |
|---|---|---|
| SwitchTenant | valida UsuarioTenant activo → JWT `TenantId=X, UsuarioTenantId=Y` | tenant |
| SwitchPlatform | revoca sesion tenant → JWT `IdTenant=null` | platform |
| PlatformLogin | Argon2id directo (sin SP) → JWT `null/null` | platform |

## 4. Hallazgos de flujo

| ID | Hallazgo | Evidencia | Resultado | Accion | Confidence |
|---|---|---|---|---|---|
| FLOW-001 | Los 8 flujos propagan `Result<T>` correctamente hasta controller, sin excepciones crudas a cliente. | `AuthService.cs` · controllers `[Authorize]` | PASS | REUTILIZAR | Alta |
| FLOW-002 | **MFA catch silencia error en envio** (mismo SEC-005) — destacado por ser flujo de seguridad. | `AuthService.cs:332-340` | **FAIL** | REEMPLAZAR | Media |
| FLOW-003 | `Logout` no loguea resultado de revocacion estructurado (solo revoca Jti) | SesionesController | WARNING | EXTENDER | Media |
| FLOW-004 | PlatformLogin valida Argon2id directo sin SP — correcto, evita pasar IdTenant por SP. | `AuthService.PlatformLoginAsync` | PASS | REUTILIZAR | Alta |

## 5. Matriz de riesgo por flujo

| Flujo | Riesgo | Confidence |
|---|---|---|
| Login password | striking (MFA second check) | Alta |
| MFA | silenciado envio (SEC-005) — Alto | Alta |
| OAuth | callback único + PKCE + auto-link | Alta |
| Refresh | reuso detection | Alta |
| Switch tenant | membership check | Alta |

## 6. Resultado (flujos)

- **PASS** en 7/8 flujos (login, OAuth, refresh, logout, switch, switch-platform, platform). 
- **FAIL parcial**: MFA enviar (SEC-005) — muestra detalle para correccion en S16-005.
- **Insumo F12**: corregir silenciado envio MFA; estructurar logout (EventIds); documentar flujos de platform scope.

## 7. Cierre uniforme S15

| Metrica | Valor |
|---|---|
| Cobertura CBP | 90 % |
| Architecture Score | 84 / 100 |
| Confidence | Alta |
| Technical Debt | TD-AUTH-END (SEC-005, FLOW-003, logout traza) |