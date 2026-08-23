# S12 FASE 2.1 + 2R + 2R.1 — Contrato UI de Login

## Estado
**CERTIFICADA** — 2026-08-02 (FASE 2.1) / 2026-08-02 (FASE 2R) / 2026-08-02 (FASE 2R.1)

## Alcance

Corregir y certificar el contrato UI del login en `https://localhost:7275/login`:

- Flujo `App → Tenant → (formulario Usuario/Contraseña + proveedores OAuth simultáneos)`.
- Prohibido selector de método (Interno/Externo, Contraseña/OAuth).
- Formulario Usuario/Contraseña **siempre visible** tras resolver App+Tenant.
- Proveedores OAuth **siempre visibles** cuando existan, cargados dinámicamente por tenant.
- Fuente de verdad: `GET /api/auth/externo/proveedores-login?idTenant={idTenant}`.
- **FASE 2R**: herencia configurable de proveedores de la plataforma para tenants sin proveedores propios válidos (campo `ConfigApp`).

## Reglas del contrato

| Regla | Descripción |
|-------|-------------|
| R1 | No existe selector de método en ninguna etapa del flujo. |
| R2 | Usuario/Contraseña/Recordarme/Ingresar siempre visibles tras resolver App+Tenant. |
| R3 | Proveedores OAuth siempre visibles cuando `proveedores-login` devuelva datos para ese tenant. |
| R4 | Proveedores cargados dinámicamente (nunca hardcodeados en la UI). |
| R5 | Sin tenant resuelto, no se puede iniciar sesión (el formulario no se muestra hasta Continuar). |
| R6 | **FASE 2R.1** Sin App resuelta (IdApp real > 0), no se puede autenticar (form, botón, OAuth, DoLogin bloqueados). |
| R7 | **FASE 2R.1** Gate central único `IsAuthenticationContextReady = AppId > 0 && TenantId > 0`; prohibido duplicar condiciones por botón/componente. |
| R8 | **FASE 2R.1** Auto-resolución de App única solo si produce IdApp real; con 0 apps queda bloqueado (nunca el default `Auth.AppId=1`). |

## Diagnóstico (P0)

| Pregunta | Respuesta |
|----------|-----------|
| ¿Qué sirve el puerto 7275? | `PassPlat.Web` (Blazor WASM), único proyecto con `https://localhost:7275` |
| ¿7275 == 5273? | **Sí, mismo proceso.** El DevServer WASM bindea ambos (`--launch-profile https` → `applicationUrl: https://localhost:7275;http://localhost:5273`). PID único 13492. |
| ¿Instancia vieja? | No. `PassPlat.Web.dll` (01-08 14:48) más reciente que `Login.razor` (31-07 23:42). |
| ¿API en qué puerto? | `http://localhost:5000` (override `--urls`). WebAPI `launchSettings` declara 5001/5259 pero el Web apunta `ApiBaseUrl=http://localhost:5000`. |
| ¿BBDD? | `Server=.;Database=PassPlat` (appsettings.Development.json). |

## Causa raíz (P1)

`Login.razor` contenía un selector de método (`MudRadioGroup @bind-Value="_selectedAuthMethod"`) con
opciones "Contraseña" / "Proveedor OAuth". El formulario password se mostraba solo con
`_selectedAuthMethod == "password"` y el bloque OAuth solo con `== "oauth"`. Violación real de
R1/R2/R3 — no era un problema de entorno.

## Corrección implementada (P2–P5)

`D:\CODIGOS\PassPlat\PassPlat.Web\Pages\Login.razor`:

1. **Eliminado** `MudRadioGroup` y el campo `_selectedAuthMethod`.
2. Formulario Usuario/Contraseña renderizado cuando `!_mfaRequired && !_requiereSeleccionApp && !_requiereSeleccionTenant`.
3. Bloque OAuth renderizado en el mismo bloque, con estados:
   - Carga: `Cargando proveedores...`
   - Con datos: `O continúa con` + botones de ícono (primeros 3 + "más opciones").
   - Vacío: `No hay proveedores externos disponibles para este tenant.`
