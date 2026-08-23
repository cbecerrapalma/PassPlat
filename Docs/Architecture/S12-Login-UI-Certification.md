# S12 FASE 2.1 + 2R + 2R.1 — Certificación UI Login (7275)

## Estado
**CERTIFICADA** — 2026-08-02 (FASE 2.1) / 2026-08-02 (FASE 2R) / 2026-08-02 (FASE 2R.1)

## Resumen ejecutivo

El contrato UI del login en `https://localhost:7275` quedó corregido y certificado. Se eliminó el
selector de método (Contraseña/OAuth) y ahora el formulario Usuario/Contraseña y los proveedores
OAuth se muestran simultáneamente tras resolver App+Tenant, con proveedores dinámicos por tenant.

**FASE 2R**: se implementó la herencia configurable de proveedores de la plataforma. Los tenants sin
proveedores propios válidos (ABARROTES/VESTUARIO) ahora muestran el GOOGLE de la plataforma
(`EsDePlataforma=true`) cuando el campo configurable `ConfigApp[OAuth:MostrarProveedoresPlataforma]`
es `true`. Con `false`, quedan sin proveedores (`[]`).

## Evidencia de login real (P6)

Login desde la UI real en `https://localhost:7275/login` con **test_multitenant / tenant 3 (Abarrotes del Sur)**.

### Request `POST http://localhost:5000/api/auth/login`
```json
{"nomUsuario":"test_multitenant","email":null,"idApp":1,"password":"Admin@123",
 "idDisp":null,"idIP":null,"idAgente":null,"idTenant":3}
```

### Response (200)
- `idUsuario: 8`, `idTenant: 3`, `nomUsuario: test_multitenant`
- `reqCambioPwd: false`, `idMFAPrincipal: null`
- `accessToken`: JWT HS256
- `refreshToken` presente

### Claims del JWT (decodificado)
```
nameidentifier = 8
IdApp          = 1
TenantId       = 3
UsuarioTenantId = 4
permiso        = [ACCESOS_VER, GRUPOS_VER, MATRIZ_PERMISOS_VER, PERMISOS_VER,
                  ROLES_VER, USUARIOS_VER, USUARIOS_VERDISP]
iss            = PassPlat
aud            = PassPlat
```

### Cadena BBDD → SP → JWT → UI
`EXEC SP_Permisos_Usuario_Efectivos @IdUsuario=8, @IdTenant=3, @IdApp=1` devuelve exactamente los
mismos 7 permisos que los claims del JWT → la cadena completa queda certificada sin alterar backend.

### Red del flujo (registrada en DevTools)
| Request | Estado |
|---------|--------|
| `GET /api/apps/activas` | 200 |
| `GET /api/auth/tenant-info` | 200 |
| `GET /api/auth/tenants` | 200 |
| `GET /api/auth/externo/proveedores-login?idTenant=3` | 200 → `[GOOGLE (esDePlataforma=true)]` (FASE 2R) |
| `POST /api/auth/login` | 200 → JWT |
| `GET /api/auth/current-tenant` | 200 |
| Dashboard: endpoints sin permiso (`contar-tenant`, `Apps/count`, `Tenants/count`, `AuditoriaPwd`) | 403 esperados |

### Dashboard tras login
`https://localhost:7275/` → "Bienvenido, test_multitenant", tenant "Abarrotes del Sur",
navegación respetando los 7 permisos (Roles y Permisos, Usuarios, Accesos visibles; IAM Permisos
colapsado sin permiso de escritura).

## FASE 2R.1 — Login Context Gate

### Defecto estructural (detectado por el usuario tras la 2R)

La UI exigía Tenant pero **no exigía App explícitamente** como precondición de autenticación. El gate
del login era `!_requiereSeleccionApp && !_requiereSeleccionTenant` (banderas basadas en el conteo de
apps), no una condición central de contexto. Consecuencia:

- `AuthService.AppId { get; set; } = 1` — **default 1 hardcodeado** (`AuthService.cs:42`).
- En `Login.razor` `OnInitializedAsync`: con `_apps.Count == 0` se hacía `_selectedAppId = Auth.AppId`
  (1) sin validar una app real; con `Count == 1` la auto-resolución sí producía `IdApp > 0` real.
- Escenario teórico de violación del contrato: `_apps.Count == 0` (o fallo de `GetAppsAsync`) +
  tenant resuelto → form/Google visibles con `AppId=0` (o `1` ficticio) → `POST /api/auth/login` con
  contexto inválido.
- En el entorno real (1 app PASSPLAT id=1, 3 tenants) el flujo funcionaba porque la auto-resolución
  era correcta (`IdApp=1` real), pero el gate estructural quedaba roto.

### Causa raíz

1. `AuthService.cs:42` — `AppId` con default `1` (app asumida sin fuente de datos).
2. `Login.razor:381-395` — `OnInitializedAsync` con `Count==0` heredaba `Auth.AppId` (1) sin validar.
3. Gate de render form/botón/OAuth = `!_requiereSeleccionApp && !_requiereSeleccionTenant`
   (depende del conteo, no de la validez del contexto App+Tenant).
