# S11 — Login Access Model (Contrato formal)

- **Fecha**: 2026-08-01
- **Sprint**: S11 — SEED/DDL Reproducible + Certificación End-to-End del Login
- **Estado**: Contrato trazado desde código (FASE 2 completa). Pendiente: aprobación Opción A (FASE 5) e implementación.
- **Método**: inspección + rastreo de `AuthController`, `ExternalAuthController`, `AuthService`, `AuthenticationTokenService`, `PermissionClaimBuilder`, `AuthenticationTokenIssuer`, `SessionManager`, `AuthRepository`, `SP_Auth_Login`, `SP_Permisos_Usuario_Efectivos`, `SP_Auth_LoginExterno`.

---

## 1. Contrato de Login Local (credenciales + password)

### 1.1 Firmas y validación de entrada

| Capa | Sitio | Requisito | Evidencia |
|------|-------|-----------|-----------|
| DTO | `LoginRequest` | `IdApp [Required]`, `IdTenant [Required]` | `AuthController.cs:400,407` |
| SP | `SP_Auth_Login` | `@IdTenant int` (sin default), `@IdApp int` (sin default), `@HashPwdCalculado nvarchar(512)` | `PASSWORDS SP.sql:1496-1498` |
| Repo | `AuthRepository.LoginAsync` | Pasa IdTenant, IdApp, hash, NomUsuario/Email | `AuthRepository.cs:22-37` |

**Conclusión: el login local exige SIEMPRE `IdTenant` + `IdApp`. No hay login local sin ambos.**

### 1.2 Resolución de usuario dentro del SP

```sql
SELECT TOP 1 ... 
FROM dbo.Usuarios u
JOIN dbo.Tenants t        ON u.IdTenant = t.Id
JOIN dbo.ConfigTenants ct ON u.IdTenant = ct.IdTenant
WHERE u.IdTenant = @IdTenant
  AND u.Eliminado = 0
  AND (u.NomUsuario = @NomUsuario OR u.Email = @Email)
  AND t.Activo = 1;
```
`PASSWORDS SP.sql:1548-1561`

> **HALLAZGO CLAVE**: el filtro de tenant en login local usa la columna **legacy `Usuarios.IdTenant`**, NO la membresía `UsuarioTenant`. La membresía se resuelve DESPUÉS del login (`ResolverIdUsuarioTenantAsync`, `AuthService.cs:680`) y solo alimenta los claims del JWT y la rama de permisos. Esto es coherente con A1 (IdTenant legacy como FK de datos), pero implica: un usuario con múltiples membresías sigue teniendo `Usuarios.IdTenant` como "tenant primario" para login.

### 1.3 Secuencia de verificación del SP

1. Usuario no encontrado → `CredencialesInvalidas`.
2. `IdEstado <> 1` → `CuentaInactiva`.
3. Bloqueo activo (`Bloqueos.Activo=1` y `EsTemporal=0` o `FecFin > now`) → `CuentaBloqueada`.
4. **`EsSistema=0`** → requiere `Accesos` activo `(IdUsuario, IdApp)` → `SinAccesoApp`.
5. Política de password: cascada `RolesPoliticasPwd` → `PoliticasPwd(tenant,app)` → `(tenant,NULL)` → `(NULL,NULL)`; `MaxIntentos` default 5, `DurBloqueo` default 30.
6. `HistorialPwd.EsActual=1` → `HashPwdDB`. Si NULL → `CuentaInactiva` (sin contraseña).
7. Comparación **exacta** `@HashPwdDB COLLATE Latin1_General_BIN2 = @HashPwdCalculado COLLATE Latin1_General_BIN2`.
8. OK → resetea `IntentosFallidos`, calcula `ReqCambioPwd`, `PwdExpirada`, `RequiereReHash`, y `IdMFAPrincipal` (solo `EsPrincipal=1` y `IdEstado=Codigo='ACTIVO'`).

### 1.4 Pre-verificación Argon2id en servicio

