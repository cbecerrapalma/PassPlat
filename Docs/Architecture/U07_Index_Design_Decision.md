# U07 — Índices: Decisión Formal

## Estado: RESUELTO

---

## 1. Índices Existentes Afectados por A1

### 1.1 Acceso — 3 índices modificados

| Índice actual | Columnas | Cambio A1 |
|---------------|----------|-----------|
| `IX_Accesos_Tenant` | `IdTenant` | **Eliminar** — columna eliminada |
| `IX_Accesos_UsuarioTenantAppActivo` | `(IdUsuario, IdTenant, IdApp, Activo)` INCLUDE `(IdRol)` | **Reemplazar** — `IdTenant` → `IdUsuarioTenant` |
| `IX_Accesos_Usuario` | `IdUsuario` | **Conservar** — sigue siendo lookup válido |
| `IX_Accesos_App` | `IdApp` | **Conservar** — sin cambios |
| `IX_Accesos_Rol` | `IdRol` | **Conservar** — sin cambios |
| `UQ_Accesos_UsrAppRol` | `(IdUsuario, IdApp, IdRol)` | **Reemplazar** — ampliar semántica |

### 1.2 Usuario — índices UNIQUE modificados

| Índice actual | Problema | Cambio A1 |
|---------------|----------|-----------|
| `UX_Usuarios_Tenant_NomUsuario` | `(IdTenant, NomUsuario)` WHERE `Eliminado=0` | **Eliminar** — `IdTenant` desaparece de Usuario |
| `UX_Usuarios_TenantEmail` | `(IdTenant, Email)` WHERE `Eliminado=0 AND Email IS NOT NULL` | **Eliminar** — `IdTenant` desaparece de Usuario |

### 1.3 Sin cambios

| Índice | Razón |
|--------|-------|
| `UX_MFA_Principal` | MFA KEEP, sin cambios |
| `IX_MFA_Usuario` | MFA sin cambios |
| `IX_Bloqueos_Activo` | Bloqueos KEEP, sin cambios |
| `IX_GruposUsuarios_Grupo/Usuario` | Grupos sin cambios estructurales |
| `IX_UsuariosPermisos_Usuario` | UsuariosPermisos.IdTenant = EXECUTION CONTEXT, KEEP |
| `UX_Historial_Actual` | HistorialPwd sin cambios |
| `IX_Sesiones_Contexto` | Sesiones.IdTenant = EXECUTION CONTEXT, KEEP |

---

## 2. Nuevos Índices Requeridos

### 2.1 UsuarioTenant (tabla nueva)

| Índice | Columnas | Propósito | Tipo |
|--------|----------|-----------|------|
| `PK_UsuarioTenant` | `Id` | Primary Key | CLUSTERED |
| **`UX_UsuarioTenant_Usuario_Tenant`** | `(IdUsuario, IdTenant)` | Unicidad: un usuario por tenant (CR-03 A0.4) | **UNIQUE** |
| **`UX_UsuarioTenant_Id_IdUsuario`** | `(Id, IdUsuario)` | Clave candidata para FK compuesta de Accesos/UP | **UNIQUE** |
| `IX_UsuarioTenant_Usuario` | `(IdUsuario)` | Lookup: todos los tenants de un usuario | NON-CLUSTERED |
| `IX_UsuarioTenant_Tenant_Estado` | `(IdTenant, Activo, IdEstado)` INCLUDE `(IdUsuario)` | Lookup: miembros activos de un tenant | NON-CLUSTERED |
| `IX_UsuarioTenant_Estado` | `(IdEstado)` | FK index | NON-CLUSTERED |

### 2.2 Acceso — índices reemplazados

