# ADR-004: Platform Scope

**Status**: Aprobado
**Date**: 2026-07-28
**Deciders**: Arquitectura PassPlat
**Source**: A0 — Domain Model Review, A0.2 Semantic Dependency Audit

---

## Context

La arquitectura PassPlat debe soportar usuarios que tengan autorización a nivel de plataforma, sin necesidad de pertenecer a un tenant específico. Ejemplos:

- Administradores globales que gestionan la plataforma IAM
- Usuarios de soporte que necesitan acceso a múltiples tenants
- Usuarios sistema (procesos batch, background jobs)
- Consultores que solo necesitan acceso a PASSPLAT (App de administración)

## Problem

Necesitamos un mecanismo para representar autorización que no dependa de `UsuarioTenant` (membresía). Este mecanismo debe:

- Convivir con el Tenant Scope en la misma tabla `Acceso`
- Tener reglas de integridad que impidan combinaciones inválidas
- Ser distinguible en consultas para filtrado correcto
- Soportar roles globales vs roles por tenant

## Decision

### Representación

Platform Scope se representa como:

```
Acceso.IdUsuarioTenant IS NULL
    → Platform Scope
    → Rol.IdTenant IS NULL (rol global)
```

Tenant Scope se representa como:

```
Acceso.IdUsuarioTenant IS NOT NULL
    → Tenant Scope
    → Rol.IdTenant = UsuarioTenant.IdTenant (rol del tenant)
```

### Reglas de integridad

| Combinación | ¿Válido? | Explicación |
|-------------|----------|-------------|
| `IdUsuarioTenant=NULL` + `Rol.IdTenant=NULL` | ✅ | Platform Scope con rol global |
| `IdUsuarioTenant=NULL` + `Rol.IdTenant=TA` | ❌ | Rol de tenant requiere UsuarioTenant |
| `IdUsuarioTenant=UT1` + `Rol.IdTenant=NULL` | ❌ | Rol global no puede combinarse con Tenant Scope |
| `IdUsuarioTenant=UT1` + `Rol.IdTenant=UT1.IdTenant` | ✅ | Tenant Scope correcto |
| `IdUsuarioTenant=UT1` + `Rol.IdTenant=TB` (≠ UT1.IdTenant) | ❌ | Rol de otro tenant |

Estas reglas se garantizan mediante:
- **FK compuesta** `(IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)` — valida UsuarioTenant existe y pertenece al usuario correcto
- **Lógica de aplicación** en `AccesoService.AsignarAccesoAsync` — valida coherencia entre scope y rol
- **SP_Permisos_Usuario_Efectivos** — filtra `(r.IdTenant = @IdTenant OR r.IdTenant IS NULL)` solo para Tenant Scope
- **No triggers**: las validaciones se hacen en la capa de aplicación, no en BD

### Roles globales

Los roles globales se definen con `Rol.IdTenant IS NULL`:

| Código | Nombre | Descripción |
|--------|--------|-------------|
| `PLATFORM_ADMIN` | Administrador de plataforma | Acceso total a todas las apps |
| `PLATFORM_EDITOR` | Editor de plataforma | Puede gestionar configuración |
| `PLATFORM_SUPERVISOR` | Supervisor de plataforma | Solo lectura en toda la plataforma |
| `PLATFORM_CONSULTA` | Consulta de plataforma | Visibilidad mínima del sistema |

### Usuario sistema (EsSistema=1)

El usuario sistema tendrá:
1. `UsuarioTenant` al tenant PLATFORM (tenant con `EsSistema=1`)
2. `Acceso` Platform Scope a PASSPLAT con `PLATFORM_ADMIN`
3. El trigger `TR_Usuarios_ValidarEsSistema` se reescribe para:
   - Validar que exista `UsuarioTenant` con `IdTenant` del tenant sistema
   - Validar que `EsSistema=1` solo usuarios que tengan esa membresía
4. `SP_Permisos_Usuario_Efectivos` bypass (`EsSistema=1` → todos los permisos) se mantiene sin cambios

## Consequences

**Positive**:
- Modelo unificado de autorización (una tabla `Acceso` para ambos scopes)
- Roles globales y por tenant conviven con reglas claras
- Sin triggers de validación
- El usuario sistema no requiere excepciones arquitectónicas

**Negative**:
- La lógica de validación de coherencia scope/rol está en la capa de aplicación
- SP_Permisos_Usuario_Efectivos necesita JOIN a UsuarioTenant para filtrar correctamente

## Related

- ADR-002: UsuarioTenant Membership
- ADR-003: Access Scope Modelo A
- A0.3 Access Matrix + Login Flow
