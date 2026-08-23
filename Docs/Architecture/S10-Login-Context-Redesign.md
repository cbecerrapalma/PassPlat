# S10 — Auditoría de Rediseño de Login (App → Tenant → Método → Credenciales)

> **Estado**: ENTREGA IMPLEMENTADA (P0 + P1 completados, FASE 1-2)
> **Fecha**: 2026-07-31
> **Baseline congelado**: A1.8 (24/24), A1.9 (17/17), xUnit (66/66), Google xUnit (39/39), Build 0 errores
> **Nota**: La auditoría (FASE 0) fue read-only. La implementación P0-P1 ya se aplicó y se documenta en §16.

---

## 1. Inventario del modelo App / Tenant

| Tabla | PK | Columnas clave | Estado en DDL commit | Estado en DB live |
|-------|----|----------------|----------------------|-------------------|
| `Tenants` | Id (int) | Codigo, Nombre, Activo, EsSistema, FecCrea | ✅ `PASSWORDS.sql:257` | 3 filas |
| `Apps` | Id (int) | Codigo, Nombre, UrlBase, Activa, FecCrea | ✅ `PASSWORDS.sql:2272` | 3 filas |
| `Accesos` | Id | IdUsuario, IdTenant, IdApp, IdRol, Activo | ✅ `PASSWORDS.sql:2647` (SIN `IdUsuarioTenant`) | 13 filas (CON columna `IdUsuarioTenant`) |
| `UsuarioTenant` | Id | IdUsuario, IdTenant, IdEstado, Activo | ❌ **NO existe** en `PASSWORDS.sql` ni migraciones | ✅ **EXISTE** (61 filas) |

