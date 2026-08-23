# A1 — Script Pack Completo

**Proyecto:** PassPlat — Multi-Tenant Evolution  
**Fecha:** 2026-07-28  
**Gate:** A1.0 Approval Gate firmado (A1 FROZEN)  
**Backup:** C:\SQL\BACKUP\PassPlat_A1.1_PreMigration.bak  

---

## A1.1 — SQL Schema (✅ EJECUTADO)

| # | Script | Propósito | Estado |
|---|--------|-----------|--------|
| 001 | Preflight | 9 validaciones pre-ejecución | ✅ PASS (9/9) |
| 002 | Create UsuarioTenant | Crear tabla UsuarioTenant | ✅ PASS |
| 003 | Alter Accesos | Agregar IdUsuarioTenant | ✅ PASS |
| 004 | Alter UsuariosPermisos | No-op documental | ✅ N/A |
| 005 | Indexes | 7 nuevos índices | ✅ PASS |
| 006 | Foreign Keys | FK compuesta Accesos → UsuarioTenant | ✅ PASS |
| 007 | Triggers | 1 DROP, 3 REWRITE, 1 MODIFY | ✅ PASS |
| 008 | Stored Procedures | Plan diferido a A1.4 | ✅ N/A |
| 009 | Postflight | Validación final | ✅ PASS (0 errores) |

### Verificaciones A1.1
- Tabla UsuarioTenant (0 filas — vacía, pendiente de A1.2)
- Accesos.IdUsuarioTenant (int NULL)
- FK compuesta (IdUsuarioTenant, IdUsuario) → UsuarioTenant(Id, IdUsuario)
- 13 índices nuevos / 6 legacy conservados
- TR_Accesos_ValidarTenant eliminado / 4 triggers reescritos

---

## A1.2 — Data Migration (⏳ PENDIENTE)

| # | Script | Propósito | Estado |
|---|--------|-----------|--------|
| 010 | Preflight | Prevalidación de datos (SELECT-only) | ⏳ Pendiente |
| 011 | Migration | INSERT UsuarioTenant + UPDATE Accesos (transaccional) | ⏳ Pendiente |
| 012 | Postflight | Validación independiente post-migración | ⏳ Pendiente |

### Reglas de migración
- **Fuente canónica de tenant:** Roles.IdTenant (NO Accesos.IdTenant)
- **Usuario activo + Acceso tenant-scope:** → INSERT UsuarioTenant + UPDATE Accesos.IdUsuarioTenant
- **Usuario activo + solo Accesos platform-scope:** → NO migrar (IdUsuarioTenant=NULL permanente)
- **Usuario Eliminado=1:** → NO migrar (excluido por política)
- **Multi-tenant (usuario en >1 tenant):** → soportado (INFO, no error)
- **One-shot:** Preflight valida UsuarioTenant vacío antes de migrar

---

---

## 010_A1.2_Preflight.sql
### Prevalidación de datos (SELECT-only)
-- ============================================
-- A1.2 - 010_Preflight.sql
-- Prevalidación de datos pre-migración
-- Fuente canónica de tenant: Roles.IdTenant
-- One-shot guard: UsuarioTenant debe estar vacío
-- ============================================
-- Política:
--   Usuarios Eliminado=1: NO migrar (Accesos se
--   quedan con IdUsuarioTenant=NULL aunque el rol
--   sea tenant-scope). La capa de aplicación filtra.
--   Usuarios Eliminado=0 con Accesos tenant-scope:
--     SI migrar (INSERT UsuarioTenant + UPDATE Accesos)
--   Usuarios Eliminado=0 con solo Accesos platform-scope:
--     NO migrar (IdUsuarioTenant=NULL permanente)

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Errors int = 0;

PRINT '=== A1.2 - 010_PREFLIGHT ===';
PRINT '';

-- ============================================
-- 10.1: One-shot guard
-- ============================================
PRINT '--- 10.1: One-shot guard ---';