`AuthService.LoginConTokenAsync` (`AuthService.cs:108`):
- Busca usuario por `NomUsuario` OR `Email` (`ObtenerUsuarioPorNomAsync` — **sin filtro tenant**, primera coincidencia global).
- Obtiene hash actual y verifica Argon2id. Si válido, envía `storedHash` al SP (para la comparación BIN2); si no, envía el hash calculado (el SP fallará).
- La autoridad final de login es el SP (filtra por IdTenant correctamente).

> ⚠️ **NOTA**: la pre-verificación usa la primera coincidencia global de NomUsuario/Email. No es riesgo porque el SP re-valida contra `@IdTenant`. Pero si existiera el mismo `NomUsuario` en 2 tenants, la pre-verificación de hash usaría el primero — solo afecta telemetría/auditoría, no la decisión final.

---

## 2. Contrato de Scope (platform vs tenant)

| Scope | `IdTenant` contexto | `IdUsuarioTenant` | Claim `TenantId` | Claim `UsuarioTenantId` | Origen |
|-------|---------------------|-------------------|------------------|------------------------|--------|
| **Platform** | `null` | `null` | ❌ ausente | ❌ ausente | `Login` (platform) / `SwitchToPlatform` |
| **Tenant con membresía** | X | Y | ✅ | ✅ | `Login` / `SwitchTenant` / `Refresh` / `MFA` |
| **Tenant sin membresía** | X | `null` | ✅ | ❌ | legacy (rama 3 de permisos) |

`AuthenticationContext.cs:3-11` + `AuthenticationTokenIssuer.cs:52-59`.

### 2.1 Login local → contexto

`GenerarAuthResponseAsync` (`AuthService.cs:376`):
- `IdUsuarioTenant` = `ResolverIdUsuarioTenantAsync(idUsuario, idTenant)` — **puede ser null si no hay membresía**.
- `EsSistema` = valor del SP (`login.EsSistema`) o del DTO básico.

### 2.2 Platform login (sin tenant)

`PlatformLoginAsync` (`AuthService.cs:510`):
- `PlatformLoginRequest` NO incluye `IdTenant` (`AuthController.cs:411-422`).
- Valida Argon2id directo, estado de cuenta, **NO valida acceso a app en el servicio** — los permisos salen de `PermissionClaimBuilder` rama platform (Accesos con `IdUsuarioTenant == null`).
- `AuthenticationContext(usuario.Id, null, idApp, ...)` — platform scope.
- `AuthResponseDto.IdTenant = 0` (convención platform en respuesta).

### 2.3 Switch tenant

`SwitchTenantAsync` (`AuthService.cs:564`):
- Valida membresía **activa** `ObtenerActivoPorTenantAsync` + `Activo` + `IdEstado=Activo`.
- Contexto con `IdUsuarioTenant = membresia.Id`.
- Origen `AuthenticationOrigin.SwitchTenant`.

### 2.4 Switch to platform

`SwitchToPlatformAsync` (`AuthService.cs:614`):
- Valida `ExisteAccesoPlatformActivoAsync` (Acceso con `IdUsuarioTenant == null`).
- Revoca la sesión actual por `jti` (`ResolveAndRevokeSessionByJtiAsync`).
- Contexto platform scope, Origen `SwitchToPlatform`.
- ⚠️ **`is_system` se propaga** (bug de auditoría S6 corregido en A1.6).

---

## 3. Contrato de Permisos (claims `permiso`)

### 3.1 Ramas de PermissionClaimBuilder

`PermissionClaimBuilder.cs:19-56`:

| Rama | Condición | Fuente | Evidencia |
|------|-----------|--------|-----------|
| **Platform** | `IdTenant == null` | EF: `Accesos(IdUsuario, IdApp, Activo, IdUsuarioTenant==null)` → roles → permisos | `AuthRepository.cs:144-162` |
| **Tenant con membresía** | `IdUsuarioTenant.HasValue` | Resuelve UsuarioTenant → `SP_Permisos_Usuario_Efectivos` | `AuthRepository.cs:164-196` |
| **Tenant legacy** | `IdTenant != null && IdUsuarioTenant == null` | `SP_Permisos_Usuario_Efectivos` | `AuthRepository.cs:125-142` |