4. Añadido campo `_cargandoProveedores` y estado de carga en `CargarProveedoresAsync`.
5. Botón `INICIAR SESIÓN` exige también App resuelta.

No se modificó backend (AuthService, SPs, AuthenticationContext, AuthenticationTokenIssuer,
PermissionClaimBuilder, SessionManager).

## Evidencia de entorno (P8)

| Componente | URL | Proyecto | PID | Entorno |
|------------|-----|----------|-----|---------|
| Web | `https://localhost:7275` + `http://localhost:5273` | `PassPlat.Web` | 13492 | Development (perfil `https`) |
| API | `http://localhost:5000` | `PassPlat.WebAPI` | 16164 | Development (`--no-build --urls http://localhost:5000`) |
| SQL Server | `Server=.;Database=PassPlat` | — | — | Local (sa) |

- `GET /api/apps/activas` → 200, 1 app: `{"id":1,"codigo":"PASSPLAT","nombre":"AccessPlat"}` (auto-resuelta).
- `GET /api/auth/externo/proveedores-login?idTenant=1` → `[{"codigo":"GOOGLE","nombre":"Google","icono":"google"}]`.
- `GET /api/auth/externo/proveedores-login?idTenant=3` → `[]`.

## Proveedores dinámicos (evidencia BBDD)

`ConfProvIden`: los tenants 1/3/4 tienen filas para IdProvIden 1-7 con `Activo=1`, `Estado=1`,
`ClientId`/`ClientSecret`/`Callback` presentes, pero **solo** la fila `Id=1` (tenant 1, GOOGLE) tiene
`RolDefecto=4` **y** un `ClientSecret` real cifrado (prefijo `Edd9q…`); el resto (tenants 3/4) tienen
`RolDefecto=NULL` y `ClientSecret=GOOGLE_CLIENT_SECRET` (placeholder, falla `TryDecrypt`) →
`ExternalProviderValidator` los excluye. Por ello (antes de FASE 2R):

- Tenant 3 (Abarrotes del Sur): sin proveedores → mensaje vacío (comportamiento correcto).
- Tenant 1 (Plataforma): GOOGLE visible.

Esto certifica R3/R4 con configuración real, sin fixtures ni proveedores ficticios.

## FASE 2R — Herencia configurable de proveedores de la plataforma

### Decisión (usuario)

> "el proveedor de google actual es de la plataforma passplat, si los otros apps/tenants no cuentan
> con un proveedor seria bueno que existiera un campo configurable que permitiera mostrar o no el/los
> proveedores de la plataforma o no."

### Implementación

- **Campo configurable**: `ConfigApp` (global, `IdTenant=NULL`) con `Grupo='OAuth'`,
  `Clave='MostrarProveedoresPlataforma'`, `Valor='true'` (bool). Migración
  `Migrations\FASE2R_MostrarProveedoresPlataforma.sql` (idempotente, ya aplicada) y clave incluida
  en seed `BBDD\Seed\Configuracion\04_Infraestructura.sql` (MERGE id 9).
- **Constantes**: `PassPlat.Dominio\Constants\TenantCodes.cs` (`Plataforma="PLATFORM"`) y
  `ConfigAppKeys.cs` (`GrupoOAuth`, `MostrarProveedoresPlataforma`).
- **Servicio** `ExternalLoginProviderService.ObtenerDisponiblesAsync(idTenant)`:
  1. Devuelve los proveedores propios del tenant que pasan el validador.
  2. Si `Count==0` y `ConfigApp.MostrarProveedoresPlataforma == true`, resuelve el tenant plataforma
     por código (`TenantCodes.Plataforma`) y, si es distinto del tenant actual, devuelve sus
     proveedores válidos marcados con `EsDePlataforma=true`.
  3. Si el flag es `false`, devuelve `[]` (sin herencia).