IF EXISTS (SELECT 1 FROM dbo.UsuarioTenant)
BEGIN
    PRINT 'FAIL 10.1: UsuarioTenant ya contiene datos. A1.2 es ONE-SHOT.';
    SELECT COUNT(*) as ExistingRows FROM dbo.UsuarioTenant;
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 10.1: UsuarioTenant vacío. One-shot OK.';

-- ============================================
-- 10.2: Validar que IdEstado=1 existe y es "Activo"
-- ============================================
PRINT '';
PRINT '--- 10.2: Estado Activo (Id=1) ---';

IF EXISTS (SELECT 1 FROM dbo.EstadosUsr WHERE Id = 1)
BEGIN
    DECLARE @EstadoNombre varchar(100);
    SELECT @EstadoNombre = Nombre FROM dbo.EstadosUsr WHERE Id = 1;
    PRINT 'PASS 10.2: EstadosUsr.Id=1 existe: "' + @EstadoNombre + '"';
END
ELSE
BEGIN
    PRINT 'FAIL 10.2: EstadosUsr.Id=1 NO existe. No se puede asumir Activo.';
    SELECT Id, Nombre FROM dbo.EstadosUsr ORDER BY Id;
    SET @Errors = @Errors + 1;
END

-- ============================================
-- 10.3: Roles tenant-scope y platform-scope
-- ============================================
PRINT '';
PRINT '--- 10.3: Roles tenant-scope (IdTenant IS NOT NULL) ---';

SELECT Id, Nombre, IdTenant,
    CASE WHEN IdTenant IS NULL THEN 'PLATFORM' ELSE 'TENANT' END as Scope
FROM dbo.Roles ORDER BY Id;

PRINT '';
SELECT COUNT(*) as PlatformRoles FROM dbo.Roles WHERE IdTenant IS NULL;
SELECT COUNT(*) as TenantRoles FROM dbo.Roles WHERE IdTenant IS NOT NULL;

-- ============================================
-- 10.4: Incoherencias Accesos.IdTenant vs Roles.IdTenant
-- Roles.IdTenant es la fuente canónica.
-- Si algún Acceso tenant-scope tiene Accesos.IdTenant
-- distinto de Roles.IdTenant, hay inconsistencia legacy.
-- ============================================
PRINT '';
PRINT '--- 10.4: Incoherencias Accesos.IdTenant vs Roles.IdTenant ---';
PRINT 'Regla: 0 = todos los Accesos tenant-scope tienen IdTenant coherente con su Rol';
PRINT '';

SELECT
    a.Id as AccesoId,
    a.IdUsuario,
    u.NomUsuario,
    a.IdTenant as AccesoIdTenant,
    r.IdTenant as RolIdTenant,
    a.IdApp,
    r.Nombre as RolNombre
FROM dbo.Accesos a
JOIN dbo.Roles r ON a.IdRol = r.Id
JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
WHERE r.IdTenant IS NOT NULL
    AND a.IdTenant <> r.IdTenant;

IF @@ROWCOUNT = 0
    PRINT 'PASS 10.4: Sin incoherencias. Accesos.IdTenant == Roles.IdTenant para tenant-scope.';
ELSE
BEGIN
    PRINT 'FAIL 10.4: Hay Accesos tenant-scope con IdTenant distinto al del Rol.';
    PRINT '         Corrección manual requerida antes de migrar.';
    SET @Errors = @Errors + 1;
END

-- ============================================
-- 10.5: Accesos tenant-scope para usuarios eliminados
-- Política: NO migrar. Se registran para decisión.
-- ============================================
PRINT '';
PRINT '--- 10.5: Accesos tenant-scope de usuarios Eliminado=1 ---';
PRINT 'Politica: No se migran. Quedan con IdUsuarioTenant=NULL.';
PRINT '';

SELECT
    a.Id as AccesoId,
    a.IdUsuario,
    u.NomUsuario,
    u.Eliminado,
    r.IdTenant as RolTenant,
    r.Nombre as RolNombre
FROM dbo.Accesos a
JOIN dbo.Roles r ON a.IdRol = r.Id
JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 1;