### 3.2 SP_Permisos_Usuario_Efectivos (`PASSWORDS SP.sql:601`)

- **Bypass EsSistema**: usuario con `EsSistema=1` recibe TODOS los permisos activos (sin joins).
- `RolesBase`: `Accesos(IdUsuario, IdTenant, Activo, IdApp filtrado, rol Activo, rol.IdTenant=X o NULL)`.
- `RolesEfectivos`: cierre transitivo de `RolesHerencia` (máx 32 niveles, anti-ciclo).
- `PermisosUsuarioConcedidos` (IdTipoAsig=1) y `PermisosUsuarioDenegados` (IdTipoAsig=2) por UsuariosPermisos con ventana de fechas.
- Resultado: `(PermisosRol ∪ Concedidos) − Denegados`.

> **CONTRATO**: los permisos de tenant provienen de `Accesos.IdTenant` (no directamente de `UsuarioTenant`). La membresía `UsuarioTenant` es el *gate* de acceso al tenant (rama 2), y luego el SP filtra por `@IdTenant` derivado de la membresía.

---

## 4. Contrato de JWT

### 4.1 Claims (AuthenticationTokenIssuer, `AuthenticationTokenIssuer.cs:43-62`)

| Claim | Valor | Siempre presente |
|-------|-------|------------------|
| `sub` (NameIdentifier) | `IdUsuario` | ✅ |
| `IdApp` | `IdApp` del contexto | ✅ |
| `jti` | `Guid` nuevo | ✅ |
| `TenantId` | `IdTenant` | solo si `IdTenant.HasValue` |
| `UsuarioTenantId` | `IdUsuarioTenant` | solo si `IdUsuarioTenant.HasValue` |
| `is_system` | `"true"` | solo si `EsSistema` |
| `permiso` (varios) | códigos efectivos | según rama |

### 4.2 Claims NO incluidos
- No hay claim `name`/`email`/`role` estándar — solo permisos efectivos + identidad + contexto.

---

## 5. Contrato de Refresh

`RefreshTokenAsync` (`AuthService.cs:273`) + `AuthenticationTokenService.RefreshAsync`:
- Hash SHA256 del refresh → sesión por hash.
- Rechaza reuso (`INVALID_REFRESH`), expirados (`REFRESH_EXPIRED`).
- Contexto reconstruido desde `Sesion` (IdUsuario, IdTenant, IdApp) + `EsSistema` + `ResolverIdUsuarioTenantAsync`.
- Rota refresh token en la sesión existente (`RotateRefreshTokenAsync`) — **no crea sesión nueva**.
- Reconstruye claims de permisos (rama según contexto).

---

## 6. Contrato OAuth / Login Externo

### 6.1 Endpoints

| Endpoint | Autorización | Params | Evidencia |
|----------|-------------|--------|-----------|
| `POST /api/auth/externo/login` | `[AllowAnonymous]` | `LoginExternoRequest`: `IdTenant [Required]`, `IdApp [Required]`, `ProviderCode`, `AuthorizationCode`, `RedirectUri` | `ExternalAuthController.cs:51-98` |
| `GET /api/auth/externo/{provider}/authorize` | `[AllowAnonymous]` | `idApp` query (default 1), tenant por header `X-Tenant-Code` o query `idTenant` | `ExternalAuthController.cs:239-273` |
| `GET /api/auth/externo/{provider}/callback` | `[AllowAnonymous]` | code, state (valida OAuthSession cache, PKCE, nonce, provider match) | `ExternalAuthController.cs:100-219` |
| `GET /api/auth/externo/proveedores-login` | `[AllowAnonymous]` | `idTenant` | `ExternalAuthController.cs:221-237` |

