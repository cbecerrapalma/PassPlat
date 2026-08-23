-- Script to update database indexes to allow email reuse for deleted users
-- Run this on the PASSWORDS database after the application starts

USE PASSWORDS;
GO

-- Check if the existing unique indexes exist
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuarios_TenantEmail')
BEGIN
    -- Drop the existing unique index
    DROP INDEX UX_Usuarios_TenantEmail ON dbo.Usuarios;
END

-- Create new filtered unique index for email (excludes eliminated users)
CREATE UNIQUE INDEX UX_Usuarios_TenantEmail ON dbo.Usuarios (IdTenant, Email)
WHERE Eliminado = 0;
GO

-- Repeat for NomUsuario index if it exists and needs to be updated
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuarios_TenantNomUsuario')
BEGIN
    DROP INDEX UX_Usuarios_TenantNomUsuario ON dbo.Usuarios;
END

CREATE UNIQUE INDEX UX_Usuarios_TenantNomUsuario ON dbo.Usuarios (IdTenant, NomUsuario)
WHERE Eliminado = 0;
GO

-- Verify the indexes were created correctly
SELECT 
    i.name AS IndexName,
    i.is_unique AS IsUnique,
    i.filter_definition AS FilterDefinition
FROM sys.indexes i
INNER JOIN sys.objects o ON i.object_id = o.object_id
WHERE o.name = 'Usuarios' 
    AND (i.name = 'UX_Usuarios_TenantEmail' OR i.name = 'UX_Usuarios_TenantNomUsuario')
ORDER BY i.name;
GO

PRINT 'Database indexes updated successfully!';
PRINT 'Users with Eliminado=1 will no longer block email reuse for new users.';
