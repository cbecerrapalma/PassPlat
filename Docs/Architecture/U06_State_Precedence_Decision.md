# U06 — Precedencia de Estados: Decisión Formal

## Estado: RESOLUCIÓN EN PROGRESO (documento completo, pendiente de aprobación)

---

## 1. Evidencia del Sistema Actual

### 1.1 Catálogos de Estado

| Catálogo | Códigos | Dominio |
|----------|---------|---------|
| **EstadosUsr** (EEstadoUsuario) | Activo(1), Inactivo(2), Bloqueado(3), Eliminado(4), Pendiente(5), Suspendido(6) | Identidad global |
| **EstadosMFA** (EEstadoMFA) | Activo(1), Inactivo(2), Pendiente(3), Revocado(4) | Método MFA |
| **Bloqueos (tabla)** | Activo + FecFin + TipoBloqueo (Temporal/Permanente) | Restricción temporal/permanente de cuenta |
| **Acceso.Activo** | true/false | Habilitación de rol por app |

### 1.2 SP_Auth_Login (Local Password) — Línea 1505

```
WHERE u.IdTenant = @IdTenant
  AND u.Eliminado = 0             ← Eliminado = bloqueo temprano
  AND (u.NomUsuario = @NomUsuario OR u.Email = @Email)
  AND t.Activo = 1
```

```
IF @IdEstado <> 1                 ← Inactivo(2), Bloqueado(3), Pendiente(5), Suspendido(6)
    → CuentaInactiva

IF EXISTS (Bloqueos activos)       ← Bloqueos con Activo=1 AND (EsTemporal=0 OR FecFin > now)
    → CuentaBloqueada

IF NOT EsSistema AND NOT EXISTS (Acceso WHERE Activo=1)
    → SinAccesoApp

[MFA lookup SIN IdTenant]
    → IdMFAPrincipal global
```

### 1.3 SP_Auth_LoginExterno (OAuth) — Línea 1211

```
VerificarUsuario:
IF IdEstado = 2                    ← SOLO Inactivo, NO Bloqueado/Pendiente/Suspendido
    → CuentaInactiva

IF NOT EXISTS (Acceso WHERE Activo=1 AND IdTenant)
    → SinAccesoApp

[MFA lookup SIN IdTenant]
    → IdMFAPrincipal global
```

**⚠️ Asimetría**: LoginExterno solo verifica `IdEstado=2` (Inactivo), mientras Login verifica `IdEstado<>1` (todos los no-Activo). Esto es un bug o diseño incompleto de LoginExterno.

### 1.4 PasswordExpirationBackgroundService — Línea 130

```csharp
.Where(u => !u.Eliminado && u.EmailVerificado && !string.IsNullOrWhiteSpace(u.Email))
```

**NO verifica IdEstado**. Usuario Bloqueado(3) o Inactivo(2) puede recibir notificaciones de expiración de contraseña.

### 1.5 AccesoRepository.TieneAccesoAsync

```csharp
.AnyAsync(a => a.IdUsuario == idUsuario && a.IdApp == idApp && a.Activo)
```

**NO verifica Usuario.IdEstado**. La respuesta de "tiene acceso" es independiente del estado de identidad.

---

## 2. Regla Formal de Composición

### 2.1 Dimensiones ortogonales

| Dimensión | Campo | Ámbito | Propósito |
|-----------|-------|--------|-----------|
| **Identity State** | `Usuario.IdEstado` | Global (todos los tenants + Platform) | Estado de la identidad digital del usuario, independiente de su relación con tenants |
| **Membership State** | `UsuarioTenant.IdEstado` | Por tenant | Estado de la relación usuario-tenant |
| **Membership Enabled** | `UsuarioTenant.Activo` | Por tenant | Interruptor administrativo de membresía |
| **Access Active** | `Acceso.Activo` | Por app | Interruptor de rol por aplicación |

### 2.2 Regla de composición: **Conjunción semántica**

