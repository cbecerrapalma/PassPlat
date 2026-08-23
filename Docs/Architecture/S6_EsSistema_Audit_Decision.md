# S6 — Auditoría de EsSistema / is_system

**Estado**: 🔵 COMPLETADA (solo lectura, sin implementación)  
**Auditoría**: 2026-07-30  
**Baseline protegido**: A1.8 24/24 · A1.9 17/17 · FASE15 9/9 · xUnit 66/66 · Build 0 errores

---

## 1. Fuente de verdad

### Existe una columna formal en la base de datos

`Usuarios.EsSistema` (`bit NOT NULL DEFAULT (0)`) está definida en el esquema (`PASSWORDS.sql:1206`), con un índice filtrado (`IX_Usuarios_EsSistema WHERE EsSistema=1`), y un trigger de validación (`TR_Usuarios_ValidarEsSistema`) que asegura:

1. Si `Usuario.EsSistema=1` → su `Tenant` debe tener `Tenant.EsSistema=1`
2. (A1) Si `Usuario.EsSistema=1` → no puede tener `UsuarioTenant` en tenants no-sistema

`Tenant.EsSistema` es un concepto paralelo con su propio índice único filtrado (`UX_Tenants_EsSistema`), que garantiza que **solo un tenant** en todo el sistema puede ser el tenant de sistema.

### El ORM ya la mapea

- `Usuario.cs:18` → `public bool EsSistema { get; set; }`
- `UsuarioConfiguration.cs:30` → `HasDefaultValue(false).IsRequired()`
- `Tenant.cs:9` → `public bool EsSistema { get; set; }` (concepto separado)

### Los SP ya la retornan

- `SP_Auth_Login` → `LoginResult.EsSistema` (SELECT `ISNULL(u.EsSistema,0)`)
- `SP_Auth_LoginExterno` → `LoginExternoResult.EsSistema`
- `SP_Usuario_Crear` → recibe `@EsSistema bit = 0` y valúa coherencia con tenant

### Los repositorios ya la proveen

- `AuthRepository.ObtenerUsuarioBasicoAsync` → SELECT projection incluye `EsSistema = u.EsSistema`
- `AuthRepository.ObtenerUsuarioPorNomAsync` → SELECT projection incluye `EsSistema = u.EsSistema`

### Los servicios ya la consumen correctamente (2/5)

- ✅ `ExternalAuthService.cs:292` → `EsSistema: result.EsSistema` (desde SP)
- ✅ `ModuloService.cs:60` → `usuario.EsSistema` desde repo (filtra módulos SYSTEM)
- ✅ `TenantService.cs:97` → `r.Value.EsSistema` (previene desactivar tenant sistema)
- ❌ **AuthService.cs** (6 puntos) → `idUsuario == 1` (ignora la columna)

---

## 2. Modelo de autorización

### No existe rol SuperAdmin en la DB

- `Roles` tabla: `Id`, `IdTenant`, `Codigo`, `Nombre`, `Descripcion`, `Activo`, `FecCrea`
- No hay columna `EsSistema` ni campo equivalente en Roles
- Los roles se crean por tenant (ADMIN, EDITOR, SUPERVISOR, CONSULTA)
- `[Authorize(Roles = "SuperAdmin")]` en `ProvIdenController` es un gate **intencionalmente bloqueante** (no existe el rol) — documentado en AGENTS.md regla 25

### `is_system` vs roles administrativos

| Concepto | Fuente | Alcance |
|----------|--------|---------|
| `EsSistema` (usuario) | `Usuarios.EsSistema` (DB) | Identidad de sistema — acceso global sin restricción por tenant |
| Roles (ADMIN, etc.) | `Roles` + `Accesos` (DB) | Permisos administrativos dentro de un tenant |
| `PLATFORM_*` roles | `Roles` con `IdTenant=NULL` | Permisos platform-scope (creados en A1) |
| `[Authorize(Roles="SuperAdmin")]` | No existe en DB | Gate contractual bloqueado |

