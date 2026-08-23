# U03 — Platform Scope Seed: Decisión Formal

## Estado: RESUELTO

---

## 1. Estado Actual (Seeds Existentes)

### 1.1 Roles Platform — YA EXISTEN (IdTenant = NULL)

| Id | Código | Nombre | Descripción | Activo |
|----|--------|--------|-------------|--------|
| 1 | `PLATFORM_ADMIN` | Administrador | Acceso total a todas las funcionalidades del sistema | 1 |
| 2 | `PLATFORM_EDITOR` | Editor | Acceso de lectura/edición a contenido del sistema | 1 |
| 3 | `PLATFORM_SUPERVISOR` | Supervisor | Acceso de solo lectura a todas las funcionalidades del sistema | 1 |
| 4 | `PLATFORM_CONSULTA` | Consulta | Acceso mínimo de solo lectura a módulos básicos | 1 |

**Fuente**: `Seed\Configuracion\03_RolesGlobales.sql` (MERGE por Codigo + IdTenant IS NULL)

### 1.2 RolesPermisos — YA EXISTEN

- `PLATFORM_ADMIN`: todos los permisos activos (INNER JOIN Permisos WHERE Activo=1)
- `PLATFORM_EDITOR`: ~23 permisos (lectura + escritura selectiva)
- `PLATFORM_SUPERVISOR`: ~30 permisos (solo lectura, cobertura amplia)
- `PLATFORM_CONSULTA`: ~6 permisos (solo lectura, mínimo)

**Fuente**: `Seed\Configuracion\03_RolesGlobales.sql` (MERGE por código de permiso)

### 1.3 Acceso Actual — PROBLEMA ESTRUCTURAL

```sql
-- Seed\Configuracion\07_Usuarios.sql
INSERT INTO dbo.Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo)
VALUES (2, @IdTenantP, 1, @IdRolAdmin, 1);
```

En el modelo actual, el Acceso usa `IdTenant` = tenant PLATFORM (Id=1). En el nuevo modelo A1:
- `Usuario.IdTenant` desaparece
- `UsuarioTenant` representa membership, no existe para Platform Context
- `Acceso.IdTenant` debe reemplazarse por `Acceso.IdUsuarioTenant?` (nullable para Platform)

---

## 2. Decisión: Estructura de Platform Access

### 2.1 Modelo

| Elemento | Tenant Context | Platform Context |
|----------|----------------|------------------|
| Identity | `Usuario.IdEstado` | `Usuario.IdEstado` |
| Membership | `UsuarioTenant` (FK + estado + activo) | ❌ No existe |
| Access | `Acceso` via `IdUsuarioTenant` | `Acceso` via `IdUsuario` directo (IdUsuarioTenant=NULL) |

### 2.2 Regla

```
TenantScope:
  Usuario → UsuarioTenant → Acceso(IdUsuarioTenant) → App → Rol

PlatformScope:
  Usuario → Acceso(IdUsuario, IdTenant=NULL) → App → Rol(IdTenant=NULL)
```

**El Acceso actual se reutiliza para Platform Scope** añadiendo un nuevo modo: `Acceso.IdUsuarioTenant IS NULL` indica Platform Access (vinculación directa al usuario, sin membership).

No se requiere tabla adicional (`UsuarioPlatRoles`). La reutilización de `Acceso` con `IdUsuarioTenant=NULL` evita:
- Migración de datos existentes (los Accesos actuales migran a UsuarioTenant)
- Nueva tabla con su propio CRUD
- Duplicación lógica de "asignación rol-usuario"

### 2.3 Impacto en Schema A1.1

```sql
-- Acceso modificado:
-- IdTenant → IdUsuarioTenant int NULL
-- Cuando IdUsuarioTenant IS NULL → Platform Scope (usuario directo, sin tenant)
-- Cuando IdUsuarioTenant IS NOT NULL → Tenant Scope (via UsuarioTenant)

ALTER TABLE dbo.Accesos ADD IdUsuarioTenant int NULL;
ALTER TABLE dbo.Accesos ADD CONSTRAINT FK_Accesos_UsuarioTenant
    FOREIGN KEY (IdUsuarioTenant) REFERENCES dbo.UsuarioTenant(Id);

-- Para Platform Scope, el IdUsuario original se conserva como referencia directa
-- (no cambia: Acceso siempre tiene IdUsuario)
```

### 2.4 Permisos de Platform Scope

Los 4 roles platform existentes **no cambian**. Son correctos porque ya usan `IdTenant=NULL`.

Lo que cambia es **cómo se asigna** un platform role a un usuario:
- Antes: `INSERT Acceso(IdUsuario, IdTenant=1, IdApp, IdRol)` — usaba tenant PLATFORM como "contenedor"
- Después: `INSERT Acceso(IdUsuario, IdUsuarioTenant=NULL, IdApp, IdRol WHERE IdTenant=NULL)` — vinculación directa

---

## 3. Seeds Afectados

### 3.1 Sin cambios (ya correctos)

| Seed | Razón |
|------|-------|
| `01_Modulos.sql` | Módulos no dependen de tenant |
| `02_Permisos.sql` | Permisos no dependen de tenant |
| `03_RolesGlobales.sql` | Roles platform ya con IdTenant=NULL |
| `05_OAuth.sql` | ConfProvIden depende de tenant, no de platform |

### 3.2 Con cambios

| Seed | Cambio |
|------|--------|
| `07_Usuarios.sql` | Accesos: cambiar `IdTenant=@IdTenantP` por `IdUsuarioTenant=NULL` + `IdRol IN (SELECT Id FROM Roles WHERE IdTenant=NULL)` |
| `04_Infraestructura.sql` | Apps: la App PassPlat (Id=1) se mantiene, su asignación a platform es vía Acceso directo |

### 3.3 Nueva sección opcional: PlatformAccess seed

Si se desea crear usuarios platform adicionales en seed, el patrón es:

```sql
-- Platform User (IdTenant=NULL en el nuevo modelo no existe, es global)
-- 1. Crear usuario (global, sin IdTenant en el nuevo modelo)
INSERT INTO dbo.Usuarios (IdEstado, NomUsuario, Email, ...)
VALUES (1, N'auditor_platform', N'auditor@passplat.app', ...);

-- 2. Asignar Acceso Platform Scope (IdUsuarioTenant=NULL)
INSERT INTO dbo.Accesos (IdUsuario, IdUsuarioTenant, IdApp, IdRol)
VALUES (@IdUsuario, NULL, 1, @IdRolSupervisor);
```

---

## 4. Conclusión

**Decisión: REUSE Acceso con IdUsuarioTenant=NULL para Platform Scope.**

| Punto | Decisión |
|-------|----------|
| Roles | ✅ Existentes (PLATFORM_ADMIN, _EDITOR, _SUPERVISOR, _CONSULTA) | 
| IDs | ✅ Fijos (1-4) — preservar en seed |
| Permisos | ✅ Existentes (03_RolesGlobales.sql) — no cambiar |
| Acceso Platform | 🔧 IdUsuarioTenant=NULL en lugar de IdTenant=PLATFORM |
| Tabla nueva | ❌ No necesaria — reusar Acceso |
| Seed 07_Usuarios | 🔧 Modificar acceso sistema + platform_admin |
| Seed 04_Infraestructura | ✅ Sin cambios |

**No bloquea A1.1.** El cambio en Acceso (IdUsuarioTenant NULLable) ya está planificado en A1.1. La lógica de platform access se implementa en SPs (A1.4) y servicios (A1.5).
