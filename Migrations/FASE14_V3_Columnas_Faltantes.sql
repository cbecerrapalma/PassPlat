-- =============================================================================
-- FASE 14 V3: Columnas faltantes + SP multi-tabla (Desvincular)
-- =============================================================================
-- NOTA: CRUD individual se implementa via EF Core (RepositoryAsync<T>),
-- no via SPs individuales. Solo se crean SPs para operaciones multi-tabla
-- que afectan performance o requieren lógica transaccional compleja.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '=== FASE 14 V3: Columnas faltantes ===';
GO

-- =============================================================================
-- 1. ProvIden - Agregar columnas V3
-- =============================================================================
PRINT 'Agregando columnas V3 a ProvIden...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'Protocolo')
    ALTER TABLE dbo.ProvIden ADD Protocolo nvarchar(20) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'EndpointAutorizacion')
    ALTER TABLE dbo.ProvIden ADD EndpointAutorizacion nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'EndpointToken')
    ALTER TABLE dbo.ProvIden ADD EndpointToken nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'EndpointUserInfo')
    ALTER TABLE dbo.ProvIden ADD EndpointUserInfo nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'EndpointRevocacion')
    ALTER TABLE dbo.ProvIden ADD EndpointRevocacion nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'SoportaPKCE')
    ALTER TABLE dbo.ProvIden ADD SoportaPKCE bit NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'SoportaRefreshToken')
    ALTER TABLE dbo.ProvIden ADD SoportaRefreshToken bit NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ProvIden') AND name = 'SoportaMFA')
    ALTER TABLE dbo.ProvIden ADD SoportaMFA bit NOT NULL DEFAULT 0;
GO

-- Actualizar seed ProvIden con endpoints y capacidades V3
UPDATE dbo.ProvIden SET
    Protocolo = 'OIDC',
    EndpointAutorizacion = 'https://accounts.google.com/o/oauth2/v2/auth',
    EndpointToken = 'https://oauth2.googleapis.com/token',
    EndpointUserInfo = 'https://openidconnect.googleapis.com/v1/userinfo',
    EndpointRevocacion = 'https://oauth2.googleapis.com/revoke',
    SoportaPKCE = 1,
    SoportaRefreshToken = 1,
    SoportaMFA = 1
WHERE Codigo = 'GOOGLE';

UPDATE dbo.ProvIden SET
    Protocolo = 'OAuth2',
    EndpointAutorizacion = 'https://github.com/login/oauth/authorize',
    EndpointToken = 'https://github.com/login/oauth/access_token',
    EndpointUserInfo = 'https://api.github.com/user',
    SoportaPKCE = 0,
    SoportaRefreshToken = 0,
    SoportaMFA = 0
WHERE Codigo = 'GITHUB';

UPDATE dbo.ProvIden SET
    Protocolo = 'OIDC',
    EndpointAutorizacion = 'https://login.microsoftonline.com/common/oauth2/v2.0/authorize',
    EndpointToken = 'https://login.microsoftonline.com/common/oauth2/v2.0/token',
    EndpointUserInfo = 'https://graph.microsoft.com/oidc/userinfo',
    EndpointRevocacion = 'https://login.microsoftonline.com/common/oauth2/v2.0/logout',
    SoportaPKCE = 1,
    SoportaRefreshToken = 1,
    SoportaMFA = 1
WHERE Codigo = 'MICROSOFT';

UPDATE dbo.ProvIden SET
    Protocolo = 'OIDC',
    EndpointAutorizacion = 'https://appleid.apple.com/auth/authorize',
    EndpointToken = 'https://appleid.apple.com/auth/token',
    EndpointUserInfo = 'https://appleid.apple.com/auth/userinfo',
    SoportaPKCE = 1,
    SoportaRefreshToken = 1,
    SoportaMFA = 1
WHERE Codigo = 'APPLE';

UPDATE dbo.ProvIden SET
    Protocolo = 'OAuth2',
    EndpointAutorizacion = 'https://www.linkedin.com/oauth/v2/authorization',
    EndpointToken = 'https://www.linkedin.com/oauth/v2/accessToken',
    EndpointUserInfo = 'https://api.linkedin.com/v2/userinfo',
    SoportaPKCE = 1,
    SoportaRefreshToken = 1,
    SoportaMFA = 0
WHERE Codigo = 'LINKEDIN';

PRINT 'Seed ProvIden actualizado con endpoints V3';
GO

-- Extended properties V3
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Protocolo: OAuth2, OIDC, SAML2', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'Protocolo';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Endpoint de autorización OAuth/OIDC', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'EndpointAutorizacion';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Endpoint de intercambio de token', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'EndpointToken';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Endpoint de información de usuario', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'EndpointUserInfo';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Endpoint de revocación de token', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'EndpointRevocacion';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Soporta PKCE (Proof Key for Code Exchange)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'SoportaPKCE';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Soporta refresh tokens', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'SoportaRefreshToken';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Soporta MFA condicional del proveedor', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'SoportaMFA';
GO