IF @@ROWCOUNT = 0
    PRINT 'PASS 10.5: Sin Accesos tenant-scope de usuarios eliminados.';
ELSE
    PRINT 'INFO 10.5: ' + CAST(@@ROWCOUNT AS varchar) + ' Accesos excluidos (politica: usuarios eliminados no migran).';

-- ============================================
-- 10.6: Duplicados potenciales post-migracion
-- Usando Roles.IdTenant como fuente canónica
-- ============================================
PRINT '';
PRINT '--- 10.6: Duplicados potenciales (IdUsuario, Roles.IdTenant, IdApp, IdRol) ---';
PRINT 'Regla: 0 = no hay duplicados que rompan UX_Accesos_Tenant_UsrAppRol';
PRINT '';

SELECT
    a.IdUsuario,
    u.NomUsuario,
    r.IdTenant as TenantCanonico,
    a.IdApp,
    a.IdRol,
    COUNT(*) as Cantidad,
    STRING_AGG(CAST(a.Id AS varchar(20)), ',') as AccesoIds
FROM dbo.Accesos a
JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
JOIN dbo.Roles r ON a.IdRol = r.Id
WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 0
GROUP BY a.IdUsuario, u.NomUsuario, r.IdTenant, a.IdApp, a.IdRol
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0
    PRINT 'PASS 10.6: Sin duplicados potenciales.';
ELSE
BEGIN
    PRINT 'FAIL 10.6: Hay duplicados que romperian la unicidad post-migracion.';
    SET @Errors = @Errors + 1;
END

-- ============================================
-- 10.7: Usuarios con multi-tenant (INFO, no error)
-- El nuevo modelo permite Usuario > 1 UsuarioTenant
-- ============================================
PRINT '';
PRINT '--- 10.7: Usuarios con Accesos tenant-scope en multiples tenants ---';
PRINT 'INFO: El nuevo modelo permite membresias multiples.';
PRINT '';

SELECT
    a.IdUsuario,
    u.NomUsuario,
    COUNT(DISTINCT r.IdTenant) as TenantsDistintos,
    STRING_AGG(CAST(r.IdTenant AS varchar(10)), ',') as Tenants
FROM dbo.Accesos a
JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
JOIN dbo.Roles r ON a.IdRol = r.Id
WHERE u.Eliminado = 0 AND r.IdTenant IS NOT NULL
GROUP BY a.IdUsuario, u.NomUsuario
HAVING COUNT(DISTINCT r.IdTenant) > 1;

IF @@ROWCOUNT = 0
    PRINT 'INFO 10.7: Todos los usuarios tienen 1 tenant (modelo legacy).';
ELSE
    PRINT 'INFO 10.7: Se crearan multiples UsuarioTenant por usuario.';

-- ============================================
-- 10.8: Candidatos a migrar (preview)
-- ============================================
PRINT '';
PRINT '--- 10.8: Candidatos UsuarioTenant ---';
PRINT '';

SELECT
    u.Id as IdUsuario,
    u.NomUsuario,
    r.IdTenant as IdTenantCanonico,
    t.Nombre as TenantNombre,
    t.EsSistema as TenantEsSistema,
    COUNT(DISTINCT a.Id) as AccesosCount,
    STRING_AGG(DISTINCT CAST(a.IdRol AS varchar(10)), ',') as RolesEnTenant
FROM dbo.Usuarios u
JOIN dbo.Accesos a ON a.IdUsuario = u.Id
JOIN dbo.Roles r ON a.IdRol = r.Id
JOIN dbo.Tenants t ON r.IdTenant = t.Id
WHERE u.Eliminado = 0
    AND r.IdTenant IS NOT NULL
GROUP BY u.Id, u.NomUsuario, r.IdTenant, t.Nombre, t.EsSistema
ORDER BY u.Id, r.IdTenant;

DECLARE @Candidates int = @@ROWCOUNT;