4. `DoLogin` / `IniciarConProveedorAsync` no validaban `Auth.AppId > 0`.

### Corrección implementada

`D:\CODIGOS\PassPlat\PassPlat.Web\Pages\Login.razor`:

1. **Gate central único**: `IsAuthenticationContextReady => Auth.AppId > 0 && TenantIdContexto > 0`
   (con `TenantIdContexto => _resolvedTenantId ?? (_selectedTenantId > 0 ? _selectedTenantId : 0)`).
   Aplicado a las 3 condiciones de render (form, botón INICIAR SESIÓN, proveedores OAuth).
2. **`OnInitializedAsync`**:
   - `Count == 0` → `Auth.AppId = 0`, sin auto-asumir, mensaje "No hay aplicaciones disponibles".
   - `Count == 1` → auto-resolución con `IdApp` real (`_apps[0].Id > 0`).
   - `Count > 1` → selector de App (`Auth.AppId = 0` hasta `ContinuarConApp`).
3. **`ContinuarConApp`**: guard `_selectedAppId <= 0 → return`.
4. **`ContinuarConTenant`**: guard `Auth.AppId <= 0 → return` (App precede a Tenant).
5. **`DoLogin`**: guard `!IsAuthenticationContextReady → return` (sin llamada a `POST /api/auth/login`).
6. **`CargarProveedoresAsync`**: guard `Auth.AppId <= 0 → return`.
7. **`IniciarConProveedorAsync`**: guard `!IsAuthenticationContextReady → return` (OAuth bloqueado).

No se modificó: OAuth 2R (`ExternalLoginProviderService`, `EsDePlataforma`, toggle ConfigApp),
`ExternalProviderValidator`, `AuthenticationTokenIssuer`, `PermissionClaimBuilder`,
`AuthenticationContext`, `SessionManager`, SPs.

### Antes / Después

| Estado | Antes (2R) | Después (2R.1) |
|--------|-----------|----------------|
| Sin App (`Count==0`) | `Auth.AppId=1` (ficticio) → auth posible | `Auth.AppId=0` → bloqueado, mensaje "No hay aplicaciones" |
| App sin Tenant | form/Google visibles si `requiereSeleccionTenant=false` | gate `IsAuthenticationContextReady=false` → sin form/Google |
| App+Tenant | form + OAuth visibles | form + OAuth visibles (igual) |
| `POST /api/auth/login` sin contexto | podía dispararse | bloqueado en `DoLogin` antes del request |
| OAuth sin contexto | botones podían existir | bloqueado en `IniciarConProveedorAsync` |

### Verificación manual en navegador real (7275)

1. `/login` → App auto-resuelta (id=1 real) + selector Tenant, botón Continuar deshabilitado.
   **Sin** form, **sin** botón INICIAR SESIÓN, **sin** proveedores OAuth (gate bloquea).
2. Seleccionar "Abarrotes del Sur" → Continuar → form (Usuario/Contraseña/Recordarme/INICIAR SESIÓN)
   + "O continúa con" + GOOGLE heredado simultáneos.
3. Seleccionar "Plataforma" → form + GOOGLE propio simultáneos.
4. Login real `test_multitenant` / ABARROTES → Dashboard "Bienvenido, test_multitenant" con tenant
   Abarrotes del Sur y navegación según los 7 permisos.
5. Login real en tenant Plataforma con `test_multitenant` → "Credenciales invalidas" (aislamiento de
   tenants, usuario pertenece a ABARROTES).

### Suite LOGIN-CONTEXT (nueva)

`tests/s12-login-context-gate.spec.ts` — 9 tests seriales:

| # | Test | Validación |
|---|------|-----------|
| LOGIN-CONTEXT-01 | Sin App: form y OAuth bloqueados, AppId real no asumida | gate App |
| LOGIN-CONTEXT-02 | Rechazo Tenant sin App: selector no permite auth | gate Tenant |
| LOGIN-CONTEXT-03 | App sin Tenant: auth bloqueada (ni form ni Google) | gate App+Tenant |
| LOGIN-CONTEXT-04 | App+Tenant: auth habilitada (form completo) | R2 |
| LOGIN-CONTEXT-05 | App+Tenant+OAuth: GOOGLE visible con form | R3 |
| LOGIN-CONTEXT-06 | Form+OAuth simultáneos sin selector de método | R1 |
| LOGIN-CONTEXT-07 | Login interno sin contexto: bloqueado antes de POST `/api/auth/login` | gate backend UI |
| LOGIN-CONTEXT-08 | OAuth sin contexto: bloqueado (sin botones de proveedor) | gate OAuth |
| LOGIN-CONTEXT-09 | Login completo: JWT con IdApp/IdTenant/IdUsuarioTenant/permisos | JWT |

Resultado: **9/9 PASS**.

## Resultado de tests

