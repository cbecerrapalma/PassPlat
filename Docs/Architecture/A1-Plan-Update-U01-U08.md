# A1 Implementation Plan — Correcciones U01–U08

**Date**: 2026-07-28  
**Parent**: `A1-Implementation-Plan.md`  
**Purpose**: Reflejar decisiones U01–U08 en el plan de ejecución. No reemplaza el plan original — solo corrige puntos específicos.

---

## 1. U01 — MFA.IdTenant KEEP (EXECUTION CONTEXT)

### Corrección al plan

| Item | Antes | Después |
|------|-------|---------|
| U01 task | A1.2-006 (solo validar, no migrar) | **KEEP**. Sin migración. No bloquea A1.1 |
| SP_MFA_Validar | Sin cambios | Soporte `@IdTenant=NULL` para Platform Context (P1, post-A1.1) |
| Dependency | Bloquea Contract | No bloquea Contract. Pre-Contract solo validación documental |

### Sin cambios en A1.1 schema — MFA table no se modifica.

---

## 2. U06 — State Precedence (RESUELTO)

### Reemplazar sección 7.7 con:

```text
Authorization = AND(
    IdentityState  == ACTIVE,          // Usuario.IdEstado (global)
    MembershipState == ACTIVE,         // UsuarioTenant.IdEstado
    MembershipEnabled == TRUE,         // UsuarioTenant.Activo (toggle admin)
    AccessActive == TRUE               // Acceso.Activo (per-app)
)
```

### Reglas:
- **NO usar `MIN(Id)`** — precedencia es semántica (AND), no numérica
- **`UsuarioTenant.Activo`** es toggle administrativo, no estado de catálogo `EstadosUsr`
- **Platform Context**: solo `IdentityState == ACTIVE` + `PlatformRole` — sin membership
- **Background services** (PasswordExpiration, Email, TokenRest): identity-scoped globales. No imponer UsuarioTenant sin justificación

### Impacto en código:
- `AuthService.LoginConTokenAsync`: validar los 4 factores después de password OK
- `AccesoRepository.AsignarAccesoAsync`: validar `IdentityState` + `UsuarioTenant` antes de insertar
- `PasswordExpirationBackgroundService`: filtrar `usuario.IdEstado = 1` (sin membership check)

---

## 3. U03 — Platform Scope Seed (REUSE Acceso)

### Corrección al plan

| Item | Antes | Después |
|------|-------|---------|
| Tabla nueva para Platform Scope | No mencionado | **NO** crear tabla nueva. REUSE Acceso con `IdUsuarioTenant=NULL` |
| Roles PLATFORM_* | Solo mencionados como seed | Preservar IDs fijos: 1=PLATFORM_ADMIN, 2=_EDITOR, 3=_SUPERVISOR, 4=_CONSULTA (IdTenant=NULL) |
| Seed 07_Usuarios.sql | Sin detalles | Accesos platform → `IdUsuarioTenant=NULL` en vez de `IdTenant=PLATFORM` |

### A1 tasks afectadas:
- **A1.1-005**: Acceso.IdUsuarioTenant NULLable (ya planificado). Confirmar que FK no es requerida para NULLs
- **A1.7 U04 UX tenant selector**: Diferido post-A1

---

## 4. U07 — Índices (13 nuevos, 4 eliminados, 3 conservados)

### Reemplazar sección 3.1.4 con:

| Tabla | Índices nuevos | Eliminados | Conservados |
|-------|----------------|------------|-------------|
| UsuarioTenant | `PK_UT` (clustered), `UX_UT_Usuario_Tenant` (UNIQUE), **`UX_UT_Id_IdUsuario`** (UNIQUE para FK compuesta), `IX_UT_Usuario`, `IX_UT_Tenant_Estado`, `IX_UT_Estado` (6) | — | — |
| Acceso | `IX_Accesos_UsuarioTenant`, `IX_Accesos_UT_App`, `IX_Accesos_Platform_App`, `UX_Accesos_Tenant_UsrAppRol`, `UX_Accesos_Platform_UsrAppRol` (5) | `IX_Accesos_Tenant`, `IX_Accesos_UsuarioTenantAppActivo` (2) | `IX_Accesos_Usuario`, `IX_Accesos_App`, `IX_Accesos_Rol` (3) |
| Usuario | `UX_Usuarios_NomUsuario`, `UX_Usuarios_Email` (2) | `UX_Usuarios_Tenant_NomUsuario`, `UX_Usuarios_TenantEmail` (2) | — |
| **Total** | **13** | **4** | **3** |