- **DTO** `ExternalLoginProviderDto` añade `bool EsDePlataforma { get; set; }` (aditivo; la UI lo ignora).

### Comportamiento certificado (API real, flag `true`)

| `GET /api/auth/externo/proveedores-login?idTenant=` | Resultado | `esDePlataforma` |
|------|-----------|------------------|
| 1 (PLATFORM) | `[GOOGLE]` (propio) | `false` |
| 3 (ABARROTES) | `[GOOGLE]` (heredado) | `true` |
| 4 (VESTUARIO) | `[GOOGLE]` (heredado) | `true` |

Con flag `false`: tenant 3 → `[]`.

### Verificado en navegador real (7275)

1. Seleccionar "Abarrotes del Sur" → Continuar → formulario + **"O continúa con" + botón GOOGLE** simultáneamente (antes: "No hay proveedores...").

## FASE 2R.1 — Login Context Gate

### Defecto (detectado por el usuario tras la 2R)

La UI exigía Tenant pero **no exigía App explícitamente** como precondición de autenticación. El gate
del login era `!_requiereSeleccionApp && !_requiereSeleccionTenant` — banderas derivadas del conteo de
apps, no una condición central de contexto App+Tenant.

- `AuthService.cs:42` — `AppId { get; set; } = 1;` (**default 1 hardcodeado**).
- `Login.razor` `OnInitializedAsync`: con `Count==0` → `_selectedAppId = Auth.AppId` (1) sin validar
  app real; con `Count==1` → auto-resolución correcta (IdApp real); con `Count>1` → selector App.
- Escenario de violación del contrato: `Count==0` + tenant resuelto → form/Google con AppId ficticio.
- En el entorno real (1 app id=1, 3 tenants) la auto-resolución era correcta; el defecto era
  estructural (gate), no visible en este entorno.

### Causa raíz

1. Default `Auth.AppId = 1` sin fuente de datos.
2. `OnInitializedAsync` `Count==0` heredaba el default sin validar.
3. Gate de render basado en flags de conteo, no en validez de contexto.
4. `DoLogin` / `IniciarConProveedorAsync` sin validar `Auth.AppId > 0`.

### Corrección implementada (`PassPlat.Web\Pages\Login.razor`)

1. **Gate central único**:
   ```csharp
   private int TenantIdContexto => _resolvedTenantId ?? (_selectedTenantId > 0 ? _selectedTenantId : 0);
   private bool IsAuthenticationContextReady => Auth.AppId > 0 && TenantIdContexto > 0;
   ```
   Aplicado a las 3 condiciones de render (form, botón INICIAR SESIÓN, proveedores OAuth).
2. **`OnInitializedAsync`**: `Count==0` → `Auth.AppId=0` + mensaje "No hay aplicaciones";
   `Count==1` → auto-resolución con IdApp real; `Count>1` → selector App con `Auth.AppId=0`.
3. **`ContinuarConApp`**: guard `_selectedAppId <= 0`.
4. **`ContinuarConTenant`**: guard `Auth.AppId <= 0` (App precede a Tenant).
5. **`DoLogin`**: guard `!IsAuthenticationContextReady` (bloquea antes de `POST /api/auth/login`).
6. **`CargarProveedoresAsync`**: guard `Auth.AppId <= 0`.
7. **`IniciarConProveedorAsync`**: guard `!IsAuthenticationContextReady` (OAuth bloqueado).

No se modificó backend (AuthService/SPs/AuthenticationContext/AuthenticationTokenIssuer/
PermissionClaimBuilder/SessionManager) ni OAuth 2R (`ExternalLoginProviderService`,
`ExternalProviderValidator`, toggle ConfigApp).

### Estados UI certificados (manual + tests)