### Hallazgo CRÍTICO 1 — Schema Drift de `UsuarioTenant`
- La tabla `UsuarioTenant` **no está definida** en `PASSWORDS.sql` ni en ningún script de `Migrations\` (búsqueda sin resultados).
- La entidad EF `UsuarioTenant` está mapeada a la tabla `UsuarioTenant` (`UsuarioTenantConfiguration.cs` → `ToTable("UsuarioTenant")`) y **funciona contra la DB live**, lo que demuestra que la tabla existe en la base real.
- `Accesos` live tiene la columna extra `IdUsuarioTenant` que el DDL commit no define.
- **Origen de la tabla**: no se localizó script SQL commit que la cree. Única fuente que la puebla: `A1.8_test_fixtures.sql`. Clasificación: **SEED GAP / ARCHITECTURAL GAP**.

### Inventario Live DB (sqlcmd verificado)
```
UsuarioTenant = 61 filas   Apps = 3   Tenants = 3   Accesos = 13
```

---

## 2. Estado real de los seeds

| Script | Contenido | Problema |
|--------|-----------|----------|
| `Seed\Catalogo\06_Apps.sql` | 1 sola App (PASSPLAT, Id=1) con guard `IF NOT EXISTS` | DB live tiene 3 Apps → guard silencia la diferencia |
| `Seed\Configuracion\07_Usuarios.sql` | sistema (Id=1), platform_admin (Id=2) + Accesos sin `IdUsuarioTenant` | No crea `UsuarioTenant` |
| `Seed\Tenant\06_Accesos.sql` | Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo) | No crea `UsuarioTenant` ni `IdUsuarioTenant` |
| `A1.8_test_fixtures.sql` | INSERTs en `UsuarioTenant` + `Accesos` con `IdUsuarioTenant` (15, 30, 34, 39, 43, 47, 63, 67, 83...) | **Única** fuente SQL que puebla `UsuarioTenant` |

### Conclusión de seeds
Los seeds commits **no reproducen** el estado de la DB live:
1. No crean la tabla `UsuarioTenant` (drift DDL).
2. No insertan `UsuarioTenant` ni `Accesos.IdUsuarioTenant`.
3. Solo siembran 1 App mientras live tiene 3.
4. `platform_admin` (Id=2) y `cbecerrapalma` (Id=7) tienen `Acceso` directo sin `UsuarioTenant`.

Clasificación: **SEED GAP** (bloquea S10.15 seed reproducible).

---

## 3. Contrato actual de login

### `POST /api/auth/login` — `LoginRequest` (`AuthController.cs:396-409`)
```csharp
public class LoginRequest
{
    public string? NomUsuario { get; init; }
    public string? Email { get; init; }
    [Required] public int IdApp { get; init; }
    [Required(AllowEmptyStrings = false)] public string Password { get; init; } = string.Empty;
    public int? IdDisp { get; init; }
    public int? IdIP { get; init; }
    public int? IdAgente { get; init; }
    [Required] public int IdTenant { get; init; }
}
```

**Verificado**: El contrato backend **YA exige** `IdApp` y `IdTenant` como `[Required]`. El usuario puede identificarse por `NomUsuario` **o** `Email` (ambos opcionales, al menos uno debe llegar al SP).

### `POST /api/auth/login/platform` — `PlatformLoginRequest` (`AuthController.cs:411-422`)
- `NomUsuario` [Required], `IdApp` [Required], `Password` [Required]. Sin `IdTenant` (ámbito plataforma).

### `POST /api/auth/switch-tenant/{idTenant}` — `SwitchTenantRequest` (`AuthController.cs:424-431`)
- `IdApp` [Required] + IdDisp/IdIP/IdAgente.

### `POST /api/auth/switch-to-platform` — `SwitchToPlatformRequest` (`AuthController.cs:433-439`)
- `IdApp` [Required] + IdDisp/IdIP/IdAgente.

### `POST /api/auth/validar-mfa` — `ValidarMfaLoginRequest` (`AuthController.cs:384-394`)
- `IdUsuario`, `IdTenant`, `IdApp`, `IdMFAPrincipal`, `CodigoMFA` todos `[Required]`.

### SP `SP_Auth_Login` (`PASSWORDS SP.sql:1505`)
```sql
@IdTenant int, @IdApp int, @HashPwdCalculado nvarchar(512), @NomUsuario nvarchar(100)=NULL,
@Email nvarchar(255)=NULL, @IdDisp, @IdIP, @IdAgente
```
- Valida `Tenants.Activo`, tenant `EsSistema`, resuelve usuario **por `Usuarios.IdTenant`** (legacy, NO UsuarioTenant).
- `AuthRepository.LoginAsync` (`AuthRepository.cs:22-36`) mapea `@IdTenant` + `@IdApp` directamente.

---

## 4. Contrato actual de OAuth

### `ExternalAuthController` — clase `[AllowAnonymous]` (`ExternalAuthController.cs:15`)

| Endpoint | Parámetros | Autenticación |
|----------|-----------|---------------|
| `POST /api/auth/externo/login` | `LoginExternoRequest` | Anónimo |
| `GET /api/auth/externo/{provider}/callback` | code, state, error | Anónimo |
| `GET /api/auth/externo/proveedores-login?idTenant=` | idTenant | Anónimo |
| `GET /api/auth/externo/{provider}/authorize` | **`idApp = 1` default**, `idTenant` nullable | Anónimo |
| `GET /api/auth/externo/proveedores?idTenant=1` | idTenant (default 1) | Anónimo |

### `LoginExternoRequest` (`ExternalAuthController.cs:321-333`)
```csharp
[Required] int IdTenant; [Required] int IdApp; [Required] string ProviderCode;
[Required] string AuthorizationCode; [Required] string RedirectUri;
int? IdDisp; int? IdIP; int? IdAgente; string? CodeVerifier; string? Nonce;
```

### Flujo de estado (state)
1. `GenerateAuthorizationUrlAsync(providerCode, idTenant, idApp=1)` (`ExternalAuthService.cs:329`) genera `state` aleatorio + PKCE + nonce.
2. Guarda `OAuthSession` en caché `oauth_state:{state}` con `IdTenant`, `IdApp`, `RedirectUri` (`ExternalAuthService.cs:369-377`).
3. Callback lee `OAuthSession` de la caché, **recupera `IdTenant` + `IdApp` del session** y llama `LoginExternoAsync(session.IdTenant, session.IdApp, ...)` (`ExternalAuthController.cs:175-177`).
4. Tras login → redirect a `/signin-callback#accessToken=...&refreshToken=...&idUsuario=...&idTenant=...` o `?mfaUsuario=&mfaTenant=&mfaMFA=` (MFA).