**⚠️ NO usar `MIN(Id)`.** La prioridad no debe depender del valor numérico de la PK del catálogo. Usar **severidad semántica**:

```
EffectiveAccess =
    IdentityState == ACTIVE
    AND MembershipState == ACTIVE         // si existe UsuarioTenant
    AND MembershipEnabled == TRUE          // si existe UsuarioTenant (toggle administrativo)
    AND AccessActive == TRUE               // si existe Acceso

PlatformScope: MembershipState NO APLICA (no existe UsuarioTenant)
Evaluación: IdentityState == ACTIVE AND PlatformRole existe
```

**UsuarioTenant.Activo NO es un estado.** Es un interruptor administrativo. No tratarlo como `IdEstado`. La evaluación es explícita booleana, no por catálogo de estados.

### 2.3 Matriz de Estados Efectivos

| Identity (Usuario.IdEstado) | Membership (UT.IdEstado) | UT.Activo | Acceso.Activo | Login | Autorización | Context‑Switch |
|---|---|---|---|---|---|---|
| Activo(1) | Activo | 1 | 1 | ✅ | ✅ | ✅ |
| Activo(1) | Activo | 1 | 0 | ❌ SinAccesoApp | ❌ | ❌ |
| Activo(1) | Activo | **0** | 1 | ❌ MembershipDisabled | ❌ | ❌ |
| Activo(1) | **Inactivo** | 1 | 1 | ❌ MembershipInactive | ❌ | ❌ |
| Activo(1) | **Bloqueado** | 1 | 1 | ❌ MembershipBlocked | ❌ | ❌ |
| **Inactivo(2)** | Activo | 1 | 1 | ❌ IdentityInactive | ❌ | ❌ |
| **Bloqueado(3)** | Activo | 1 | 1 | ❌ IdentityBlocked | ❌ | ❌ |
| **Pendiente(5)** | Activo | 1 | 1 | ❌ IdentityPending | ❌ | ❌ |
| **Suspendido(6)** | Activo | 1 | 1 | ❌ IdentitySuspended | ❌ | ❌ |
| **Eliminado(4)** | — | — | — | ❌ (WHERE Eliminado=0) | ❌ | ❌ |

### 2.4 Platform Context (IdTenant = NULL)

```
PlatformState = min(
    IdentityState(Usuario.IdEstado),
    PlatformAccess(PlatformRole)
)

Donde:
- MembershipState(NO APLICA)
- PlatformAccess = EXISTS(UsuarioPlatRoles WHERE Activo=1)
```

| Identity | PlatformRole | Login | Autorización |
|---|---|---|---|
| Activo(1) | ✅ | ✅ | ✅ |
| Activo(1) | ❌ | ❌ NoAccess | ❌ |
| Inactivo(2) | — | ❌ IdentityInactive | ❌ |
| Bloqueado(3) | — | ❌ IdentityBlocked | ❌ |

---

## 3. Comportamiento por Operación

### 3.1 Login (SP_Auth_Login modificado)

```sql
-- WHERE incorpora UsuarioTenant lookup
SELECT u.Id, ut.IdEstado AS IdEstadoMembership, ut.Activo AS MembershipActivo
FROM dbo.Usuarios u
JOIN dbo.UsuarioTenant ut ON ut.IdUsuario = u.Id AND ut.IdTenant = @IdTenant
WHERE u.Eliminado = 0
  AND (u.NomUsuario = @NomUsuario OR u.Email = @Email)
  AND u.IdEstado = 1;          -- Identity Activo

IF @@ROWCOUNT = 0 → IdentityState invalid or no membership

IF ut.IdEstado <> (SELECT Id FROM EstadosUsr WHERE Codigo = 'Activo')
    → MembershipState invalid

IF ut.Activo = 0
    → MembershipDisabled

-- Bloqueos check: UsuarioTenant scope (ya no Bloqueos.IdTenant)
IF EXISTS (Bloqueos WHERE IdUsuario AND IdTenant AND Activo AND ...)
    → CuentaBloqueada

-- Acceso check (sin cambios, pero ahora usa IdUsuarioTenant)
IF NOT EXISTS (Acceso WHERE IdUsuarioTenant = ut.Id AND IdApp AND Activo)
    → SinAccesoApp
```