### Scripts UP/DOWN detallados en `U07_Index_Design_Decision.md`

---

## 5. U08 — Triggers (CRÍTICO — corrección)

### ⚠️ Corrección importante al plan original

| Trigger | Plan original (A1-Implementation-Plan.md) | **Corrección U08** |
|---------|------------------------------------------|--------------------|
| `TR_Accesos_ValidarTenant` | ELIMINAR | ✅ **ELIMINAR** (FK compuesta lo reemplaza) |
| `TR_UsuariosPermisos_ValidarTenant` | ELIMINAR | ❌ **REESCRIBIR** (NO eliminar) — `UP.IdTenant` se conserva como EXECUTION CONTEXT |
| `TR_GruposUsuarios_ValidarTenant` | REESCRIBIR | ✅ **REESCRIBIR** contra UsuarioTenant |
| `TR_Usuarios_ValidarEsSistema` | REESCRIBIR contra UsuarioTenant | ✅ Confirmado |
| `TR_Usuarios_Mod` | MODIFICAR — quitar `OR UPDATE(IdTenant)` | ✅ Confirmado |

### ¿Por qué TR_UsuariosPermisos_ValidarTenant NO se elimina?

La tabla `UsuariosPermisos` **conserva `IdTenant`** (es EXECUTION CONTEXT, mismo principio que MFA.IdTenant en U01). Como `IdTenant` permanece como columna, el trigger que valida coherencia usuario-tenant sigue siendo necesario. La diferencia es que ahora valida contra `UsuarioTenant` en lugar de `Usuarios.IdTenant`.

### Nota: FK compuesta para Accesos

`TR_Accesos_ValidarTenant` **sí puede eliminarse** porque la FK es **compuesta**: `(IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)`. Esto garantiza que:
- Existe el `UsuarioTenant` referenciado
- `Acceso.IdUsuario` coincide con `UsuarioTenant.IdUsuario` para ese registro

Una FK simple `(IdUsuarioTenant) → UsuarioTenant(Id)` **no bastaría** porque permitiría que `Acceso.IdUsuario` apunte a un usuario distinto del dueño del `UsuarioTenant`. La compuesta es necesaria.

### Nuevo trigger UP:

```sql
CREATE OR ALTER TRIGGER dbo.TR_UsuariosPermisos_ValidarTenant
ON dbo.UsuariosPermisos
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.UsuarioTenant ut
            WHERE ut.IdUsuario = i.IdUsuario
            AND ut.IdTenant = i.IdTenant
        )
    )
    BEGIN
        RAISERROR('El tenant del permiso directo debe coincidir con una membresía del usuario.', 16, 1);
        ROLLBACK; RETURN;
    END
END;
```

---

## 6. GruposUsuarios — SIN CAMBIOS ESTRUCTURALES

| Elemento | Decisión |
|----------|----------|
| `GruposUsuarios.IdUsuario` | **KEEP** (no agregar `IdUsuarioTenant`) |
| `TR_GruposUsuarios_ValidarTenant` | Reescribir contra `UsuarioTenant` |
| Índices | **Sin cambios** (`IX_GruposUsuarios_Grupo`, `IX_GruposUsuarios_Usuario`) |

---

## 7. Resumen: estado de U01–U08 post-resolución

| ID | Estado | Bloquea A1.1? | Corrección al plan |
|----|--------|-------------|-------------------|
| U01 | ✅ RESUELTO | No | SP_MFA_Validar soportar @IdTenant=NULL (P1) |
| U06 | ✅ RESUELTO | No | Sección 7.7 reemplazada con reglas semánticas |
| U03 | ✅ RESUELTO | No | Platform Scope: REUSE Acceso, no tabla nueva |
| U07 | ✅ RESUELTO | No | 13 índices nuevos, 4 eliminados (detalle en U07 doc) |
| U08 | ✅ RESUELTO | No | **TR_UP_ValidarTenant REESCRIBIR** (corrección crítica) |

**Todas las resoluciones verificadas contra el plan. A1.1 (SQL Schema) puede comenzar sin bloqueos.**