| Estado | UI |
|--------|-----|
| A. Sin App | Mensaje "No hay aplicaciones disponibles"; sin form, sin OAuth |
| B. App sin Tenant | Selector Tenant; Continuar deshabilitado; sin form, sin OAuth |
| C. App+Tenant | Form (Usuario/email, Contraseña, Recordarme, INICIAR SESIÓN) + "O continúa con" + OAuth simultáneos |
| Prohibido | "Tenant seleccionado, App=0, login visible" — FAIL (gate central) |

### Suite `tests/s12-login-context-gate.spec.ts` — 9/9 PASS

| # | Test | Criterio |
|---|------|----------|
| LOGIN-CONTEXT-01 | Sin App: form y OAuth bloqueados, AppId real no asumida | R8/R6 |
| LOGIN-CONTEXT-02 | Rechazo Tenant sin App: selector no permite auth | R6 |
| LOGIN-CONTEXT-03 | App sin Tenant: auth bloqueada (ni form ni Google) | R5 |
| LOGIN-CONTEXT-04 | App+Tenant: auth habilitada (form completo) | R2 |
| LOGIN-CONTEXT-05 | App+Tenant+OAuth: GOOGLE visible con form | R3 |
| LOGIN-CONTEXT-06 | Form+OAuth simultáneos sin selector de método | R1 |
| LOGIN-CONTEXT-07 | Login interno sin contexto: bloqueado antes de POST `/api/auth/login` | R6 backend |
| LOGIN-CONTEXT-08 | OAuth sin contexto: bloqueado (sin botones de proveedor) | R6 OAuth |
| LOGIN-CONTEXT-09 | Login completo: JWT con IdApp/IdTenant/IdUsuarioTenant/permisos | R5+JWT |

## Contrato verificado en navegador real (7275)

1. `/login` → App auto-resuelta, selector Tenant + Continuar deshabilitado. Sin radios de método.
2. Seleccionar "Abarrotes del Sur" → Continuar habilitado → formulario (Usuario/Contraseña/Recordarme/
   INICIAR SESIÓN/¿Olvidaste tu contraseña?) + "O continúa con" + botón GOOGLE (heredado de la plataforma).
3. Seleccionar "Plataforma" → formulario + "O continúa con" + botón GOOGLE simultáneamente.

## Suite de tests

`D:\CODIGOS\PassPlat\tests\s12-login-ui-contract.spec.ts` — 9 tests, `WEB_BASE` objetivo `https://localhost:7275`.

| # | Test | Regla |
|---|------|-------|
| 1 | Página carga sin errores de consola | smoke |
| 2 | App auto-resuelta (`GET /apps/activas` 200, sin selector App) | flujo |
| 3 | Tenant: 3 opciones, Continuar se habilita al seleccionar | flujo |
| 4 | No existe selector de método (0 radios, sin "Proveedor OAuth") | R1 |
| 5 | Formulario siempre visible tras resolver tenant | R2 |
| 6 | OAuth y formulario simultáneos en tenant 1 (GOOGLE) | R3 |
| 7 | Proveedores dinámicos coinciden con respuesta API | R4 |
| 8 | Login real 7275: JWT con claims correctos + form permanece | R5 + JWT |
| 9 | **FASE 2R** Herencia de proveedores de plataforma vía API (t1 `false`, t3/t4 `true`) | 2R |

Resultado: **9/9 PASS**.

`D:\CODIGOS\PassPlat\tests\s12-login-context-gate.spec.ts` — 9 tests LOGIN-CONTEXT-01..09 (FASE 2R.1),
gate central `IsAuthenticationContextReady` (R6/R7/R8). Resultado: **9/9 PASS**.

## Nota técnica (locator OAuth)

Los botones de proveedor son `MudIconButton` (solo ícono) dentro de `MudTooltip`; MudBlazor 7 no
expone nombre accesible (el tooltip se muestra por popover en hover). Los tests los localizan por la
clase CSS `login-provider-icon` y validan el conteo contra la respuesta de `proveedores-login`.