La separación es clara: **`EsSistema` identifica al usuario-sistema** (singleton, pertenece al tenant PLATFORM). Los roles asignan permisos granulares dentro de un tenant. `EsSistema` es un bypass de seguridad completo (el `SP_Permisos_Usuario_Efectivos` retorna todos los permisos sin filtro cuando `EsSistema=1`, y `SP_Auth_Login` salta la validación de acceso a app para usuarios sistema).

---

## 3. AuthenticationContext — Mapa completo

| # | Flujo | Archivo | Línea | `EsSistema` actual | Fuente disponible correcta |
|---|-------|---------|-------|---------------------|---------------------------|
| 1 | Login (ReqCambioPwd) | `AuthService.cs` | 191 | `login.IdUsuario!.Value == 1` ❌ | `login.EsSistema` (SP result) ✅ |
| 2 | Login (MFA flow) | `AuthService.cs` | 246 | `idUsuario == 1` ❌ | `login.EsSistema` disponible en contexto |
| 3 | Refresh | `AuthService.cs` | 291 | `sesion.IdUsuario == 1` ❌ | Requiere fetch adicional (no disponible directamente) |
| 4 | GenerarAuthResponse | `AuthService.cs` | 378 | `login.IdUsuario!.Value == 1` ❌ | `login.EsSistema` (SP result) ✅ |
| 5 | PlatformLogin | `AuthService.cs` | 533 | `usuario.Id == 1` ❌ | `usuario.EsSistema` (de `ObtenerUsuarioPorNomAsync`) ✅ |
| 6 | SwitchTenant | `AuthService.cs` | 576 | `idUsuario == 1` ❌ | Requiere fetch de Usuario (o pasar desde caller) |
| 7 | **SwitchToPlatform** | `AuthService.cs` | 638 | **OMITIDO** (default `false`) 🔴 | `usuario.EsSistema` disponible |
| 8 | ExternalAuthService | `ExternalAuthService.cs` | 292 | `result.EsSistema` ✅ | Correcto |

**Hallazgo crítico**: El flujo `SwitchToPlatform` (punto 7) **no pasa `EsSistema`** en absoluto. Incluso el usuario `sistema` (Id=1, EsSistema=1) obtiene un JWT platform-scope **sin el claim `is_system`**, lo que rompe las 13 guardas de controllers que dependen de ese claim.

---

## 4. Consumidores de `is_system` — Mapa completo

| # | Controlador | Línea | Uso | ¿Obligatorio? | Impacto si `EsSistema` no se emite |
|---|-------------|-------|-----|---------------|-------------------------------------|
| 1 | UsuariosController.GetPaged | 69 | Sistema ve todos los usuarios (bypass tenant filter) | Opcional | Fallback a tenant-scoped (funciona) |
| 2 | UsuariosController.CambiarPasswordAdmin | 243 | Solo sistema cambia password de otros | **Obligatorio** | **401 ACCESO_DENEGADO** |
| 3 | UsuariosController.AgregarPasswordLocal | 271 | Sistema agrega password a cualquier usuario | Opcional | Fallback a self-only (funciona) |
| 4 | RolesController.Create | 47 | Sistema asigna IdTenant diferente | Opcional | Fallback a tenant actual |
| 5 | PoliticasPwdController.GetAll | 28 | Sistema ve todas las políticas | Opcional | Fallback a tenant-scoped |
| 6 | AccesosController.GetByUsuario | 38 | Sistema ve accesos de otros | Opcional | Fallback a permiso USUARIOS_VER |
| 7 | HistorialPwdController.GetAll | 30 | Solo sistema lista todo el historial | **Obligatorio** | **403 Forbid** |
| 8 | HistorialPwdController.GetPaged | 39 | Sistema ve todas las páginas | Opcional | Fallback a tenant-scoped |
| 9 | ConfigAppController.GetAll | 24 | Sistema ve toda la config | Opcional | Fallback a tenant-scoped |
| 10 | ConfigAppController.GetPaged (search) | 33 | Sistema busca global | Opcional | Fallback a in-memory |
| 11 | ConfigAppController.GetPaged (no search) | 42 | Sistema ve todo paginado | Opcional | Fallback a tenant-scoped |
| 12 | ~~AuditoriaPwdController.GetPaged~~ | 33 | **Código muerto** (misma rama ambos casos) | — | — |
| 13 | EmailTemplatesController.GetAll | 28 | Sistema ve todas las plantillas | Opcional | Fallback a tenant-scoped |
| — | **Program.cs** (política "SystemOnly") | 75 | `RequireClaim("is_system")` | — | **No usado** por ningún controller |

