# ADR-002: UsuarioTenant Membership

**Status**: Aprobado
**Date**: 2026-07-28
**Deciders**: Arquitectura PassPlat
**Source**: A0 — Domain Model Review

---

## Context

Con la identidad global (ADR-001), la pertenencia de un usuario a un tenant debe modelarse mediante una entidad separada. Actualmente esta pertenencia está implícita en `Usuario.IdTenant`, sin metadata adicional como:

- Fecha de ingreso al tenant
- Estado de la membresía (activo, suspendido, pendiente, expirado)
- Si es el tenant principal del usuario
- Quién invitó al usuario
- Origen de la membresía (invitación, auto-provision, admin, seed)
- Fecha de expiración de la membresía

## Problem

Se necesita una entidad que represente la membresía de un usuario en un tenant, con capacidad de:

- Soportar 0..N tenants por usuario
- Soportar suspensión/expiración por tenant (no global)
- Servir como ancla para Accesos Tenant Scope
- Servir como fuente única de `IdTenant` para autorización
- Mantener historia de membresía

## Options Considered

### Option 1: Tabla separada UsuarioTenant

Nueva tabla con FK a `Usuario` y `Tenant`, metadata de membresía, y `UNIQUE(IdUsuario, IdTenant)`.

### Option 2: Embedded en Acceso

Derivar el tenant exclusivamente del `Acceso`, sin entidad de membresía explícita.

- **Con**: No hay membresía sin autorización. Un usuario podría tener Accesos sin estar explícitamente en el tenant. No hay forma de suspender membresía sin revocar Accesos.

### Option 3: JSON column en Usuario

Almacenar membresías como JSON en una columna de Usuario.

- **Con**: No relacional. Sin integridad referencial. Sin índices. Sin FKs.

## Decision

**Elegido: Option 1** — Tabla separada `UsuarioTenant`.

```
UsuarioTenant
├── Id              int (PK)
├── IdUsuario       int (FK → Usuario)
├── IdTenant        int (FK → Tenant)
├── IdEstado        int (FK → EstadosUsr): Activo, Inactivo, Suspendido, Pendiente
├── EsTenantPrincipal bit (default 1)
├── FechaIngreso    datetime2
├── FechaFin        datetime2?
├── InvitadoPor     int? (FK → Usuario)
├── Origen          string?: 'invitacion', 'auto-provision', 'admin', 'seed', 'migration'
├── UltimoAcceso    datetime2?
├── FecCrea         datetime2
└── FecMod          datetime2?
```

**Restricciones**:
- `UNIQUE(IdUsuario, IdTenant)` — un usuario no puede tener dos membresías en el mismo tenant
- `UNIQUE(Id, IdUsuario)` — necesaria para FK compuesta desde Acceso (ver ADR-003)
- `FK_UsuarioTenant_Usuario(IdUsuario) → Usuario(Id)`
- `FK_UsuarioTenant_Tenant(IdTenant) → Tenant(Id)`

**UNIQUE(Id, IdUsuario)** es necesaria porque `Acceso` usará una FK compuesta `(IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)`. SQL Server requiere que la columna referenciada tenga una constraint de unicidad.

## Consequences

**Positive**:
- Modelo explícito de membresía con metadata completa
- Soportado por integridad referencial (FK + UNIQUE), sin triggers
- Permite suspensión por tenant sin afectar otros tenants del mismo usuario
- UsuarioTenant.IdEstado puede diferir de Usuario.IdEstado (membresía vs identidad)
- `EsTenantPrincipal` permite determinar el tenant por defecto

**Negative**:
- Nueva tabla = nueva entidad, configuración EF, repositorio, servicio, DTO
- Las queries de autorización requieren JOIN a UsuarioTenant
- Migración: crear UsuarioTenant para cada usuario existente

**Migration**: Para usuarios existentes se creará:
```sql
INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, EsTenantPrincipal, Origen, FechaIngreso)
SELECT Id, IdTenant, IdEstado, 1, 'MIGRATION', FecCrea
FROM Usuarios WHERE Eliminado = 0;
```

## Related

- ADR-001: Global User Identity
- ADR-003: Access Scope Modelo A
- ADR-004: Platform Scope