-- ============================================
-- 10.9: Resumen
-- ============================================
PRINT '';
PRINT '=== RESUMEN 010_PREFLIGHT ===';
PRINT 'Candidatos UsuarioTenant a insertar: ' + CAST(@Candidates AS varchar);
PRINT 'Accesos tenant-scope a actualizar: ' + CAST(ISNULL((SELECT COUNT(*) FROM dbo.Accesos a JOIN dbo.Roles r ON a.IdRol = r.Id WHERE r.IdTenant IS NOT NULL AND a.IdUsuario IN (SELECT Id FROM dbo.Usuarios WHERE Eliminado = 0)), 0) AS varchar);
PRINT 'Accesos platform-scope (sin cambios): ' + CAST((SELECT COUNT(*) FROM dbo.Accesos a JOIN dbo.Roles r ON a.IdRol = r.Id WHERE r.IdTenant IS NULL) AS varchar);
PRINT '';

IF @Errors = 0
    PRINT '=== 010 PREFLIGHT PASS: 0 errores. Continuar con 011_Migration. ===';
ELSE
BEGIN
    PRINT '=== 010 PREFLIGHT FAIL: ' + CAST(@Errors AS varchar) + ' error(es). ===';
    PRINT '=== CORREGIR ANTES DE EJECUTAR 011 ===';
    THROW 50001, '010 Preflight fallo. Corregir antes de continuar.', 1;
END


---

## 011_A1.2_Migration.sql
### Migración transaccional (INSERT+UPDATE+COMMIT)
-- ============================================
-- A1.2 - 011_Migration.sql
-- Migración de datos al nuevo esquema A1
-- ============================================
-- Fuente canónica de tenant: Roles.IdTenant
-- Solo se migran usuarios Eliminado=0
-- One-shot: asume UsuarioTenant vacío (validado en 010)
--
-- Flujo:
--   Preview → BEGIN TRAN → INSERT UsuarioTenant
--   → UPDATE Accesos.IdUsuarioTenant
--   → 5 validaciones post → COMMIT
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '=== A1.2 - 011_MIGRATION ===';
PRINT '';

-- ============================================
-- 11.1: Preview de lo que se insertará
-- ============================================
PRINT '--- 11.1: Preview UsuarioTenant ---';

SELECT
    u.Id as IdUsuario,
    u.NomUsuario,
    u.Email,
    r.IdTenant as IdTenantCanonico,
    t.Nombre as TenantNombre,
    '1' as IdEstado,
    '1' as Activo
FROM dbo.Usuarios u
JOIN dbo.Accesos a ON a.IdUsuario = u.Id
JOIN dbo.Roles r ON a.IdRol = r.Id
JOIN dbo.Tenants t ON r.IdTenant = t.Id
WHERE u.Eliminado = 0
    AND r.IdTenant IS NOT NULL
GROUP BY u.Id, u.NomUsuario, u.Email, r.IdTenant, t.Nombre
ORDER BY u.Id, r.IdTenant;

DECLARE @PreviewCount int = @@ROWCOUNT;
PRINT 'Preview: ' + CAST(@PreviewCount AS varchar) + ' filas a insertar';
PRINT '';

-- ============================================
-- 11.2: Iniciar transacción
-- ============================================
PRINT '--- 11.2: Iniciando transaccion ---';
PRINT '';
PRINT 'COMMIT se ejecutará solo si todas las validaciones post-pasan.';
PRINT '';