**Verificado**: OAuth ya transporta `IdTenant` + `IdApp` vía `OAuthSession`/state.

### Hallazgo CRÍTICO 2 — `authorize` con `idApp=1` default
`GET /api/auth/externo/{provider}/authorize` usa `idApp = 1` como default y `idTenant` nullable. Si el usuario NO seleccionó App en la UI, se asume App 1 (PASSPLAT) y el `idTenant` debe venir de query param o header `X-Tenant-Code`. Clasificación: **UX-UI GAP** (no validado contra la selección previa del usuario).

---

## 5. Flujo actual `/login` (Login.razor)

```
OnInitializedAsync
  ├── Si Auth.IsAuthenticated → redirige a "/"
  ├── Parsea query params de error OAuth (error, state_invalido, proveedor_rechazo, ...)
  ├── Auth.GetTenantInfoAsync()  → api/auth/tenant-info (X-Tenant-Code o ResolvedTenantId)
  ├── Auth.GetAppsAsync()        → api/apps/activas
  ├── _selectedAppId = Auth.AppId
  ├── _requiereSeleccionApp = _apps.Count > 1 && _selectedAppId <= 0
  │     ├── Si requiere selección App → muestra MudSelect de Apps + botón "Continuar"
  │     └── Si no: tenantInfo.RequiereSeleccion
  │           ├── true → MudSelect de Tenants (api/auth/tenants)
  │           └── false y IdTenant.HasValue → CargarProveedoresAsync()
  │
  └── MudRadioGroup _selectedAuthMethod = "password" | "oauth"
        ├── password → formulario usuario + contraseña → DoLogin()
        └── oauth → botones de proveedores → IniciarConProveedorAsync(codigo)

DoLogin(): Auth.LoginAsync(_username, _password, Auth.AppId, _rememberMe, idTenant)
MFA: VerificarMfa() → CompletarLoginMFAAsync(idUsuario, idTenant, idApp, idMFAPrincipal, codigo)
OAuth: Http.GetFromJsonAsync("api/auth/externo/{codigo}/authorize?idTenant={X}&idApp={Y}")
```

### Hallazgo CRÍTICO 3 — App Selector NO funciona pre-login
`Auth.GetAppsAsync()` (`AuthService.cs:132-142`) llama `GET /api/apps/activas`. Pero `AppsController` tiene `[Authorize(Policy = "APPS_VER")]` a nivel de clase (`AppsController.cs:13`), y `PermissionPolicyProvider` (`PermissionPolicyProvider.cs:17-26`) exige claim `permiso == "APPS_VER"`. **Sin JWT (login pre-auth) la llamada retorna 401** → `GetAppsAsync` devuelve `[]` → `_requiereSeleccionApp = false` (porque `_apps.Count == 0`) → **el App Selector NUNCA se muestra**.

**Impacto**: El paso "App" del flujo App→Tenant→Método→Credenciales **no se puede completar** desde la UI de login. `Auth.AppId` queda en su default `1` (`AuthService.cs:42`), lo que funciona solo porque `idApp=1` (PASSPLAT) es la App predominante. Clasificación: **CONTRACT GAP / ARCHITECTURAL GAP**.

---

## 6. Puntos donde `IdApp` e `IdTenant` entran al backend