-- =============================================================================
-- 2. ConfProvIden - Agregar columnas V3
-- =============================================================================
PRINT 'Agregando columnas V3 a ConfProvIden...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConfProvIden') AND name = 'RedirectUri')
    ALTER TABLE dbo.ConfProvIden ADD RedirectUri nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConfProvIden') AND name = 'Estado')
    ALTER TABLE dbo.ConfProvIden ADD Estado tinyint NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConfProvIden') AND name = 'Metadata')
    ALTER TABLE dbo.ConfProvIden ADD Metadata nvarchar(max) NULL;
GO

-- Actualizar RedirectUri = Callback para configs existentes
UPDATE dbo.ConfProvIden SET RedirectUri = Callback WHERE RedirectUri IS NULL;
GO

-- Actualizar Estado = 1 (Activo) para configs activas
UPDATE dbo.ConfProvIden SET Estado = 1 WHERE Estado IS NULL;
GO

-- Agregar constraint check para Estado
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE object_id = OBJECT_ID('CK_ConfProvIden_Estado'))
    ALTER TABLE dbo.ConfProvIden ADD CONSTRAINT CK_ConfProvIden_Estado CHECK (Estado BETWEEN 0 AND 2);
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'URL de redireccionamiento registrada en el proveedor (para validación de seguridad)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'RedirectUri';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Estado de la config: 0=Inactivo, 1=Activo, 2=Revocado', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'Estado';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'JSON con metadatos adicionales de configuración (endpoints personalizados, claims mapping, etc.)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'Metadata';
GO

-- =============================================================================
-- 3. IdenExt - Agregar columnas V3
-- =============================================================================
PRINT 'Agregando columnas V3 a IdenExt...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'ProviderUserName')
    ALTER TABLE dbo.IdenExt ADD ProviderUserName nvarchar(255) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'ClaimsJson')
    ALTER TABLE dbo.IdenExt ADD ClaimsJson nvarchar(max) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'AccessToken')
    ALTER TABLE dbo.IdenExt ADD AccessToken varbinary(2000) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'RefreshToken')
    ALTER TABLE dbo.IdenExt ADD RefreshToken varbinary(2000) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'IdToken')
    ALTER TABLE dbo.IdenExt ADD IdToken varbinary(3000) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'TokenExpiration')
    ALTER TABLE dbo.IdenExt ADD TokenExpiration datetime2 NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'CorrelationId')
    ALTER TABLE dbo.IdenExt ADD CorrelationId uniqueidentifier NULL;
GO

-- Copiar CorrelationId desde MetadataJson si existe (migración datos legacy)
UPDATE dbo.IdenExt
SET CorrelationId = TRY_CAST(JSON_VALUE(MetadataJson, '$.correlationId') AS uniqueidentifier)
WHERE CorrelationId IS NULL AND MetadataJson IS NOT NULL AND JSON_VALUE(MetadataJson, '$.correlationId') IS NOT NULL;
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Nombre de usuario en el proveedor externo (handle, alias)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'ProviderUserName';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Todos los claims del proveedor en JSON (para auditoría y mapeo)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'ClaimsJson';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'AccessToken cifrado (AES-256-GCM), NULL si GuardarTokens=0', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'AccessToken';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'RefreshToken cifrado (AES-256-GCM), NULL si GuardarTokens=0', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'RefreshToken';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'IdToken cifrado (AES-256-GCM) para OIDC, NULL si GuardarTokens=0', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'IdToken';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Fecha de expiración del AccessToken', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'TokenExpiration';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'CorrelationId para trazabilidad end-to-end del flujo OAuth', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'CorrelationId';
GO

PRINT '=== Columnas V3 agregadas exitosamente ===';
GO

-- =============================================================================
-- 4. SP_IdenExt_Desvincular - Soft-delete + revocar sesiones (multi-tabla)
-- =============================================================================
-- SP multi-tabla: afecta IdenExt + Sesiones.
-- CRUD individual se maneja via EF Core (RepositoryAsync<T>).
-- =============================================================================
PRINT 'Creando SP_IdenExt_Desvincular...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_IdenExt_Desvincular') AND type = 'P')
    DROP PROCEDURE dbo.SP_IdenExt_Desvincular;
GO

CREATE PROCEDURE dbo.SP_IdenExt_Desvincular
    @IdIdentidad        bigint,
    @IdUsuarioElimina   int,
    @RevocarSesiones    bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdUsuario int, @IdProvIden int;

    SELECT @IdUsuario = IdUsuario, @IdProvIden = IdProvIden
    FROM dbo.IdenExt
    WHERE Id = @IdIdentidad AND Eliminado = 0;

    IF @IdUsuario IS NULL
    BEGIN
        RAISERROR('Identidad externa no encontrada o ya eliminada', 16, 1);
        RETURN;
    END

    -- Soft-delete lógico
    UPDATE dbo.IdenExt
    SET Eliminado = 1, Activo = 0, FecEliminacion = SYSUTCDATETIME(),
        IdUsuarioElimina = @IdUsuarioElimina, FecMod = SYSUTCDATETIME()
    WHERE Id = @IdIdentidad;

    -- Revocar todas las sesiones del usuario si se solicita
    IF @RevocarSesiones = 1
    BEGIN
        UPDATE dbo.Sesiones
        SET EsActiva = 0
        WHERE IdUsuario = @IdUsuario AND EsActiva = 1;
    END

    SELECT @@ROWCOUNT AS FilasAfectadas;
END
GO

PRINT '=== FASE 14 V3: Migración completada exitosamente ===';
GO