BEGIN TRANSACTION;
BEGIN TRY

    -- ============================================
    -- 11.3: INSERT UsuarioTenant
    -- Roles.IdTenant es la fuente canónica.
    -- ============================================
    INSERT INTO dbo.UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo)
    SELECT DISTINCT
        u.Id as IdUsuario,
        r.IdTenant as IdTenant,
        1 as IdEstado,
        1 as Activo
    FROM dbo.Usuarios u
    JOIN dbo.Accesos a ON a.IdUsuario = u.Id
    JOIN dbo.Roles r ON a.IdRol = r.Id
    WHERE u.Eliminado = 0
        AND r.IdTenant IS NOT NULL;

    PRINT '11.3: INSERT UsuarioTenant → ' + CAST(@@ROWCOUNT AS varchar) + ' filas';

    -- ============================================
    -- 11.4: UPDATE Accesos.IdUsuarioTenant
    -- Match por (IdUsuario, Roles.IdTenant)
    -- ============================================
    UPDATE a
    SET a.IdUsuarioTenant = ut.Id
    FROM dbo.Accesos a
    JOIN dbo.Roles r ON a.IdRol = r.Id
    JOIN dbo.UsuarioTenant ut ON ut.IdUsuario = a.IdUsuario AND ut.IdTenant = r.IdTenant
    WHERE r.IdTenant IS NOT NULL
        AND a.IdUsuario IN (SELECT Id FROM dbo.Usuarios WHERE Eliminado = 0);

    PRINT '11.4: UPDATE Accesos.IdUsuarioTenant → ' + CAST(@@ROWCOUNT AS varchar) + ' filas';

    -- ============================================
    -- 11.5: Validación 1 — 100% Accesos tenant-scope mapeados
    -- ============================================
    IF EXISTS (
        SELECT 1 FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
        WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 0 AND a.IdUsuarioTenant IS NULL
    )
    BEGIN
        PRINT 'FAIL 11.5: Hay Accesos tenant-scope (usuarios activos) sin IdUsuarioTenant';
        SELECT a.Id, a.IdUsuario, u.NomUsuario, a.IdTenant
        FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
        WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 0 AND a.IdUsuarioTenant IS NULL;
        THROW 50001, '11.5: Migracion incompleta', 1;
    END
    PRINT 'PASS 11.5: 100% Accesos tenant-scope (usuarios activos) mapeados';

    -- ============================================
    -- 11.6: Validación 2 — FK compuesta válida
    -- Ningún Acceso.IdUsuarioTenant apunta a un UsuarioTenant
    -- con distinto IdUsuario (violaría la FK compuesta).
    -- ============================================
    IF EXISTS (
        SELECT 1 FROM dbo.Accesos a
        WHERE a.IdUsuarioTenant IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM dbo.UsuarioTenant ut
                WHERE ut.Id = a.IdUsuarioTenant AND ut.IdUsuario = a.IdUsuario
            )
    )
    BEGIN
        PRINT 'FAIL 11.6: FK compuesta violada';
        SELECT a.Id, a.IdUsuario, a.IdUsuarioTenant
        FROM dbo.Accesos a
        WHERE a.IdUsuarioTenant IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM dbo.UsuarioTenant ut
                WHERE ut.Id = a.IdUsuarioTenant AND ut.IdUsuario = a.IdUsuario
            );
        THROW 50001, '11.6: FK compuesta violada', 1;
    END
    PRINT 'PASS 11.6: FK compuesta válida para todos los Accesos';

    -- ============================================
    -- 11.7: Validación 3 — Platform-scope sigue con NULL
    -- ============================================
    IF EXISTS (
        SELECT 1 FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        WHERE r.IdTenant IS NULL AND a.IdUsuarioTenant IS NOT NULL
    )
    BEGIN
        PRINT 'FAIL 11.7: Accesos platform-scope contaminados con IdUsuarioTenant';
        THROW 50001, '11.7: Platform-scope contaminado', 1;
    END
    PRINT 'PASS 11.7: Platform-scope intacto (IdUsuarioTenant=NULL)';

    -- ============================================
    -- 11.8: Validación 4 — Cardinalidad EXCEPT
    -- Todo UsuarioTenant tiene origen en Accesos tenant-scope
    -- ============================================
    IF EXISTS (
        SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut
        EXCEPT
        SELECT DISTINCT a.IdUsuario, r.IdTenant
        FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        WHERE r.IdTenant IS NOT NULL
    )
    BEGIN
        PRINT 'FAIL 11.8: Existen UsuarioTenant sin origen en Accesos tenant-scope';
        SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut
        EXCEPT
        SELECT DISTINCT a.IdUsuario, r.IdTenant
        FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        WHERE r.IdTenant IS NOT NULL;
        THROW 50001, '11.8: UsuarioTenant huerfano detectado', 1;
    END
    PRINT 'PASS 11.8: Todo UsuarioTenant tiene origen en Accesos tenant-scope';

    -- ============================================
    -- 11.9: Validación 5 — No faltan membresías requeridas
    -- ============================================
    IF EXISTS (
        SELECT DISTINCT a.IdUsuario, r.IdTenant
        FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        WHERE r.IdTenant IS NOT NULL
        EXCEPT
        SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut
    )
    BEGIN
        PRINT 'FAIL 11.9: Existen membresías requeridas que no fueron migradas';
        SELECT DISTINCT a.IdUsuario, r.IdTenant
        FROM dbo.Accesos a
        JOIN dbo.Roles r ON a.IdRol = r.Id
        WHERE r.IdTenant IS NOT NULL
        EXCEPT
        SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut;
        THROW 50001, '11.9: Membresias requeridas faltantes', 1;
    END
    PRINT 'PASS 11.9: Todas las membresías requeridas fueron migradas';

    -- ============================================
    -- 11.10: Validación 6 — UsuarioTenant.IdUsuario == Acceso.IdUsuario
    -- Garantiza que ningún Acceso.IdUsuarioTenant apunta
    -- a un UsuarioTenant de otro usuario.
    -- ============================================
    IF EXISTS (
        SELECT 1 FROM dbo.Accesos a
        WHERE a.IdUsuarioTenant IS NOT NULL
            AND EXISTS (
                SELECT 1 FROM dbo.UsuarioTenant ut
                WHERE ut.Id = a.IdUsuarioTenant AND ut.IdUsuario <> a.IdUsuario
            )
    )
    BEGIN
        PRINT 'FAIL 11.10: Acceso.IdUsuarioTenant apunta a UsuarioTenant de otro usuario';
        THROW 50001, '11.10: Mismatch de usuario en FK compuesta', 1;
    END
    PRINT 'PASS 11.10: Todos los Accesos.IdUsuarioTenant referencian al usuario correcto';

    -- ============================================
    -- 11.11: Snapshot post-migración
    -- ============================================
    DECLARE @UTCount int, @AccUTenantCount int, @AccPlatformCount int;
    SELECT @UTCount = COUNT(*) FROM dbo.UsuarioTenant;
    SELECT @AccUTenantCount = COUNT(*) FROM dbo.Accesos WHERE IdUsuarioTenant IS NOT NULL;
    SELECT @AccPlatformCount = COUNT(*) FROM dbo.Accesos a JOIN dbo.Roles r ON a.IdRol = r.Id WHERE r.IdTenant IS NULL;

    PRINT '';
    PRINT '--- 11.11: Snapshot post-migracion ---';
    PRINT 'UsuarioTenant: ' + CAST(@UTCount AS varchar);
    PRINT 'Accesos con IdUsuarioTenant: ' + CAST(@AccUTenantCount AS varchar);
    PRINT 'Accesos platform-scope (NULL): ' + CAST(@AccPlatformCount AS varchar);
    PRINT '';

    -- ============================================
    -- COMMIT
    -- ============================================
    PRINT '=== A1.2 - MIGRACION COMPLETADA ===';
    PRINT 'Todas las validaciones post-migracion pasaron.';
    COMMIT TRANSACTION;
    PRINT '=== COMMIT EJECUTADO ===';