| Capa | Símbolo | IdApp | IdTenant |
|------|---------|-------|----------|
| Controller | `AuthController.Login` | ✅ `LoginRequest.IdApp [Required]` | ✅ `LoginRequest.IdTenant [Required]` |
| Controller | `AuthController.PlatformLogin` | ✅ | ❌ (platform scope) |
| Controller | `AuthController.SwitchTenant` | ✅ | ✅ (route) |
| Controller | `AuthController.SwitchToPlatform` | ✅ | ❌ (platform scope) |
| Controller | `AuthController.ValidarMFA` | ✅ | ✅ |
| Controller | `AuthController.OlvidoPassword` | ✅ `idApp = request.IdApp ?? 1` | ✅ `request.IdTenant` |
| Controller | `ExternalAuthController.Authorize` | ✅ default 1 | ✅ nullable |
| Controller | `ExternalAuthController.Callback` | ✅ desde OAuthSession | ✅ desde OAuthSession |
| Service | `AuthService.LoginAsync` (SPro) | ✅ `idApp` | ✅ `idTenant` |
| Repository | `AuthRepository.LoginAsync` | ✅ `@IdApp` | ✅ `@IdTenant` |
| SP | `SP_Auth_Login` | ✅ | ✅ |
| Contexto | `AuthenticationContext` | ✅ `IdApp` | ✅ `IdTenant?` |
| JWT | `AuthenticationTokenIssuer` | ✅ claim `IdApp` | ✅ claim `TenantId` (condicional) |

**Verificado**: `AuthenticationContext` (`AuthenticationContext.cs`) es `record(int IdUsuario, int? IdTenant, int IdApp, short? IdDispositivo, int? IdIp, AuthenticationOrigin Origen, bool EsSistema, int? IdUsuarioTenant)`. El backend ya lleva `IdApp` en todos los puntos de entrada.

---

## 7. AuthenticationContext

```csharp
public sealed record AuthenticationContext(
    int IdUsuario,
    int? IdTenant,
    int IdApp,
    short? IdDispositivo,
    int? IdIp,
    AuthenticationOrigin Origen,
    bool EsSistema = false,
    int? IdUsuarioTenant = null);
```
- Fuente: `PassPlat.Aplicacion\Services\Authentication\AuthenticationContext.cs`
- `IdApp` ya es parte del contexto (no requiere cambio estructural).

---

## 8. JWT

`AuthenticationTokenIssuer.BuildIdentityClaims` (`AuthenticationTokenIssuer.cs:43-59`):
```csharp
new(ClaimTypes.NameIdentifier, context.IdUsuario.ToString()),
new("IdApp", context.IdApp.ToString()),               // ← ID App claim
...
new("TenantId", ...)            // solo si IdTenant.HasValue
new("UsuarioTenantId", ...)     // solo si IdUsuarioTenant.HasValue
new("is_system", "true")        // solo si EsSistema
```
- Permisos: `claims.AddRange(permisoClaims)` — claims `permiso` (68 para login clásico).
- **Verificado**: JWT ya incluye `IdApp`. No requiere cambio en `AuthenticationTokenIssuer` (regla S10: NO tocar sin PRODUCTION BUG).

---

## 9. UsuarioTenant

| Aspecto | Estado |
|---------|--------|
| Entidad | `PassPlat.Dominio\Entities\Core\UsuarioTenant.cs` (Id, IdUsuario, IdTenant, IdEstado, Activo, FecAlta, FecMod, IdUsrMod, navegación Accesos) |
| EF Config | `UsuarioTenantConfiguration.cs` → `ToTable("UsuarioTenant")`, índice único `UX_UsuarioTenant_Usuario_Tenant`, FKs a Usuario/Tenant/Estado |
| Repositorio | `IUsuarioTenantRepository` + `UsuarioTenantRepository` — 7 métodos (ObtenerPorUsuario, ObtenerMembresia, ObtenerActivosPorUsuario, ObtenerActivoPorTenant, ExisteMembresia, ResolverIdUsuarioTenant, ObtenerIdsUsuariosActivosPorTenant) |
| DI | `DatosDependencyInjection.cs` — registrado interface + concreto |
| DDL commit | ❌ **Ausente** (drift) |
| DB live | ✅ Existe (61 filas) |
| Seeds | ❌ No la crean ni pueblan |

**Fuente de verdad de membresía**: `IUsuarioTenantRepository` (decisión A1). `PermissionClaimBuilder` ramifica por `IdTenant==null` (platform) / `IdUsuarioTenant.HasValue` (tenant con membresía) / sin membresía.

---

## 10. Archivos que deberán modificarse (propuesta)

