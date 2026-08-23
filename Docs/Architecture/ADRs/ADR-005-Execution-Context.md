# ADR-005: Execution Context

**Status**: Aprobado
**Date**: 2026-07-28
**Deciders**: Arquitectura PassPlat
**Source**: A0 — Domain Model Review

---

## Context

PassPlat utiliza `AuthenticationContext` como objeto que transporta el contexto de ejecución actual: `IdUsuario`, `IdTenant`, `IdApp`. Este contexto se usa para:

- Emitir JWT con claims de tenant y app
- Ejecutar `SP_Permisos_Usuario_Efectivos(@IdUsuario, @IdTenant, @IdApp)`
- Crear sesiones, tokens, registros de auditoría
- Resolver políticas de password
- Filtrar consultas en repositorios

Con la identidad global (ADR-001), el `IdTenant` de `AuthenticationContext` ya no puede derivarse de `Usuario.IdTenant`. Debe provenir exclusivamente del contexto de ejecución.

## Problem

Definir la fuente de `IdTenant` en cada capa del sistema, estableciendo el principio de que **nunca se debe usar `Usuario.IdTenant` para resolver contexto de ejecución**.

## Decision

### Principio arquitectónico

```
┌──────────────────────────────────────────────┐
│ Nunca utilizar Usuario.IdTenant             │
│ para resolver contexto de ejecución.         │
│                                              │
│ La fuente de IdTenant es siempre:            │
│ 1. AuthenticationContext explícito           │
│ 2. Parámetro @IdTenant en SP                 │
│ 3. UsuarioTenant (membership del usuario)    │
└──────────────────────────────────────────────┘
```

### Fuente de IdTenant por capa

| Capa | ¿Cómo obtiene IdTenant? | ¿Qué NO debe usar? |
|------|------------------------|-------------------|
| **WebAPI (Controller)** | Desde JWT → `AuthenticationContext` | `usuario.IdTenant` |
| **AuthenticationContext** | Desde JWT claims (`TenantId`) | BD lookup |
| **Service** | Parámetro `idTenant` (recibido de controller) | `usuario.IdTenant` |
| **Repository** | Parámetro `idTenant` (recibido de service) | `usuario.IdTenant` |
| **SP** | Parámetro `@IdTenant` | `Usuarios.IdTenant` |
| **Background Service** | `UsuarioTenant` del usuario | `usuario.IdTenant` |
| **OAuth (ExternalAuthService)** | `ConfProvIden.IdTenant` (configuración) | `usuario.IdTenant` |

### Patrón: Resolución de fallback

Cuando un service recibe `idTenant` nullable y necesita un tenant por defecto:

```csharp
// ANTES (MAL):
var tenantId = idTenant ?? usuario.IdTenant;

// DESPUÉS (BIEN):
var tenantId = idTenant
    ?? await _usuarioTenantRepo.ObtenerPrincipalAsync(usuario.Id, ct)
    ?? throw new InvalidOperationException("El usuario no tiene membresía activa");
```

El fallback correcto es buscar el `UsuarioTenant` principal del usuario (el marcado como `EsTenantPrincipal = 1`), no `usuario.IdTenant`.

### SP_Permisos_Usuario_Efectivos (sin cambios estructurales)

El SP recibe `@IdTenant` como parámetro. Este parámetro:
- Siempre proviene de `AuthenticationContext`
- Nunca se deriva de `Usuarios.IdTenant`
- El SP no necesita cambiar su lógica de filtrado

El cambio interno será:
```sql
-- ACTUAL:
WHERE a.IdTenant = @IdTenant

-- FUTURO:
JOIN UsuarioTenant ut ON ut.Id = a.IdUsuarioTenant
WHERE ut.IdTenant = @IdTenant
   OR (a.IdUsuarioTenant IS NULL AND @IdTenant = ...) -- Platform Scope
```

### AuthenticationContext (sin cambios)

```csharp
public class AuthenticationContext
{
    public int IdUsuario { get; }
    public int IdTenant { get; }
    public int IdApp { get; }
}
```

`AuthenticationContext` no cambia su estructura. Siempre representa un contexto de ejecución concreto: 1 Usuario + 1 Tenant + 1 App. El JWT sigue teniendo `TenantId` e `IdApp` como claims.

Lo que cambia es **cómo se obtiene** el `IdTenant` para construir el `AuthenticationContext`:
- Antes: del usuario (`usuario.IdTenant`)
- Después: del flujo de login (tenant seleccionado explícitamente o derivado de UsuarioTenant)

## Consequences

**Positive**:
- Separación clara entre identidad, membresía y contexto
- El código no puede reintroducir accidentalmente la dependencia de `Usuario.IdTenant`
- Los SPs existentes no cambian su interfaz (siguen recibiendo `@IdTenant`)
- AuthenticationContext sigue siendo el mismo objeto

**Negative**:
- 11 servicios deben modificar el patrón `idTenant ?? usuario.IdTenant`
- Background services (PasswordExpiration) deben resolver el tenant desde UsuarioTenant
- Migración más estricta: no basta ALTER TABLE, hay que auditar cada referencia

**Riesgos**:
- Que algún servicio nuevo reintroduzca el patrón incorrecto
- Mitigación: code review + regla en .editorconfig o analyzers personalizados

## Related

- ADR-001: Global User Identity
- ADR-002: UsuarioTenant Membership
- ADR-003: Access Scope Modelo A
- A0.3 Access Matrix + Login Flow
- A0.4 Migration Strategy