**Observaciones**:
- 2/14 consumidores son **obligatorios** (CambiarPasswordAdmin, HistorialPwd.GetAll) — sin `is_system` estas operaciones se bloquean incluso para el usuario sistema
- 1/14 es **código muerto** (AuditoriaPwdController)
- 1/14 es una **política no utilizada** (SystemOnly en Program.cs)
- Los 10 restantes son opcionales (fallback a tenant-scoped o permiso normal)

---

## 5. JWT — Generación del claim

El claim `is_system` se genera **exclusivamente** en:

**`AuthenticationTokenIssuer.cs:58`**:
```csharp
if (context.EsSistema)
    claims.Add(new Claim("is_system", "true"));
```

No hay otro punto en el pipeline JWT que añada `is_system`.  
No hay middleware que lo transforme.  
No hay handler de autorización que lo interprete de forma diferente.

**Cadena completa**:
```
DB (Usuarios.EsSistema) → SP → LoginResult.EsSistema → 
  AuthService (construye AuthenticationContext con EsSistema) → 
  AuthenticationTokenIssuer (añade claim is_system si context.EsSistema) → 
  JWT → Controller (HasClaim)
```

**El eslabón roto**: `AuthService` ignora `LoginResult.EsSistema` y usa `idUsuario == 1` en 6/7 puntos (y omite `EsSistema` en SwitchToPlatform).

---

## 6. Decisión arquitectónica

### `idUsuario == 1` es: **B) Convención histórica** → **D) Workaround / Hardcode accidental**

**Evidencia**:

| Criterio | Resultado |
|----------|-----------|
| ¿Existe columna formal en DB? | ✅ Sí — `Usuarios.EsSistema` con trigger de validación |
| ¿La columna está poblada? | ✅ User 1 tiene `EsSistema=1`; el resto tiene `EsSistema=0` |
| ¿Los SP la retornan? | ✅ `LoginResult.EsSistema` y `LoginExternoResult.EsSistema` |
| ¿Los repos la consultan? | ✅ `AuthRepository` SELECT projection |
| ¿Hay documentación contractual que fije Id=1? | ❌ No hay ningún documento que diga "sistema es siempre Id=1" |
| ¿Hay restricciones que impidan otro usuario con `EsSistema=1`? | ❌ No — el trigger solo valida coherencia con tenant. `Usuarios.EsSistema` no tiene UNIQUE index |
| ¿Hay seed que cree usuario 1 con `EsSistema=1`? | ✅ Pero es un valor data, no esquema |
| ¿Funcionaría si otro usuario tuviera `EsSistema=1`? | ❌ No — el hardcode `idUsuario == 1` le negaría el claim |

**Conclusión**: `idUsuario == 1` no es un contrato explícito ni una decisión arquitectónica. Es un **workaround funcional** que coincide con el seed actual pero **no escala** a un escenario multi-sistema (siempre que lo haya) y **no refleja** la fuente de verdad formal (`Usuarios.EsSistema`).

### Decisión

> **`EsSistema: idUsuario == 1` debe reemplazarse por el valor de `Usuario.EsSistema` proveniente de la base de datos**, utilizando las fuentes ya disponibles:
> - `LoginResult.EsSistema` (para flujos Login)
> - `LoginExternoResult.EsSistema` (para OAuth)
> - `usuario.EsSistema` (para PlatformLogin, donde el usuario ya se obtuvo del repo)
> - Una consulta adicional a `AuthRepository.ObtenerUsuarioBasicoAsync` (para Refresh, SwitchTenant y SwitchToPlatform donde no está disponible directamente)

---

## 7. Propuesta de implementación (no ejecutada)

Los siguientes cambios son necesarios, pero **no se ejecutan como parte de S6**:

