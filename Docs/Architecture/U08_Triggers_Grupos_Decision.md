# U08 — Triggers & GruposUsuarios: Decisión Formal

## Estado: RESUELTO

---

## 1. Triggers Afectados por A1

Tres triggers existentes referencian `Usuarios.IdTenant`, columna que se elimina en A1:

| Trigger | Línea | Condición actual | Problema A1 |
|---------|-------|------------------|-------------|
| `TR_GruposUsuarios_ValidarTenant` | 1484 | `g.IdTenant <> u.IdTenant` | `u.IdTenant` eliminado |
| `TR_UsuariosPermisos_ValidarTenant` | 2341 | `u.IdTenant <> i.IdTenant` | `u.IdTenant` eliminado |
| `TR_Accesos_ValidarTenant` | 2686 | `i.IdTenant <> u.IdTenant` | `u.IdTenant` + `Acceso.IdTenant` eliminados |

---

## 2. Decisión por Trigger

### 2.1 TR_Accesos_ValidarTenant → **ELIMINAR**

**Justificación**: Con A1, `Acceso.IdTenant` desaparece y es reemplazado por `Acceso.IdUsuarioTenant` (FK nullable). Se agrega una **FK compuesta** `(IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)` que proporciona integridad referencial directa sobre **ambas columnas**. Esto garantiza que `Acceso.IdUsuario` coincide con `UsuarioTenant.IdUsuario` para el `UsuarioTenant.Id` referenciado. No es necesaria una FK simple suelta porque la compuesta cubre ambos.

**Requisito**: `UsuarioTenant` necesita un UNIQUE `(Id, IdUsuario)` como destino de la FK compuesta. Ver `U07_Index_Design_Decision.md`.

**Impacto**: Ninguno — la FK compuesta reemplaza al trigger para ambos casos:
- Tenant scope (`IdUsuarioTenant IS NOT NULL`): FK compuesta garantiza que el par `(IdUsuarioTenant, IdUsuario)` existe en `UsuarioTenant`, validando tanto la membresía como la identidad del usuario
- Platform scope (`IdUsuarioTenant IS NULL`): sin validación tenant (correcto, y la FK no se evalúa por ser NULL)

### 2.2 TR_GruposUsuarios_ValidarTenant → **REESCRIBIR**

**Condición actual**:
```sql
g.IdTenant <> u.IdTenant
```

**Condición nueva** (vía UsuarioTenant):
```sql
NOT EXISTS (
    SELECT 1 FROM dbo.UsuarioTenant ut
    WHERE ut.IdUsuario = i.IdUsuario
    AND ut.IdTenant = g.IdTenant
)
```

**Trigger completo reescrito**:
```sql
CREATE OR ALTER TRIGGER dbo.TR_GruposUsuarios_ValidarTenant
ON dbo.GruposUsuarios
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Grupos g ON g.Id = i.IdGrupo
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.UsuarioTenant ut
            WHERE ut.IdUsuario = i.IdUsuario
            AND ut.IdTenant = g.IdTenant
        )
    )
    BEGIN
        RAISERROR('El usuario debe tener membresía en el mismo tenant que el grupo.', 16, 1);
        ROLLBACK;
        RETURN;
    END
END;
```

**Nota**: El trigger valida **existencia de membresía** (UsuarioTenant existe), no estado activo. La validación de estado es responsabilidad de la capa de aplicación/SPs.

### 2.3 TR_UsuariosPermisos_ValidarTenant → **REESCRIBIR**

**Condición actual**:
```sql
u.IdTenant <> i.IdTenant
```

**Condición nueva** (vía UsuarioTenant):
```sql
NOT EXISTS (
    SELECT 1 FROM dbo.UsuarioTenant ut
    WHERE ut.IdUsuario = i.IdUsuario
    AND ut.IdTenant = i.IdTenant
)
```

**Trigger completo reescrito**:
```sql
CREATE OR ALTER TRIGGER dbo.TR_UsuariosPermisos_ValidarTenant
ON dbo.UsuariosPermisos
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.UsuarioTenant ut
            WHERE ut.IdUsuario = i.IdUsuario
            AND ut.IdTenant = i.IdTenant
        )
    )
    BEGIN
        RAISERROR('El tenant del permiso directo debe coincidir con una membresía activa del usuario.', 16, 1);
        ROLLBACK;
        RETURN;
    END
END;
```

**Nota**: `UsuariosPermisos.IdTenant` se conserva (es EXECUTION CONTEXT, analizado en U01 para MFA — mismo principio aplica aquí). El trigger cambia la validación de `u.IdTenant == i.IdTenant` (propiedad del usuario) a `UsuarioTenant existe para ese usuario+tenant` (membresía).

---

## 3. GruposUsuarios vs UsuarioTenant — Sin cambios estructurales

### Decisión: **KEEP IdUsuario (no agregar IdUsuarioTenant)**

| Opción | Esquema | Integridad | Complejidad |
|--------|---------|------------|-------------|
| **A** — Agregar IdUsuarioTenant | FK directa a UsuarioTenant | Referencial | Alta (migración, lookup en cada insert) |
| **B** — KEEP IdUsuario + trigger reescrito | Sin cambios | Trigger-based | Baja |

**Se elige Opción B** porque:
1. GruposUsuarios no necesita saber el UsuarioTenant.Id — solo necesita validar que usuario+tenant existe
2. El trigger reescrito es semánticamente equivalente al actual
3. Evita migración de datos y complejidad en insert (resolver UsuarioTenant.Id desde IdUsuario + IdGrupo.Tenant)
4. Los índices existentes (`IX_GruposUsuarios_Grupo`, `IX_GruposUsuarios_Usuario`) no cambian

---

## 4. Resumen de Cambios

| Trigger | Acción | UP (script A1.1) | DOWN (rollback) |
|---------|--------|-------------------|-----------------|
| `TR_Accesos_ValidarTenant` | ELIMINAR | `DROP TRIGGER dbo.TR_Accesos_ValidarTenant` | `CREATE OR ALTER TRIGGER ...` (original) |
| `TR_GruposUsuarios_ValidarTenant` | REESCRIBIR | `CREATE OR ALTER TRIGGER ...` (nuevo) | `CREATE OR ALTER TRIGGER ...` (original) |
| `TR_UsuariosPermisos_ValidarTenant` | REESCRIBIR | `CREATE OR ALTER TRIGGER ...` (nuevo) | `CREATE OR ALTER TRIGGER ...` (original) |
| GruposUsuarios | SIN CAMBIOS | — | — |

---

## 5. Conclusión

**U08 — RESUELTO.** No bloquea A1.1.

**Tres triggers afectados**: 1 eliminado (FK lo reemplaza), 2 reescritos (vía UsuarioTenant). **GruposUsuarios**: sin cambios estructurales — el trigger reescrito es suficiente.