### 3.2 LoginExterno (SP_Auth_LoginExterno modificado)

```sql
-- Reemplazar IF IdEstado = 2 por verificación completa de composición
IF u.IdEstado <> 1
    → IdentityInactive

IF NOT EXISTS (UsuarioTenant WHERE IdUsuario AND IdTenant AND IdEstado = Activo AND Activo = 1)
    → MembershipInactive
```

### 3.3 Autorización

```csharp
// AuthorizationService.EffectiveAccessCheck
// 1. Identity check (UsuarioRepository)
var usuario = await _usuarioRepo.GetByIdAsync(idUsuario);
if (usuario.IsFailure || usuario.Value.IdEstado != (int)EEstadoUsuario.Activo)
    return AuthorizationResult.Denied("IdentityInactive");

// 2. Membership check (UsuarioTenantRepository — solo si context tenant)
if (authContext.IdTenant.HasValue)
{
    var membership = await _utRepo.ObtenerPorUsuarioTenantAsync(idUsuario, authContext.IdTenant.Value);
    if (membership.IsFailure || membership.Value.IdEstado != ... || !membership.Value.Activo)
        return AuthorizationResult.Denied("MembershipInactive");
}

// 3. Access check (AccesoRepository)
var acceso = await _accesoRepo.TieneAccesoAsync(idUsuario, idApp);
if (!acceso.IsSuccess || !acceso.Value)
    return AuthorizationResult.Denied("SinAccesoApp");
```

### 3.4 Context-Switch

```csharp
// Al cambiar de tenant, validar membership del tenant destino
// (No revalida MFA, ya validado en login original)
var membership = await _utRepo.ObtenerPorUsuarioTenantAsync(idUsuario, idTenantDestino);
if (membership.IsFailure || !membership.Value.Activo)
    return Result.Failure("MEMBERSHIP_INACTIVE", "Sin membresía activa en tenant destino");
```

### 3.5 MFA

```csharp
// MFA lookup: global por usuario (no cambia con U06)
// Validación de código: usa idTenant del contexto actual
// Membership state no afecta MFA lookup (el método principal existe independientemente)
```

### 3.6 Background Services (PasswordExpiration, etc.)

```csharp
// PasswordExpirationBackgroundService: Identity-scoped (NO imponer UsuarioTenant)
// La expiración de contraseña es un atributo de la identidad global,
// no del membership contextual. Un usuario Activo globalmente debe recibir
// notificaciones incluso si una membership particular está inactiva.
.Where(u => u.IdEstado == (int)EEstadoUsuario.Activo
    && !u.Eliminado
    && u.EmailVerificado
    && !string.IsNullOrWhiteSpace(u.Email))
```

---

## 4. Códigos de Negocio para Estados Efectivos

| Código | Significado | HTTP | Escenario |
|--------|-------------|------|-----------|
| `IDENTITY_INACTIVE` | Identidad global inactiva | 403 | Usuario.IdEstado = Inactivo(2) |
| `IDENTITY_BLOCKED` | Identidad global bloqueada | 403 | Usuario.IdEstado = Bloqueado(3) |
| `IDENTITY_PENDING` | Identidad pendiente de activación | 403 | Usuario.IdEstado = Pendiente(5) |
| `IDENTITY_SUSPENDED` | Identidad suspendida | 403 | Usuario.IdEstado = Suspendido(6) |
| `IDENTITY_DELETED` | Identidad eliminada | 404 | Usuario.Eliminado = 1 |
| `MEMBERSHIP_INACTIVE` | Membresía de tenant inactiva | 403 | UsuarioTenant.IdEstado ≠ Activo |
| `MEMBERSHIP_DISABLED` | Membresía deshabilitada por admin | 403 | UsuarioTenant.Activo = 0 |
| `MEMBERSHIP_REQUIRED` | Usuario sin membresía en tenant | 403 | Sin UsuarioTenant para tenant |