END TRY
BEGIN CATCH
    DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @ErrLine int = ERROR_LINE();
    PRINT 'ERROR (linea ' + CAST(@ErrLine AS varchar) + '): ' + @ErrMsg;
    ROLLBACK TRANSACTION;
    PRINT '=== ROLLBACK EJECUTADO. Ningun cambio persistido. ===';
    THROW;
END CATCH;


---

## 012_A1.2_Postflight.sql
### Validación post-migración
-- ============================================
-- A1.2 - 012_Postflight.sql
-- Validación independiente post-migración
-- Ejecutar DESPUÉS de 011 (incluso tras COMMIT)
-- No escribe nada, solo SELECT + PRINT
-- ============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Errors int = 0;

PRINT '=== A1.2 - 012_POSTFLIGHT ===';
PRINT '';

-- ============================================
-- 12.1: Schema — verificar que A1.1 está completo
-- ============================================
PRINT '--- 12.1: Schema ---';

IF OBJECT_ID('dbo.UsuarioTenant') IS NOT NULL
    PRINT 'PASS 12.1: UsuarioTenant existe';
ELSE
BEGIN PRINT 'FAIL 12.1: UsuarioTenant no existe'; SET @Errors = @Errors + 1; END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Accesos') AND name = 'IdUsuarioTenant')
    PRINT 'PASS 12.1: Accesos.IdUsuarioTenant existe';
