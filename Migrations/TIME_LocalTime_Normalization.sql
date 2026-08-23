-- ============================================================
-- MIGRACIÓN: Normalización de timestamps a HORA LOCAL
-- Objetivo: Unificar todas las columnas de fecha a hora LOCAL
--           (sysdatetime()), eliminando la mezcla con UTC
--           (sysutcdatetime()) que producía diferencias horarias
--           respecto al sistema operativo.
-- Ejecutar EN ORDEN en la base de datos PassPlat.
-- Idempotente: se puede re-ejecutar sin efectos secundarios.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @sql nvarchar(max);
DECLARE @tabla sysname;
DECLARE @columna sysname;
DECLARE @defName sysname;

-- 1. Cursor sobre todos los default constraints con sysutcdatetime()
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
SELECT
    t.name AS Tabla,
    c.name AS Columna,
    dc.name AS DefaultName
FROM sys.default_constraints dc
JOIN sys.tables t ON dc.parent_object_id = t.object_id
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.definition LIKE '%sysutcdatetime%'
ORDER BY t.name;

OPEN cur;
FETCH NEXT FROM cur INTO @tabla, @columna, @defName;

PRINT '=== NORMALIZACIÓN sysutcdatetime() -> sysdatetime() ===';
DECLARE @count INT = 0;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tabla) + N' DROP CONSTRAINT ' + QUOTENAME(@defName) + N';'
    EXEC sp_executesql @sql;

    SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tabla) + N' ADD CONSTRAINT ' + QUOTENAME(@defName) + N' DEFAULT (sysdatetime()) FOR ' + QUOTENAME(@columna) + N';'
    EXEC sp_executesql @sql;

    SET @count = @count + 1;
    PRINT '  [' + @tabla + '].[' + @columna + ']  OK';
    FETCH NEXT FROM cur INTO @tabla, @columna, @defName;
END

CLOSE cur;
DEALLOCATE cur;

PRINT '=== Constraints normalizados: ' + CAST(@count AS varchar(10)) + ' ===';

-- 2. Verificación: ¿quedan defaults UTC?
PRINT '=== VERIFICACIÓN POST-MIGRACIÓN ===';
SELECT
    t.name AS Tabla,
    c.name AS Columna,
    dc.name AS DefaultName,
    dc.definition AS Definition
FROM sys.default_constraints dc
JOIN sys.tables t ON dc.parent_object_id = t.object_id
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.definition LIKE '%utc%'
ORDER BY t.name;

PRINT 'Migración completada.';