| Índice nuevo | Columnas | Reemplaza |
|--------------|----------|-----------|
| **`IX_Accesos_UsuarioTenant`** | `(IdUsuarioTenant)` WHERE `IdUsuarioTenant IS NOT NULL` | `IX_Accesos_Tenant` (parcialmente) |
| **`IX_Accesos_UsuarioTenant_App`** | `(IdUsuarioTenant, IdApp, Activo)` INCLUDE `(IdRol)` WHERE `IdUsuarioTenant IS NOT NULL` | `IX_Accesos_UsuarioTenantAppActivo` (tenant scope) |
| **`IX_Accesos_Platform_App`** | `(IdUsuario, IdApp, Activo)` INCLUDE `(IdRol)` WHERE `IdUsuarioTenant IS NULL` | `IX_Accesos_UsuarioTenantAppActivo` (platform scope) |
| **`UX_Accesos_Platform_UsrAppRol`** | `(IdUsuario, IdApp, IdRol)` WHERE `IdUsuarioTenant IS NULL` | `UQ_Accesos_UsrAppRol` (platform scope) |
| **`UX_Accesos_Tenant_UsrAppRol`** | `(IdUsuarioTenant, IdApp, IdRol)` WHERE `IdUsuarioTenant IS NOT NULL` | `UQ_Accesos_UsrAppRol` (tenant scope) |

### 2.3 Usuario — índices reemplazados

| Índice nuevo | Columnas | Reemplaza |
|--------------|----------|-----------|
| **`UX_Usuarios_NomUsuario`** | `(NomUsuario)` WHERE `Eliminado=0` | `UX_Usuarios_Tenant_NomUsuario` (ahora global) |
| **`UX_Usuarios_Email`** | `(Email)` WHERE `Eliminado=0 AND Email IS NOT NULL` | `UX_Usuarios_TenantEmail` (ahora global) |

---

## 3. Script UP/DOWN para A1.1

### 3.1 UP

```sql
-- ============================================
-- 1. UsuarioTenant
-- ============================================
CREATE TABLE dbo.UsuarioTenant (
    Id int IDENTITY,
    IdUsuario int NOT NULL,
    IdTenant int NOT NULL,
    IdEstado int NOT NULL,
    Activo bit NOT NULL CONSTRAINT DF_UT_Activo DEFAULT (1),
    FecAlta datetime2(3) NOT NULL CONSTRAINT DF_UT_FecAlta DEFAULT (sysdatetime()),
    FecMod datetime2(3) NULL,
    IdUsrMod int NULL,
    CONSTRAINT PK_UsuarioTenant PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UX_UsuarioTenant_Usuario_Tenant UNIQUE (IdUsuario, IdTenant),
    CONSTRAINT FK_UT_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id),
    CONSTRAINT FK_UT_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
    CONSTRAINT FK_UT_Estado FOREIGN KEY (IdEstado) REFERENCES dbo.EstadosUsr(Id)
);

-- UNIQUE (Id, IdUsuario) para FK compuesta Accesos/UP → UsuarioTenant
CREATE UNIQUE INDEX UX_UsuarioTenant_Id_IdUsuario
    ON dbo.UsuarioTenant (Id, IdUsuario);

CREATE INDEX IX_UsuarioTenant_Usuario
    ON dbo.UsuarioTenant (IdUsuario);
CREATE INDEX IX_UsuarioTenant_Tenant_Estado
    ON dbo.UsuarioTenant (IdTenant, Activo, IdEstado)
    INCLUDE (IdUsuario);
CREATE INDEX IX_UsuarioTenant_Estado
    ON dbo.UsuarioTenant (IdEstado);

-- ============================================
-- 2. Acceso — nuevos índices
-- ============================================
-- 2a. DROP existentes
DROP INDEX IX_Accesos_Tenant ON dbo.Accesos;
DROP INDEX IX_Accesos_UsuarioTenantAppActivo ON dbo.Accesos;

-- 2b. Renombrar/crear nuevos
CREATE INDEX IX_Accesos_UsuarioTenant
    ON dbo.Accesos (IdUsuarioTenant)
    WHERE (IdUsuarioTenant IS NOT NULL);

CREATE INDEX IX_Accesos_UsuarioTenant_App
    ON dbo.Accesos (IdUsuarioTenant, IdApp, Activo)
    INCLUDE (IdRol)
    WHERE (IdUsuarioTenant IS NOT NULL);

CREATE INDEX IX_Accesos_Platform_App
    ON dbo.Accesos (IdUsuario, IdApp, Activo)
    INCLUDE (IdRol)
    WHERE (IdUsuarioTenant IS NULL);

-- 2c. Reemplazar UQ_Accesos_UsrAppRol
ALTER TABLE dbo.Accesos DROP CONSTRAINT UQ_Accesos_UsrAppRol;
CREATE UNIQUE INDEX UX_Accesos_Tenant_UsrAppRol
    ON dbo.Accesos (IdUsuarioTenant, IdApp, IdRol)
    WHERE (IdUsuarioTenant IS NOT NULL);
CREATE UNIQUE INDEX UX_Accesos_Platform_UsrAppRol
    ON dbo.Accesos (IdUsuario, IdApp, IdRol)
    WHERE (IdUsuarioTenant IS NULL);

-- ============================================
-- 3. Usuario — reemplazar índices UNIQUE
-- ============================================
DROP INDEX UX_Usuarios_Tenant_NomUsuario ON dbo.Usuarios;
DROP INDEX UX_Usuarios_TenantEmail ON dbo.Usuarios;

CREATE UNIQUE INDEX UX_Usuarios_NomUsuario
    ON dbo.Usuarios (NomUsuario)
    WHERE (Eliminado = 0);

CREATE UNIQUE INDEX UX_Usuarios_Email
    ON dbo.Usuarios (Email)
    WHERE (Eliminado = 0 AND Email IS NOT NULL);

-- ============================================
-- 4. FK compuesta Accesos (reemplaza TR_Accesos_ValidarTenant)
-- ============================================
-- Garantiza que (IdUsuarioTenant, IdUsuario) existe en UsuarioTenant
-- La FK compuesta impide que Acceso.IdUsuario y UsuarioTenant.IdUsuario difieran
-- NOTA: UsuariosPermisos no recibe IdUsuarioTenant — conserva IdTenant como
-- EXECUTION CONTEXT (U01/U08). Su trigger TR_UP_ValidarTenant se reescribe.
ALTER TABLE dbo.Accesos ADD CONSTRAINT FK_Accesos_UsuarioTenant
    FOREIGN KEY (IdUsuarioTenant, IdUsuario)
    REFERENCES dbo.UsuarioTenant (Id, IdUsuario);
```

