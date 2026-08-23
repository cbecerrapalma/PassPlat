# ADR-003: Access Scope — Modelo A

**Status**: Aprobado
**Date**: 2026-07-28
**Deciders**: Arquitectura PassPlat
**Source**: A0 — Domain Model Review, A0.2 Semantic Dependency Audit

---

## Context

`Acceso` actualmente contiene `IdTenant int NOT NULL`, lo que significa que todo acceso está inherentemente ligado a un tenant. Con la identidad global (ADR-001) y la membresía explícita (ADR-002), necesitamos un modelo que permita:

- Tenant Scope: acceso dentro de un tenant específico
- Platform Scope: acceso global sin membresía de tenant

Ambos deben coexistir en la misma tabla `Acceso`.

## Problem

Determinar el modelo relacional de `Acceso` que soporte ambos scopes con integridad referencial, sin triggers, y con el mínimo impacto en las consultas existentes.

Las consultas críticas a preservar:

1. **SP_Auth_Login L1601** (cada login):
   ```sql
   WHERE a.IdUsuario = @IdUsuario AND a.IdApp = @IdApp AND a.Activo = 1
   ```
   Esta consulta **no usa IdTenant**. Debe seguir funcionando sin JOIN extra.

2. **SP_Permisos_Usuario_Efectivos L639-644** (cada request JWT):
   ```sql
   WHERE a.IdUsuario = @IdUsuario
     AND a.IdTenant = @IdTenant
     AND a.Activo = 1
   ```
   Esta consulta **usa IdTenant** como filtro de scope. Debe migrarse a UsuarioTenant.

## Options Considered

### Modelo A: Acceso(IdUsuario, IdUsuarioTenant?, IdApp, IdRol)

```csharp
Acceso
├── Id                int (PK)
├── IdUsuario         int NOT NULL (FK → Usuario)
├── IdUsuarioTenant   int? NULL (FK → UsuarioTenant)
├── IdApp             int NOT NULL (FK → App)
├── IdRol             int NOT NULL (FK → Rol)
├── Activo            bit
└── FecAsignacion     datetime2
```

**Reglas**:
- `IdUsuarioTenant IS NULL` → Platform Scope
- `IdUsuarioTenant IS NOT NULL` → Tenant Scope
- `UNIQUE(IdUsuarioTenant, IdApp, IdRol)`
- FK compuesta: `(IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)`

### Modelo B: Acceso(IdUsuarioTenant?, IdApp, IdRol)

```csharp
Acceso
├── IdUsuarioTenant   int? NULL (FK → UsuarioTenant)
├── IdApp             int NOT NULL
├── IdRol             int NOT NULL
```

**Reglas**: IdUsuario siempre derivable de UsuarioTenant.

## Decision

**Elegido: Modelo A**.

### Justificación técnica

**Razón 1 — SP_Auth_Login L1601 no cambia**:
```sql
-- Actual: funciona porque IdUsuario está en Acceso
WHERE a.IdUsuario = @IdUsuario AND a.IdApp = @IdApp AND a.Activo = 1
-- Con Modelo B necesitaría:
WHERE a.IdUsuarioTenant IN (SELECT ut.Id FROM UsuarioTenant ut WHERE ut.IdUsuario = @IdUsuario)
  AND a.IdApp = @IdApp AND a.Activo = 1
```
La consulta de login es la más crítica del sistema (ejecutada en cada autenticación). Modelo A la preserva sin JOIN.

**Razón 2 — Platform Scope sin JOIN**:
Con Modelo A, un Acceso Platform Scope (`IdUsuarioTenant IS NULL`) tiene `IdUsuario` directamente. Con Modelo B, no hay forma de saber a qué usuario pertenece un Platform Scope sin JOIN a otra estructura.

**Razón 3 — Integridad referencial declarativa**:
```sql
FOREIGN KEY (IdUsuarioTenant, IdUsuario) REFERENCES UsuarioTenant(Id, IdUsuario)
```
SQL Server permite FK compuesta con columna NULL: cuando `IdUsuarioTenant IS NULL`, la FK no se evalúa. Esto garantiza que cuando hay Tenant Scope, el usuario coincida con el usuario de la membresía.

**Razón 4 — Migración en fases**:
```sql
ALTER TABLE Acceso ADD IdUsuarioTenant int NULL;
-- Poblar: JOIN con UsuarioTenant por (IdUsuario, IdTenant)
UPDATE a SET a.IdUsuarioTenant = ut.Id
FROM Acceso a JOIN UsuarioTenant ut ON ut.IdUsuario = a.IdUsuario AND ut.IdTenant = a.IdTenant;
-- Luego: eliminar FK_Accesos_Tenant, eliminar columna IdTenant
```

### Redundancia aceptada

`IdUsuario` es redundante cuando `IdUsuarioTenant IS NOT NULL` (derivable de UsuarioTenant). Esta redundancia es intencional y aceptada por las razones de performance y simplicidad de consulta expuestas arriba.

## Consequences

**Positive**:
- Consulta de login (SP_Auth_Login L1601) sin cambios
- Platform Scope sin JOIN extra
- Integridad referencial vía FK compuesta (sin triggers)
- Migración en fases: ADD columna → poblar → eliminar vieja
- Unicidad: `UNIQUE(IdUsuarioTenant, IdApp, IdRol)`

**Negative**:
- Redundancia de `IdUsuario` cuando hay Tenant Scope
- Necesidad de `UNIQUE(Id, IdUsuario)` en UsuarioTenant para FK compuesta
- Migración requiere poblar `IdUsuarioTenant` para todos los registros existentes

**Indexes post-migración**:
```sql
-- Reemplaza a IX_Accesos_UsuarioTenantAppActivo actual:
CREATE INDEX IX_Accesos_Scope ON Acceso (IdUsuarioTenant, IdApp, Activo) INCLUDE (IdRol);
-- Para Platform Scope queries:
CREATE INDEX IX_Accesos_Platform ON Acceso (IdUsuario, IdApp, Activo) INCLUDE (IdRol)
  WHERE IdUsuarioTenant IS NULL;
```

## Related

- ADR-001: Global User Identity
- ADR-002: UsuarioTenant Membership
- ADR-004: Platform Scope
