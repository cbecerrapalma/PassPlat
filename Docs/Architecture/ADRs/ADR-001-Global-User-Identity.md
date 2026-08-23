# ADR-001: Global User Identity

**Status**: Aprobado
**Date**: 2026-07-28
**Deciders**: Arquitectura PassPlat
**Source**: A0 — Domain Model Review

---

## Context

PassPlat actualmente modela `Usuario` con `IdTenant int NOT NULL`, lo que obliga a que cada usuario pertenezca exclusivamente a un solo tenant. Este diseño impide:

- Que un mismo usuario (persona física) acceda a múltiples tenants sin crear cuentas duplicadas
- Que existan administradores globales (platform admins) sin membresía en cada tenant
- La reutilización de identidades OAuth (Google, GitHub) entre tenants

## Problem

Un usuario no debe estar inherentemente ligado a un tenant. La pertenencia a un tenant es un concepto distinto de la identidad de la persona. Mezclar ambos en una sola columna (`Usuario.IdTenant`) crea las siguientes restricciones:

- `TR_Accesos_ValidarTenant` fuerza que todos los Accesos de un usuario estén en el mismo tenant que `Usuario.IdTenant`
- `TR_UsuariosPermisos_ValidarTenant` fuerza lo mismo para permisos directos
- `TR_GruposUsuarios_ValidarTenant` fuerza lo mismo para grupos
- El login con OAuth (`SP_Auth_LoginExterno`) crea usuarios dentro de un tenant específico, impidiendo que la misma identidad externa se use en múltiples tenants
- La búsqueda de usuarios (`UsuarioRepository`) siempre filtra por `IdTenant`, incluso cuando el contexto no debería requerirlo

## Options Considered

### Option 1: Mantener IdTenant en Usuario

No cambiar el modelo actual. Agregar tablas adicionales para multi-tenant.

- **Pro**: Sin migración
- **Con**: No resuelve el problema arquitectónico. Los triggers de validación seguirán bloqueando identidad global. Solución parchada.

### Option 2: Usuario.IdTenant nullable (solución intermedia)

Hacer `IdTenant int NULL` y usar `UsuarioTenant` como nueva tabla de membresía.

- **Pro**: Migración más simple (un ALTER COLUMN)
- **Con**: Deja una columna huérfana que puede seguir siendo usada como fallback por código legacy. El principio de "nunca usar Usuario.IdTenant" sería difícil de enforcear. Riesgo de que nuevo código reintroduzca la dependencia.

### Option 3: Eliminar Usuario.IdTenant completamente (modelo definitivo)

`Usuario` deja de tener `IdTenant`. La membresía al tenant se modela exclusivamente mediante `UsuarioTenant`.

- **Pro**: Modelo semánticamente puro. Sin posibilidad de reintroducir la dependencia. Forza a todo el código a usar el nuevo modelo.
- **Con**: Migración más compleja (DROP column + recrear FKs e índices). Mayor esfuerzo inicial.

## Decision

**Elegido: Option 3** — Eliminar `Usuario.IdTenant` completamente.

El objetivo final es `Usuario` sin `IdTenant`. No se adoptará una solución intermedia con `IdTenant` nullable, porque esto dejaría abierta la posibilidad de reintroducir la dependencia.

La migración se realizará en fases (ver ADR-002 y A0.4 Migration Strategy), donde:
1. Primero se crea `UsuarioTenant` con los datos actuales de `Usuario.IdTenant`
2. Luego se migran las dependencias (Acceso, UsuarioPermiso, servicios)
3. Finalmente se elimina la columna `Usuario.IdTenant`

## Consequences

**Positive**:
- Modelo de identidad puro: `Usuario` = persona, `UsuarioTenant` = membresía
- Un usuario puede pertenecer a 0, 1 o N tenants sin duplicación
- Los administradores globales no requieren membresía en cada tenant
- Las identidades OAuth se vinculan al usuario, no al tenant
- Eliminación de 3 triggers de validación (TR_Accesos_ValidarTenant, TR_UsuariosPermisos_ValidarTenant, TR_GruposUsuarios_ValidarTenant)

**Negative**:
- Migración transversal (~50 objetos afectados entre C# y SQL)
- Todo el código que usa `usuario.IdTenant` debe reescribirse para usar `UsuarioTenant`
- El patrón `idTenant ?? usuario.IdTenant` (11 ocurrencias) debe migrarse

**Risks**:
- Regresión en consultas que asumen `Usuario.IdTenant` como fuente de verdad
- Performance: las queries de autorización ahora requieren JOIN a UsuarioTenant
- Mitigación: índices cubrientes en `UsuarioTenant(IdUsuario, IdTenant)` y pruebas de performance

## Related

- ADR-002: UsuarioTenant Membership
- ADR-003: Access Scope Modelo A
- ADR-005: Execution Context
- A0.4 Migration Strategy
