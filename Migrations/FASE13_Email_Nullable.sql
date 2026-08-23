-- ============================================================
-- MIGRACIÓN FASE 13: Permitir usuarios sin Email
-- Ejecutar EN ORDEN en la base de datos PASSWORDS
-- ============================================================

-- 0. Verificar estado actual
-- SELECT name, is_unique, filter_definition 
-- FROM sys.indexes 
-- WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name LIKE '%Email%';

-- 1. Eliminar índices únicos globales existentes (no permiten múltiples NULLs)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Usuarios_Email' AND object_id = OBJECT_ID('dbo.Usuarios'))
BEGIN
    DROP INDEX UQ_Usuarios_Email ON dbo.Usuarios;
    PRINT 'Índice UQ_Usuarios_Email eliminado';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuarios_Tenant_Email' AND object_id = OBJECT_ID('dbo.Usuarios'))
BEGIN
    DROP INDEX UX_Usuarios_Tenant_Email ON dbo.Usuarios;
    PRINT 'Índice UX_Usuarios_Tenant_Email eliminado';
END

-- 2. Hacer Email NULLable
-- Verificar si ya es nullable
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.Usuarios') 
    AND name = 'Email' 
    AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.Usuarios
    ALTER COLUMN Email nvarchar(255) NULL;
    PRINT 'Columna Email cambiada a NULL';
END
ELSE
BEGIN
    PRINT 'Columna Email ya es NULL';
END

-- 3. Crear índice único filtrado por Tenant + Email (solo no nulos y no eliminados)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuarios_TenantEmail' AND object_id = OBJECT_ID('dbo.Usuarios'))
BEGIN
    CREATE UNIQUE INDEX UX_Usuarios_TenantEmail
    ON dbo.Usuarios (IdTenant, Email)
    WHERE (Eliminado = 0 AND Email IS NOT NULL);
    PRINT 'Índice filtrado UX_Usuarios_TenantEmail creado';
END
ELSE
BEGIN
    PRINT 'Índice UX_Usuarios_TenantEmail ya existe';
END

-- 4. Agregar constraint: Si EmailVerificado=1 => Email NOT NULL
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Usuarios_EmailVerificado_RequiereEmail' AND parent_object_id = OBJECT_ID('dbo.Usuarios'))
BEGIN
    ALTER TABLE dbo.Usuarios
    ADD CONSTRAINT CK_Usuarios_EmailVerificado_RequiereEmail
    CHECK (EmailVerificado = 0 OR Email IS NOT NULL);
    PRINT 'Constraint CK_Usuarios_EmailVerificado_RequiereEmail agregado';
END
ELSE
BEGIN
    PRINT 'Constraint CK_Usuarios_EmailVerificado_RequiereEmail ya existe';
END

-- 5. Verificar/Actualizar SP_Usuario_Crear para aceptar Email NULL
-- NOTA: Revisar el SP real en PASSWORDS SP.sql y ajustar según su nombre real
-- El SP debe:
--   - Aceptar @Email nvarchar(255) = NULL
--   - NO validar UNIQUE si Email IS NULL (el índice filtrado lo maneja)
--   - Insertar NULL (no empty string) cuando no se proporcione email

-- 6. Verificar trigger TR_Usuarios_Mod - ya compatible con NULL
-- El trigger actual:
--   IF UPDATE(Nombre) OR UPDATE(Apellido) OR UPDATE(IdEstado) OR UPDATE(Email) OR UPDATE(IdTento)
-- Funciona correctamente con NULL. No requiere cambios.

-- 7. Actualizar estadísticas
UPDATE STATISTICS dbo.Usuarios;

-- 8. Verificación post-migración
PRINT '=== VERIFICACIÓN POST-MIGRACIÓN ===';
SELECT 
    c.name AS Columna,
    t.name AS Tipo,
    c.max_length,
    c.is_nullable,
    CASE WHEN c.is_nullable = 1 THEN 'NULL' ELSE 'NOT NULL' END AS Nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Usuarios') AND c.name = 'Email';

SELECT 
    i.name AS Indice,
    i.is_unique,
    i.filter_definition
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.Usuarios') 
AND (i.name LIKE '%Email%' OR i.name LIKE '%TenantEmail%');

SELECT 
    name AS [Constraint],
    definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Usuarios')
AND name = 'CK_Usuarios_EmailVerificado_RequiereEmail';

PRINT 'Migración completada. Verificar resultados arriba.';