---

## 5. Impacto en A1

### 5.1 SPs modificados

| SP | Cambio | Riesgo |
|----|--------|--------|
| `SP_Auth_Login` | JOIN UsuarioTenant, composición estados | Crítico — login blocker |
| `SP_Auth_LoginExterno` | Reemplazar `IdEstado=2` por composición completa | Alto — asimetría corregida |
| `SP_Permisos_Usuario_Efectivos` | Agregar validación de UsuarioTenant | Medio |

### 5.2 Servicios C# modificados

| Servicio | Cambio | Riesgo |
|----------|--------|--------|
| `AuthService.LoginConTokenAsync` | Composición estados post-SP | Crítico |
| `AccesoRepository.TieneAccesoAsync` | Agregar validación indentity/membership | Alto |
| `PasswordExpirationBackgroundService` | Filtrar por IdEstado + membership activa | Medio |
| `MfaService` | Sin cambios | ✅ |
| `ExternalAuthService` | Manejo de MembershipInactive | Alto |

### 5.3 Nuevo servicio requerido

**AuthorizationService** (U06.4):
- `EffectiveAccessCheck(idUsuario, idTenant?, idApp)` → union de identity + membership + access
- `CheckPlatformAccess(idUsuario)` → identity + platform role
- `ValidateContextSwitch(idUsuario, idTenantOrigen, idTenantDestino)` → membership en destino

### 5.4 Tests requeridos

| Test | Combinaciones | Prioridad |
|------|---------------|-----------|
| Login — todas las combinaciones identity × membership × acceso | 36 (6×3×2) | P0 |
| LoginExterno — identity + membership | 12 (6×2) | P0 |
| Context-switch — membership válida/inválida | 4 | P0 |
| Background — filtro de estados | 6 | P1 |
| Autorización — estados efectivos | 8 combinaciones relevantes | P0 |
| Platform Scope — solo identity | 6 | P0 |

---

## 6. Decisión Formal

**U06 — RESUELTO.**

1. **Identity State** (`Usuario.IdEstado`) es la dimensión soberana: cualquier estado no-Activo bloquea toda operación en todos los ámbitos (tenants + platform).
2. **Membership State** (`UsuarioTenant.IdEstado`) y **Membership Enabled** (`UsuarioTenant.Activo`) son dimensión por-tenant. Bloquean operaciones solo dentro del tenant.
3. **Platform Context** no tiene membership: solo identity state + platform role.
4. **Acceso.Activo** es la habilitación fina por app, verificada después de identity + membership.
5. **Asimetría LoginExterno** (`IdEstado=2` en lugar de `IdEstado<>1`) debe corregirse en A1.
6. **Background Services** deben filtrar identity + membership (al menos una activa).

### 6.1 Regla de implementación para A1.1 (SQL Schema)

```sql
-- La FK compuesta de UsuarioTenant y la constraint CHECK reemplazan la lógica actual:
-- Identity State: NOT NULL, FK a EstadosUsr (existente, sin cambios)
-- Membership State: UsuarioTenant.IdEstado NOT NULL, FK a EstadosUsr
-- Membership Enabled: UsuarioTenant.Activo NOT NULL, DEFAULT 1 (toggle, no estado)

-- SP_Auth_Login: JOIN a UsuarioTenant y conjunción semántica:
--   u.IdEstado = (SELECT Id FROM EstadosUsr WHERE Codigo = 'Activo')
--   AND ut.IdEstado = (SELECT Id FROM EstadosUsr WHERE Codigo = 'Activo')
--   AND ut.Activo = 1

-- SP_Auth_LoginExterno: misma regla (corrige asimetría IdEstado=2)
```

### 6.2 No bloquea A1.1

La composición de estados no requiere cambios DDL adicionales más allá de la creación de `UsuarioTenant` (ya planificada en A1.1). Las reglas se implementan en SPs (A1.4) y servicios C# (A1.5).