### 3.2 DOWN

```sql
-- Revertir FK compuesta
ALTER TABLE dbo.Accesos DROP CONSTRAINT FK_Accesos_UsuarioTenant;

-- Revertir UsuarioTenant
DROP INDEX UX_UsuarioTenant_Id_IdUsuario ON dbo.UsuarioTenant;
DROP TABLE dbo.UsuarioTenant;

-- Revertir Acceso
DROP INDEX IX_Accesos_UsuarioTenant ON dbo.Accesos;
DROP INDEX IX_Accesos_UsuarioTenant_App ON dbo.Accesos;
DROP INDEX IX_Accesos_Platform_App ON dbo.Accesos;
DROP INDEX UX_Accesos_Tenant_UsrAppRol ON dbo.Accesos;
DROP INDEX UX_Accesos_Platform_UsrAppRol ON dbo.Accesos;

CREATE INDEX IX_Accesos_Tenant ON dbo.Accesos (IdTenant);
CREATE INDEX IX_Accesos_UsuarioTenantAppActivo
    ON dbo.Accesos (IdUsuario, IdTenant, IdApp, Activo)
    INCLUDE (IdRol);
ALTER TABLE dbo.Accesos ADD CONSTRAINT UQ_Accesos_UsrAppRol UNIQUE (IdUsuario, IdApp, IdRol);

-- Revertir Usuario
DROP INDEX UX_Usuarios_NomUsuario ON dbo.Usuarios;
DROP INDEX UX_Usuarios_Email ON dbo.Usuarios;

CREATE UNIQUE INDEX UX_Usuarios_Tenant_NomUsuario
    ON dbo.Usuarios (IdTenant, NomUsuario)
    WHERE (Eliminado = 0);
CREATE UNIQUE INDEX UX_Usuarios_TenantEmail
    ON dbo.Usuarios (IdTenant, Email)
    WHERE (Eliminado = 0 AND Email IS NOT NULL);
```

---

## 4. Conclusión

**U07 — RESUELTO.** No bloquea A1.1.

| Tabla | Índices nuevos | Índices eliminados | Índices conservados |
|-------|----------------|--------------------|------------------- |
| UsuarioTenant | 6 (1 PK clustered + 2 UNIQUE + 3 non-clustered) | — | — |
| Acceso | 5 (3 non-clustered + 2 UNIQUE) | 2 | 3 |
| Usuario | 2 (2 UNIQUE) | 2 | 0 |
| **Total** | **13** | **4** | **3** |