ELSE
BEGIN PRINT 'FAIL 12.1: Accesos.IdUsuarioTenant no existe'; SET @Errors = @Errors + 1; END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Accesos_UsuarioTenant')
    PRINT 'PASS 12.1: FK_Accesos_UsuarioTenant existe';
ELSE
BEGIN PRINT 'FAIL 12.1: FK_Accesos_UsuarioTenant no existe'; SET @Errors = @Errors + 1; END

-- ============================================
-- 12.2: Integridad FK compuesta
-- ============================================
PRINT '';
PRINT '--- 12.2: Integridad FK compuesta ---';

IF EXISTS (
    SELECT 1 FROM dbo.Accesos a
    WHERE a.IdUsuarioTenant IS NOT NULL
        AND NOT EXISTS (
            SELECT 1 FROM dbo.UsuarioTenant ut
            WHERE ut.Id = a.IdUsuarioTenant AND ut.IdUsuario = a.IdUsuario
        )
)
BEGIN
    PRINT 'FAIL 12.2: Violaciones de FK compuesta detectadas';
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 12.2: FK compuesta 100% valida';

-- ============================================
-- 12.3: Accesos tenant-scope mapeados
-- ============================================
PRINT '';
PRINT '--- 12.3: Accesos tenant-scope (usuarios activos) ---';

DECLARE @Missing int;
SELECT @Missing = COUNT(*)
FROM dbo.Accesos a
JOIN dbo.Roles r ON a.IdRol = r.Id
JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 0 AND a.IdUsuarioTenant IS NULL;

IF @Missing = 0
    PRINT 'PASS 12.3: 100% Accesos tenant-scope (usuarios activos) mapeados';
ELSE
BEGIN
    PRINT 'FAIL 12.3: ' + CAST(@Missing AS varchar) + ' Accesos tenant-scope sin mapear';
    SET @Errors = @Errors + 1;
END

-- ============================================
-- 12.4: Platform-scope NO contaminados
-- ============================================
PRINT '';
PRINT '--- 12.4: Platform-scope ---';

IF EXISTS (
    SELECT 1 FROM dbo.Accesos a
    JOIN dbo.Roles r ON a.IdRol = r.Id
    WHERE r.IdTenant IS NULL AND a.IdUsuarioTenant IS NOT NULL
)
BEGIN
    PRINT 'FAIL 12.4: Platform-scope contaminado con IdUsuarioTenant';
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 12.4: Platform-scope intacto';

-- ============================================
-- 12.5: Mismatch de usuario en FK
-- ============================================
PRINT '';
PRINT '--- 12.5: Mismatch de usuario ---';

IF EXISTS (
    SELECT 1 FROM dbo.Accesos a
    WHERE a.IdUsuarioTenant IS NOT NULL
        AND EXISTS (
            SELECT 1 FROM dbo.UsuarioTenant ut
            WHERE ut.Id = a.IdUsuarioTenant AND ut.IdUsuario <> a.IdUsuario
        )
)
BEGIN
    PRINT 'FAIL 12.5: Accesos con mismatch de usuario';
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 12.5: Sin mismatch de usuario';