### 6.2 Contrato
- OAuth **exige IdTenant + IdApp** (igual que login local).
- RedirectUri proviene de `ConfProvIden.Callback` (vía OAuthSession), nunca de `Request.Host` (regla 23 AGENTS.md).
- Callback → `LoginExternoAsync` → MFA post-OAuth o redirect a `/signin-callback#...` con tokens.
- Proveedores deshabilitados/sin config → no aparecen o retornan `provider_not_found`.

### 6.3 ⚠️ Divergencia SP_Auth_LoginExterno (canónico vs A1.4)

| Fuente | INSERT Accesos | IdUsuarioTenant |
|--------|---------------|-----------------|
| `PASSWORDS SP.sql` (canónico) :1305/:1340/:1373 | `(IdUsuario, IdTenant, IdApp, IdRol)` | ❌ **NO asigna** |
| `Migrations\A1\013_A1.4_StoredProcedures.sql` :292/:331/:370 | `(IdUsuario, IdTenant, IdApp, IdRol, IdUsuarioTenant)` | ✅ resuelve y asigna |

**Impacto**: la versión canónica genera Accesos OAuth con `IdUsuarioTenant = NULL` (platform-scope), lo que desvía los permisos a la rama 1 (platform) en `PermissionClaimBuilder` y deja sin `UsuarioTenantId` el JWT. La DB viva usa la versión A1.4 (migración ejecutada), por lo que el bug solo se manifiesta si se re-ejecuta el SP canónico. **G7 nuevo — fuente canónica desincronizada con A1.4.**

**Acción sugerida**: actualizar `PASSWORDS SP.sql` para alinear los 3 INSERT de Accesos con la migración A1.4 (resolver `@IdUsuarioTenant` antes del INSERT).

---

## 7. Respuesta a las 15 preguntas del contrato (§4 del brief)

| # | Pregunta | Respuesta | Evidencia |
|---|----------|-----------|-----------|
| 1 | ¿Login local requiere App? | **SÍ** (siempre) | `LoginRequest.IdApp [Required]` + `SP_Auth_Login @IdApp` |
| 2 | ¿Login local requiere Tenant? | **SÍ** (siempre) | `LoginRequest.IdTenant [Required]` + `SP_Auth_Login @IdTenant` |
| 3 | ¿Login platform-scope sin Tenant? | **SÍ**, vía `login/platform` | `PlatformLoginRequest` sin IdTenant; contexto `IdTenant=null` |
| 4 | ¿Multi-App por Tenant? | **SÍ** — Apps es catálogo global; acceso por `Accesos.IdApp` | SP_Auth_Login check por IdApp |
| 5 | ¿Multi-Tenant por App? | **SÍ** — una App accedida desde N tenants | `Accesos` con IdTenant distintos por usuario |
| 6 | ¿Credenciales dependen de App/Tenant? | Hash por usuario (no por app); **política y permisos** dependen de App+Tenant | cascada `RolesPoliticasPwd`/`PoliticasPwd` en SP; SP_Permisos por IdApp/IdTenant |
| 7 | ¿OAuth depende de App/Tenant? | **SÍ** — `IdTenant`+`IdApp` requeridos | `LoginExternoRequest` |
| 8 | ¿Significado de `IdApp` en JWT? | App de contexto; **siempre presente** | `AuthenticationTokenIssuer.cs:48` |
| 9 | ¿Significado de `TenantId` en JWT? | Tenant activo de contexto; ausente en platform scope | `AuthenticationTokenIssuer.cs:52-53` |
| 10 | ¿Significado de `UsuarioTenantId`? | Membresía UsuarioTenant activa; ausente si no se resuelve | `AuthenticationTokenIssuer.cs:55-56` |
| 11 | ¿Combinaciones válidas? | `(tenant, app)` y `(null, app)`; nunca `(tenant, null)` | contextos de login/switch |
| 12 | ¿App sin Tenant? | Solo vía platform login/switch-to-platform | `PlatformLoginAsync`, `SwitchToPlatformAsync` |
| 13 | ¿Tenant sin App? | **NO válido** — login local exige ambos | `LoginRequest` |
| 14 | ¿OAuth deshabilitado? | Login local sigue funcionando; authorize/proveedores devuelven config no disponible | `ExternalAuthController` + FASE 17 |
| 15 | ¿EsSistema? | Bypass de Acceso check (SP) + todos los permisos (SP_Permisos) + claim `is_system` | `PASSWORDS SP.sql:1590,610` |