### Nueva suite de contrato UI
```
tests/s12-login-ui-contract.spec.ts — 9/9 PASS (1.0m)
  #8 Login real 7275: form + GOOGLE heredado para ABARROTES (antes esperaba mensaje vacío)
  #9 FASE 2R: herencia vía API (t1 false, t3/t4 true)
tests/s12-login-context-gate.spec.ts — 9/9 PASS (53.5s) [FASE 2R.1]
```
### Toggle OFF verificado (flag `false` en BBDD)

- `ConfigApp[OAuth:MostrarProveedoresPlataforma] = false` → tenant 3 devuelve `[]` (sin herencia).
- Tenant 1 conserva su GOOGLE propio (`esDePlataforma=false`).
- Flag restaurado a `true` (valor por defecto).

### Verificado en navegador real (7275)

Seleccionando "Abarrotes del Sur" (t3): formulario Usuario/Contraseña + "O continúa con" + botón
GOOGLE simultáneos — antes de la 2R mostraba "No hay proveedores externos disponibles para este tenant."

## Resultado de tests

### Nueva suite de contrato UI
```
tests/s12-login-ui-contract.spec.ts — 9/9 PASS (1.0m)
  #8 Login real 7275: form + GOOGLE heredado para ABARROTES (antes esperaba mensaje vacío)
  #9 FASE 2R: herencia vía API (t1 false, t3/t4 true)
```

### Regresión completa (P9)

| Suite | Resultado |
|-------|-----------|
| Build `PassPlat.slnx` | 0 errores |
| xUnit `PassPlat.Aplicacion.Test` | 66/66 |
| `s12-login-e2e.spec.ts` (P0 legacy) | 12/12 |
| `s12-login-ui-contract.spec.ts` (FASE 2R) | 9/9 |
| `s12-login-context-gate.spec.ts` (FASE 2R.1) | 9/9 |
| `faseA18-multitenant-gate.spec.ts` | 24/24 |
| `faseA19-switch-to-platform.spec.ts` | 17/17 |
| `fase12-federacion-ui.spec.ts` | 23 passed + 2 skip pre-existentes |
| `fase14-federacion-identidades.spec.ts` | 14/14 |
| Gates combinados (A1.8+A1.9) | 41/41 |
| Login UI contract + e2e (P6 2R.1) | 21/21 |

## Método de certificación

- P0: diagnóstico de puertos/proyectos (7275 == 5273, mismo proceso WASM PID 13492).
- P1: auditoría de `Login.razor` → causa raíz: selector de método.
- P2–P5: corrección UI (eliminación del selector, form+OAuth siempre visibles, estados de carga/vacío).
- P6: login real en 7275 + verificación JWT/claims + red.
- P7: suite `s12-login-ui-contract.spec.ts` (9 tests en FASE 2R).
- P8: reporte de entorno.
- P9: regresión completa.
- P10: documentación (este documento + `S12-Login-UI-Contract.md` + `S12-Environment-Profile.md`).
- **2R**: decisión del usuario (campo configurable) → migración/seed ConfigApp → reescritura de
  `ExternalLoginProviderService` → test #9 API + actualización del #8 → verificación BBDD/API/navegador.
- **2R.1**: auditoría de `Login.razor`/`AuthService.cs` (defecto App no obligatoria) → gate central
  `IsAuthenticationContextReady` → guards en DoLogin/OAuth/Continuar* → suite `s12-login-context-gate.spec.ts`
  (9/9) → regresión completa → verificación manual en 7275 (estados A/B/C + login real).

## Archivos

| Archivo | Rol |
|---------|-----|
| `PassPlat.Web\Pages\Login.razor` | Corregido (selector eliminado, form+OAuth simultáneos, gate `IsAuthenticationContextReady` 2R.1) |
| `PassPlat.Web\Services\AuthService.cs` | `AppId` default 1 auditado (2R.1: Login.razor lo sobreescribe desde datos reales) |
| `tests\s12-login-ui-contract.spec.ts` | 9 tests de contrato (7275) incl. herencia 2R |
| `tests\s12-login-context-gate.spec.ts` | 9 tests LOGIN-CONTEXT-01..09 (2R.1) |
| `PassPlat.Aplicacion\Services\OAuth\ExternalLoginProviderService.cs` | Reescrito: herencia configurable de plataforma |
| `PassPlat.Aplicacion.Dtos\Core\ExternalLoginProviderDto.cs` | Añadido `EsDePlataforma` |
| `PassPlat.Dominio\Constants\TenantCodes.cs` | Nuevo: `PLATFORM` |
| `PassPlat.Dominio\Constants\ConfigAppKeys.cs` | Nuevo: `GrupoOAuth`, `MostrarProveedoresPlataforma` |
| `Migrations\FASE2R_MostrarProveedoresPlataforma.sql` | Migración idempotente (aplicada) |
| `BBDD\Seed\Configuracion\04_Infraestructura.sql` | Clave ConfigApp 2R en seed (id 9) |
| `Docs\Architecture\S12-Login-UI-Contract.md` | Contrato + diagnóstico + causa raíz + 2R + 2R.1 |
| `Docs\Architecture\S12-Environment-Profile.md` | Reporte de entorno |
