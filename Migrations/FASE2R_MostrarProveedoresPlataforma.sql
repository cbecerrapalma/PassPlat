-- FASE 2R: Campo configurable para heredar proveedores OAuth de la plataforma
-- Cuando un tenant no tiene proveedores propios válidos, este setting decide si
-- se muestran los proveedores de la plataforma (tenant PLATFORM).
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM ConfigApp WHERE Grupo = 'OAuth' AND Clave = 'MostrarProveedoresPlataforma' AND IdTenant IS NULL)
BEGIN
    INSERT INTO ConfigApp (IdTenant, Grupo, Clave, Valor, Tipo, Descripcion, Activo, FecCrea)
    VALUES (NULL, 'OAuth', 'MostrarProveedoresPlataforma', 'true', 'bool',
            'Muestra los proveedores OAuth de la plataforma en tenants sin proveedores propios',
            1, SYSDATETIME());
    PRINT 'ConfigApp MostrarProveedoresPlataforma insertado (true).';
END
ELSE
BEGIN
    PRINT 'ConfigApp MostrarProveedoresPlataforma ya existe.';
END