### 7.1 AuthService.cs — Reemplazar `id == 1` por DB

| Línea | Actual | Propuesto |
|-------|--------|-----------|
| 191 | `EsSistema: login.IdUsuario!.Value == 1` | `EsSistema: login.EsSistema` |
| 246 | `EsSistema: idUsuario == 1` | `EsSistema: login.EsSistema` (requiere acceso a `login`) |
| 291 | `EsSistema: sesion.IdUsuario == 1` | `EsSistema: usuarioBasico.EsSistema` (fetch adicional) |
| 378 | `EsSistema: login.IdUsuario!.Value == 1` | `EsSistema: login.EsSistema` |
| 533 | `EsSistema: usuario.Id == 1` | `EsSistema: usuario.EsSistema` |
| 576 | `EsSistema: idUsuario == 1` | `EsSistema: usuarioBasico.EsSistema` (fetch adicional) |

### 7.2 AuthService.cs — Añadir `EsSistema` a SwitchToPlatform (bug fix)

| Línea | Actual | Propuesto |
|-------|--------|-----------|
| ~638 | `EsSistema` **omitido** (default false) 🔴 | `EsSistema: usuarioBasico.EsSistema` |

### 7.3 AuthService.cs — Refresh flow (punto 3)

El flujo Refresh actualmente sólo tiene `sesion.IdUsuario`. Necesita un fetch:

```csharp
var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(sesion.IdUsuario, ct);
// Si es Failure, decidir si propagar error o asumir false
EsSistema: usuarioResult.IsSuccess ? usuarioResult.Value?.EsSistema == true : false
```

### 7.4 AuthService.cs — SwitchTenant (punto 6)

Necesita un fetch similar:

```csharp
var usuarioResult = await _authRepo.ObtenerUsuarioBasicoAsync(idUsuario, ct);
EsSistema: usuarioResult.IsSuccess ? usuarioResult.Value?.EsSistema == true : false
```

### 7.5 Opcional — Program.cs

La política `SystemOnly` (`RequireClaim("is_system")`) no es usada por ningún controller. Podría mantenerse como documentación de intención o eliminarse como dead code si se prefiere.

### 7.6 Riesgos de la implementación

1. **Refresh + SwitchTenant** requieren fetch adicional a DB → impacto marginal en rendimiento (1 query más)
2. Si `AuthRepository.ObtenerUsuarioBasicoAsync` falla en Refresh, podría denegar acceso a usuario sistema legítimo → la propuesta usa fallback a `false` para no romper el flujo
3. La corrección de `SwitchToPlatform` (añadir `EsSistema`) podría exponer funcionalidad de sistema en platform-scope que antes no existía → validar con tests

---

## 8. Baseline (verificado post-auditoría)

| Suite | Resultado | Estado |
|-------|-----------|--------|
| A1.8 | 24/24 PASS | ✅ |
| A1.9 | 17/17 PASS | ✅ |
| FASE 15 | 9/9 PASS (+1 skip) | ✅ |
| xUnit | 66/66 PASS | ✅ |
| Build | 0 errores | ✅ |

Sin cambios en producción ni tests — solo consultas de solo lectura.

---

## 9. Resumen ejecutivo

| Pregunta | Respuesta |
|----------|-----------|
| ¿Existe fuente formal de `EsSistema`? | ✅ Sí — `Usuarios.EsSistema` en DB, con trigger y validación |
| ¿`idUsuario == 1` es contrato? | ❌ No — es convención histórica que funciona por coincidencia del seed |
| ¿Hay que cambiarlo? | ✅ Sí — reemplazar hardcode por DB en 6 puntos de `AuthService.cs` + añadir `EsSistema` a SwitchToPlatform (bug) |
| ¿Hay bug activo? | ✅ Sí — SwitchToPlatform no emite `is_system` para ningún usuario |
| ¿Riesgo de implementación? | Bajo — las fuentes de datos ya existen y están validadas por SPs y repos |
| Próximo paso | S7 — Blazor WASM (sprint independiente de UI). La corrección de `EsSistema` puede ejecutarse como parte de S7 o en un sprint técnico posterior. |