---

## 8. Implicaciones para seeds (G1/G2/G3/G4)

1. **G1 — Seeds deben crear UsuarioTenant** para que la rama 2 de permisos funcione y `ResolverIdUsuarioTenantAsync` no devuelva null. Sin membresía, el JWT sale sin `UsuarioTenantId` y los permisos caen a rama 3 (legacy) → **el login local funcional de tenant depende de UsuarioTenant poblado**.
2. **G2 — Hash placeholder**: `SP_Auth_Login` compara BIN2 con el hash almacenado; un placeholder no coincide → `CredencialesInvalidas`. Debe usarse hash real de `Admin@123` (`g0mWVED...`).
3. **G3 — Fixtures**: el hash `CHIjGP9...` (B7$k9mX) no coincide con `PWD='Admin@123'` de los tests → corregir a `g0mWVED...`.
4. **G4 — VERIFY/VALIDATE**: deben validar que cada usuario con Acceso tenant-scope tiene membresía `UsuarioTenant` activa y que `Accesos.IdUsuarioTenant` apunta a la membresía correcta.

---

## 9. Archivos fuente trazados (evidencia)

| Archivo | Rol |
|---------|-----|
| `PassPlat.WebAPI\Controllers\AuthController.cs` | Endpoints login, refresh, logout, me, platform, switch, reset, MFA |
| `PassPlat.WebAPI\Controllers\ExternalAuthController.cs` | OAuth login/callback/authorize/proveedores |
| `PassPlat.Aplicacion\Services\SPro\AuthService.cs` | Orquestación login/refresh/switch/auditoría |
| `PassPlat.Aplicacion\Services\Authentication\AuthenticationContext.cs` | Record de contexto (3 modos) |
| `PassPlat.Aplicacion\Services\Authentication\AuthenticationTokenIssuer.cs` | Construcción de claims JWT |
| `PassPlat.Aplicacion\Services\Authentication\AuthenticationTokenService.cs` | Login/OAuth/Refresh flows |
| `PassPlat.Aplicacion\Services\Authentication\Claims\PermissionClaimBuilder.cs` | 3 ramas de permisos |
| `PassPlat.Aplicacion\Services\Authentication\SessionManager.cs` | Creación/rotación/revocación de sesiones |
| `PassPlat.Datos\Repositories\AuthRepository.cs` | SP + queries de permisos |
| `PASSWORDS SP.sql` | SP_Auth_Login (:1495), SP_Permisos_Usuario_Efectivos (:601), SP_Auth_LoginExterno (:1201) |

---

## 10. Estado de FASE 2 (S11.2)

- [x] Trazado completo del pipeline de login local, platform, switch, refresh y OAuth.
- [x] Respuestas a las 15 preguntas del contrato.
- [x] Determinado que el filtro de tenant en login local usa `Usuarios.IdTenant` (legacy), no UsuarioTenant.
- [x] Determinado que los permisos de tenant dependen de `Accesos.IdTenant`, con UsuarioTenant como gate de membresía (rama 2).
- [x] Confirmado que el hash del password se verifica como **exacto** (BIN2) — los hashes en seeds deben ser reales.
- [x] Confirmado que `IdApp` es obligatorio en todos los flujos; `IdTenant` obligatorio excepto platform scope.
- [ ] Pendiente: aprobación Opción A (FASE 5) e implementación de seeds/fixtures/verify.