-- ============================================
-- 12.6: Cardinalidad EXCEPT (bidireccional)
-- ============================================
PRINT '';
PRINT '--- 12.6: Cardinalidad UsuarioTenant vs Accesos ---';

IF EXISTS (
    SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut
    EXCEPT
    SELECT DISTINCT a.IdUsuario, r.IdTenant
    FROM dbo.Accesos a
    JOIN dbo.Roles r ON a.IdRol = r.Id
    WHERE r.IdTenant IS NOT NULL
)
BEGIN
    PRINT 'FAIL 12.6a: UsuarioTenant sin origen en Accesos';
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 12.6a: Todo UsuarioTenant tiene origen en Accesos';

IF EXISTS (
    SELECT DISTINCT a.IdUsuario, r.IdTenant
    FROM dbo.Accesos a
    JOIN dbo.Roles r ON a.IdRol = r.Id
    WHERE r.IdTenant IS NOT NULL
    EXCEPT
    SELECT ut.IdUsuario, ut.IdTenant FROM dbo.UsuarioTenant ut
)
BEGIN
    PRINT 'FAIL 12.6b: Membresias requeridas faltan en UsuarioTenant';
    SET @Errors = @Errors + 1;
END
ELSE
    PRINT 'PASS 12.6b: Todas las membresias requeridas existen';

-- ============================================
-- 12.7: Conteos detallados
-- ============================================
PRINT '';
PRINT '--- 12.7: Conteos ---';

SELECT '#Accesos total' as metrica, CAST(COUNT(*) AS varchar(20)) as valor FROM dbo.Accesos
UNION ALL
SELECT '#Accesos con IdUsuarioTenant', CAST(COUNT(*) AS varchar(20)) FROM dbo.Accesos WHERE IdUsuarioTenant IS NOT NULL
UNION ALL
SELECT '#Accesos sin IdUsuarioTenant', CAST(COUNT(*) AS varchar(20)) FROM dbo.Accesos WHERE IdUsuarioTenant IS NULL
UNION ALL
SELECT '#UsuarioTenant', CAST(COUNT(*) AS varchar(20)) FROM dbo.UsuarioTenant
UNION ALL
SELECT '#Accesos tenant-scope (activos)', CAST(COUNT(*) AS varchar(20))
    FROM dbo.Accesos a JOIN dbo.Roles r ON a.IdRol = r.Id JOIN dbo.Usuarios u ON a.IdUsuario = u.Id
    WHERE r.IdTenant IS NOT NULL AND u.Eliminado = 0
UNION ALL
SELECT '#Accesos platform-scope', CAST(COUNT(*) AS varchar(20))
    FROM dbo.Accesos a JOIN dbo.Roles r ON a.IdRol = r.Id WHERE r.IdTenant IS NULL;

-- ============================================
-- 12.8: Indices A1.1 siguen presentes
-- ============================================
PRINT '';
PRINT '--- 12.8: Indices A1.1 ---';

DECLARE @IdxCount int;
SELECT @IdxCount = COUNT(*) FROM sys.indexes WHERE name IN (
    'UX_UsuarioTenant_Usuario_Tenant',
    'UX_UsuarioTenant_Id_IdUsuario',
    'UX_Accesos_Tenant_UsrAppRol',
    'UX_Accesos_Platform_UsrAppRol',
    'FK_Accesos_UsuarioTenant'
);

IF @IdxCount = 5
    PRINT 'PASS 12.8: Indices/constraints clave presentes';
ELSE
    PRINT 'WARN 12.8: Solo ' + CAST(@IdxCount AS varchar) + '/5 indices clave encontrados';

-- ============================================
-- Resumen
-- ============================================
PRINT '';
IF @Errors = 0
    PRINT '=== 012 POSTFLIGHT PASS: 0 errores. A1.2 COMPLETADO. ===';
ELSE
BEGIN
    PRINT '=== 012 POSTFLIGHT FAIL: ' + CAST(@Errors AS varchar) + ' error(es). ===';
    THROW 50001, '012 Postflight fallo. Revisar estado de la BD.', 1;
END