| Archivo | Cambio propuesto | Clasificación |
|---------|------------------|---------------|
| `PassPlat.WebAPI\Controllers\AppsController.cs:13` | Quitar `[Authorize(Policy="APPS_VER")]` de clase para `activas` **o** crear endpoint público `GET /api/auth/apps-activas` anónimo | CONTRACT GAP |
| `PassPlat.Web\Pages\Login.razor` | Ajustar lógica `_requiereSeleccionApp` para que funcione pre-login | UX-UI GAP |
| `PassPlat.Web\Services\AuthService.cs:42` | Revisar default `AppId = 1` vs flujo de selección | UX-UI GAP |
| `PassPlat.WebAPI\Controllers\ExternalAuthController.cs:240` | `authorize` — validar `idApp` desde la sesión/selección previa, no default fijo | UX-UI GAP |
| `BBDD\Seed\Catalogo\06_Apps.sql` | Sincronizar con 3 Apps live | SEED GAP |
| `BBDD\Seed\Configuracion\07_Usuarios.sql` | Crear `UsuarioTenant` para sistema/platform_admin | SEED GAP |
| `BBDD\Seed\Tenant\06_Accesos.sql` | Insertar `IdUsuarioTenant` | SEED GAP |
| `Migrations\` (nuevo) | DDL formal de `UsuarioTenant` + `Accesos.IdUsuarioTenant` | SEED GAP |
| `Docs\Architecture\S10-Seed-Authentication-Context.md` | Actualizar con hallazgos de esta auditoría | Doc |

**NO se modificarán** (regla S10): `AuthenticationTokenIssuer`, `JwtTokenService`, `ExternalAuthService`, `SessionManager`, `PermissionClaimBuilder`, `AuthenticationMiddleware` — salvo PRODUCTION BUG demostrado.

---

## 11. Riesgos de seguridad

| Riesgo | Severidad | Detalle |
|--------|-----------|---------|
| App Selector inoperante pre-login | Media | `api/apps/activas` protegida por `APPS_VER`; login pre-auth 401 → el selector no muestra nada. |
| Default `idApp=1` silencioso | Media | Si una App distinta a PASSPLAT necesita login, el usuario no puede seleccionarla. |
| `authorize` `idApp=1` default | Media | OAuth asume App 1 si la UI no pasa la App seleccionada. |
| `SP_Auth_Login` usa `Usuarios.IdTenant` legacy | Media | La resolución de membresía NO usa `UsuarioTenant` en el SP; depende de la columna legacy. Aislado a A1 (sin fallback reintroducido). |
| Seeds no reproducen `UsuarioTenant` | Alta | Entorno nuevo no tendrá membresías → fallos de login tenant-scope. |
| `proveedores` default `idTenant=1` | Baja | Sin tenant seleccionado lista proveedores del tenant 1. |

---

## 12. Propuesta de arquitectura

### Flujo objetivo App → Tenant → Método → Credenciales

```
1. App      GET /api/auth/apps-activas (ANÓNIMO, devuelve Id/Codigo/Nombre/Activa)
            → Usuario selecciona App → Auth.AppId = X
2. Tenant   GET /api/auth/tenant-info (X-Tenant-Code) o GET /api/auth/tenants (anónimo)
            → Usuario selecciona Tenant → idTenant = Y
3. Método   GET /api/auth/externo/proveedores-login?idTenant=Y (anónimo)
            → MudRadioGroup: "Contraseña" | "Proveedor OAuth"
4. Credenciales
   ├── Password:  POST /api/auth/login { NomUsuario|Email, IdApp=X, IdTenant=Y, Password }
   ├── OAuth:     GET /api/auth/externo/{codigo}/authorize?idTenant=Y&idApp=X
   │              → state (OAuthSession con IdTenant+IdApp) → callback → LoginExternoAsync
   └── MFA:       POST /api/auth/validar-mfa { IdUsuario, IdTenant=Y, IdApp=X, ... }
