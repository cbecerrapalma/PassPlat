-- ============================================================
-- MIGRACIÓN: Normalización de Stored Procedures a HORA LOCAL
-- Reemplaza SYSUTCDATETIME() (UTC) -> SYSDATETIME() (local)
-- en TODOS los SP existentes, preservando el resto del body.
-- Alineado con: defaults sysdatetime() + DateTime.Now en app.
-- Ejecutar EN ORDEN en la base de datos PassPlat.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @name nvarchar(255);
DECLARE @def   nvarchar(max);
DECLARE @newDef nvarchar(max);
DECLARE @count INT = 0;
DECLARE @sql   nvarchar(max);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
SELECT s.name, sm.definition
FROM sys.procedures s
JOIN sys.sql_modules sm ON sm.object_id = s.object_id
WHERE sm.definition LIKE '%SYSUTCDATETIME%'
ORDER BY s.name;

OPEN cur;
FETCH NEXT FROM cur INTO @name, @def;

RAISERROR('=== NORMALIZACION SPs SYSUTCDATETIME() -> SYSDATETIME() ===', 0, 1) WITH NOWAIT;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- SQL Server es case-insensitive para palabras reservadas pero REPLACE es case-sensitive.
    -- Aplicar todos los casings para cubrir cualquier variante legada.
    SET @newDef = REPLACE(@def, 'SYSUTCDATETIME', 'SYSDATETIME');
    SET @newDef = REPLACE(@newDef, 'SysUtcDateTime', 'SysDateTime');
    SET @newDef = REPLACE(@newDef, 'sysutcdatetime', 'sysdatetime');

    -- Conservar solo desde el primer 'CREATE PROCEDURE' real (descarta comentarios/extensiones previos)
    DECLARE @pos INT = PATINDEX('%CREATE%PROCEDURE%', @newDef);
    IF @pos > 0
        SET @newDef = SUBSTRING(@newDef, @pos, LEN(@newDef) - @pos + 1);

    -- Los SP ya existen -> ALTER PROCEDURE para actualizar sin duplicar
    SET @newDef = STUFF(@newDef, 1, 6, 'ALTER ');

    SET @sql = @newDef;
    BEGIN TRY
        EXEC sp_executesql @sql;
        SET @count = @count + 1;
        PRINT '  [' + @name + '] OK';
    END TRY
    BEGIN CATCH
        PRINT '  [' + @name + '] ERROR: ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM cur INTO @name, @def;
END

CLOSE cur;
DEALLOCATE cur;

PRINT '=== SPs normalizados: ' + CAST(@count AS varchar(10)) + ' ===';

-- Verificación final: SPs que aún referencian el reloj UTC
PRINT '=== VERIFICACION POST-MIGRACION ===';
SELECT s.name AS SP
FROM sys.procedures s
JOIN sys.sql_modules sm ON sm.object_id = s.object_id
WHERE sm.definition LIKE '%SYSUTCDATETIME%'
   OR sm.definition LIKE '%GETUTCDATE%'
ORDER BY s.name;

PRINT 'Migración completada.';