5. JWT       claims: sub, IdApp=X, TenantId=Y, UsuarioTenantId, is_system, permiso...
```

### Decisiones clave
1. **Endpoint público de Apps**: crear `GET /api/auth/apps-activas` anónimo (solo Id/Codigo/Nombre/Activa) o marcar `apps/activas` como `[AllowAnonymous]`. El catálogo de Apps es **catalogo público** (solo identifica aplicaciones, no expone datos sensibles).
2. **La selección de App/Tenant es UX, no cambio de contrato**: el backend ya exige IdApp+IdTenant. Solo falta que la UI los capture y los propague correctamente.
3. **OAuth idApp**: `authorize` debe recibir la App seleccionada (`idApp=X`) desde la UI y guardarla en `OAuthSession` (ya lo hace en `GenerateAuthorizationUrlAsync`).
4. **Seeds**: crear DDL de `UsuarioTenant`, poblar miembros en seeds, y añadir `IdUsuarioTenant` a los Accesos seed. Necesita decisión A1/arquitectura antes de tocar seeds (bloqueado por SEED GAP).
5. **SP_Auth_Login**: mantener firma `@IdTenant, @IdApp`; **no** cambiar a UsuarioTenant sin decidir migración (regla S10: no reabrir A1.1 sin gate).

---

## 13. Propuesta de UX

| Paso | UI actual | Propuesta |
|------|-----------|-----------|
| App | `MudSelect` (solo si >1 apps Y app no seleccionada) | Mostrar siempre paso App si el catálogo tiene >1 apps activas; cargar desde endpoint anónimo |
| Tenant | `MudSelect` (si tenant-info requiere selección) | Mantener; añadir indicador de tenant resuelto vía X-Tenant-Code |
| Método | `MudRadioGroup` Contraseña/OAuth | Mantener; cuando se elige OAuth y hay proveedores → lista de botones |
| Credenciales | Form usuario+password o botones proveedor | Mantener; validar `Auth.AppId` y `idTenant` antes de `DoLogin` |
| MFA | Form código MFA | Mantener |
| Errores | `MudAlert`/`Snackbar` con `Api.LastError` | Mantener (regla AGENTS) |

---

## 14. Matriz de tests propuesta

| Caso | Descripción | Tipo |
|------|-------------|------|
| A | Login password App+Tenant seleccionados | Funcional |
| B | Login OAuth App+Tenant → authorize → callback → JWT | Funcional |
| C | MFA TOTP/Email post-login | Funcional |
| D | App no seleccionada → error validación / selector obligatorio | Negativo |
| E | Tenant no seleccionado → error / selector obligatorio | Negativo |
| F | `api/apps/activas` anónimo → lista Apps (200) | Contrato |
| G | `authorize` con idApp=X → state contiene IdApp=X | Contrato |
| H | Switch tenant / switch-to-platform con IdApp=X | Funcional |

---

## 15. Conclusión sobre el flujo App → Tenant → Método → Credenciales

### Verificado (CONTRATO backend ya cumple)
1. `LoginRequest` exige `IdApp [Required]` + `IdTenant [Required]`.
2. `AuthenticationContext` incluye `IdApp`.
3. JWT incluye claim `IdApp`.
4. `SP_Auth_Login` recibe `@IdTenant` + `@IdApp`.
5. OAuth transporta `IdTenant` + `IdApp` vía `OAuthSession`/state.

### No verificado / GAP (debe corregirse)
1. **CONTRACT GAP**: App Selector inoperante pre-login (`api/apps/activas` protegida por `APPS_VER`).
2. **UX-UI GAP**: defaults `idApp=1` en `AuthService`/`authorize`; selección de App/Tenant no siempre propagada.
3. **SEED GAP**: `UsuarioTenant` ausente en DDL/seeds; solo 1 App en seed; `IdUsuarioTenant` no sembrado.
4. **ARCHITECTURAL GAP**: tabla `UsuarioTenant` sin DDL formal commit.

### Veredicto de hipótesis
El flujo **App → Tenant → Método → Credenciales** es **CORRECTO y ya está soportado por el contrato backend**. El trabajo restante es: (a) habilitar el catálogo de Apps de forma anónima para el paso App, (b) garantizar que la UI propague `Auth.AppId` + `idTenant` a todos los endpoints (login, OAuth, MFA, switch), (c) cerrar el SEED GAP de `UsuarioTenant`. **No requiere** cambios en `AuthenticationTokenIssuer`, JWT, ni contratos OAuth.

---

## Próximos pasos
1. ✅ **P0** — `AppsController.GetActivas` marcado `[AllowAnonymous]` (catálogo público).
2. ✅ **P1** — `Login.razor` propagación App/Tenant: `ResolverTenantAsync()` extraído y compartido; `ContinuarConApp()` resuelve tenant tras elegir app; `ContinuarConTenant()` fija `_resolvedTenantId`; formulario credenciales oculto mientras app/tenant sin resolver; MFA resend usa `Auth.AppId` (no `AppSettings.AppId`).
3. ✅ **P4** — `authorize` ya recibe `idApp` vía query param y la UI lo pasa (`idApp={Auth.AppId}`). El default `idApp=1` queda solo como fallback para llamadas API directas (sin UI). Sin cambios de código necesarios.
4. ⏳ **P2-P3** — DDL idempotente de `UsuarioTenant` + `Accesos.IdUsuarioTenant` + seeds reproducibles (SEED GAP, requiere decisión A1/arquitectura antes de tocar DDL/seeds).
5. Regresión obligatoria tras cada cambio: Build 0E ✅, xUnit 66/66 ✅, Google 39/39 (incluido), **A1.8 24/24 ✅**, **A1.9 17/17 ✅**.

---

## 16. Registro de implementación (FASE 1-2)

### P0 — Catálogo de Apps anónimo (CONTRACT GAP)
| Archivo | Cambio |
|---------|--------|
| `PassPlat.WebAPI\Controllers\AppsController.cs:32` | `[AllowAnonymous]` + `[HttpGet("activas")]` en `GetActivas`. El resto de endpoints (`APPS_CREAR`, `APPS_ELIMINAR`) mantienen autorización. |

**Verificación**: `GET /api/apps/activas` anónimo → `200` con `[{"id":1,"codigo":"PASSPLAT","nombre":"AccessPlat","urlBase":null,"activa":true,"fecCrea":"..."}]`.

### P1 — Flujo App → Tenant → Método → Credenciales en `Login.razor`
| Archivo | Cambio |
|---------|--------|
| `PassPlat.Web\Pages\Login.razor:376-406` | `OnInitializedAsync`: si `_apps.Count > 1` → selector obligatorio (`_requiereSeleccionApp=true`); si 1 app → auto-selección en `Auth.AppId`; luego `ResolverTenantAsync()`. |
| `Login.razor:392-406` | Nuevo `ResolverTenantAsync()`: si tenant auto-resuelto → `CargarProveedoresAsync()`; si `RequiereSeleccion` → lista tenants. |
| `Login.razor:431-440` | `ContinuarConApp()` → `Auth.AppId = _selectedAppId` + `ResolverTenantAsync()`. |
| `Login.razor:441-447` | `ContinuarConTenant()` → fija `_resolvedTenantId = _selectedTenantId` antes de cargar proveedores. |
| `Login.razor:90` | Formulario credenciales gated con `!_requiereSeleccionApp && !_requiereSeleccionTenant`. |
| `Login.razor:~539` | Reenviar código MFA usa `Auth.AppId` + idTenant explícito (ya no `AppSettings.AppId`). |

### Resultados de regresión tras P0-P1
- Build: **0 errores** (317 warnings MUD0002 preexistentes en páginas no relacionadas).
- xUnit (`PassPlat.Aplicacion.Test`): **66/66 PASS** (incluye Google 39/39).
- Playwright **A1.8: 24/24 PASS** y **A1.9: 17/17 PASS** (ejecutados contra API en Development, puerto 5259).
- Confirmación del diagnóstico 429: los fallos previos en batch eran el **rate-limit preexistente** (`LoginPolicy` PermitLimit=5 cuando la API corre sin `Development`), NO regresión de S10. Con API en Development (100/min) ambas gates pasan completas.

### Archivos NO modificados (regla S10)
`AuthenticationTokenIssuer`, `JwtTokenService`, `ExternalAuthService`, `SessionManager`, `PermissionClaimBuilder`, `AuthenticationMiddleware`, `AuthController`, `ExternalAuthController`, `SP_Auth_Login`.
