-- ============================================================================
-- FASE 16: Model Improvement + Provider Changes
-- Date: 2026-07-06
-- Purpose: Add OrigenRegistro to HistorialPwd, update providers
-- ============================================================================

SET NOCOUNT ON;
GO

-- ============================================================================
-- 1. Add OrigenRegistro to HistorialPwd
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HistorialPwd') AND name = 'OrigenRegistro')
BEGIN
    ALTER TABLE dbo.HistorialPwd ADD OrigenRegistro NVARCHAR(20) NOT NULL 
        CONSTRAINT DF_HistorialPwd_Origen DEFAULT 'LOCAL';
    
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Origen de la contraseña: LOCAL (cambio voluntario), RESET (recuperación), LDAP (sincronización LDAP/AD), SAML (federación SAML), PRIMER_USO (contraseña temporal)', 'SCHEMA', N'dbo', 'TABLE', N'HistorialPwd', 'COLUMN', N'OrigenRegistro';
    
    CREATE NONCLUSTERED INDEX IX_HistorialPwd_Origen ON dbo.HistorialPwd(OrigenRegistro);
    
    PRINT 'OrigenRegistro column added to HistorialPwd';
END
ELSE
    PRINT 'OrigenRegistro column already exists';
GO

-- ============================================================================
-- 2. Update ProvIden — Remove Microsoft/Apple, Add Instagram/Facebook
-- ============================================================================

-- Deactivate Microsoft and Apple providers
UPDATE dbo.ProvIden SET Activo = 0 WHERE Codigo IN ('MICROSOFT', 'APPLE');

-- Insert Instagram provider (if not exists)
IF NOT EXISTS (SELECT * FROM dbo.ProvIden WHERE Codigo = 'INSTAGRAM')
BEGIN
    INSERT INTO dbo.ProvIden (Codigo, Nombre, TipoProveedor, Protocolo, EndpointAutorizacion, EndpointToken, EndpointUserInfo, SoportaPKCE, SoportaRefreshToken, SoportaMFA, Icono, Orden, Activo, FecCrea)
    VALUES ('INSTAGRAM', 'Instagram', 1, 'OAuth2', 
            'https://api.instagram.com/oauth/authorize', 
            'https://api.instagram.com/oauth/access_token', 
            'https://graph.instagram.com/me', 
            1, 0, 0, 'camera_alt', 6, 1, GETUTCDATE());
    PRINT 'Instagram provider inserted';
END
ELSE
    PRINT 'Instagram provider already exists';

-- Insert Facebook provider (if not exists)
IF NOT EXISTS (SELECT * FROM dbo.ProvIden WHERE Codigo = 'FACEBOOK')
BEGIN
    INSERT INTO dbo.ProvIden (Codigo, Nombre, TipoProveedor, Protocolo, EndpointAutorizacion, EndpointToken, EndpointUserInfo, SoportaPKCE, SoportaRefreshToken, SoportaMFA, Icono, Orden, Activo, FecCrea)
    VALUES ('FACEBOOK', 'Facebook', 1, 'OAuth2', 
            'https://www.facebook.com/v18.0/dialog/oauth', 
            'https://graph.facebook.com/v18.0/oauth/access_token', 
            'https://graph.facebook.com/v18.0/me', 
            1, 0, 0, 'facebook', 7, 1, GETUTCDATE());
    PRINT 'Facebook provider inserted';
END
ELSE
    PRINT 'Facebook provider already exists';
GO

-- ============================================================================
-- 3. Update existing HistorialPwd records to set default OrigenRegistro
-- ============================================================================
UPDATE dbo.HistorialPwd SET OrigenRegistro = 'LOCAL' WHERE OrigenRegistro IS NULL;

PRINT N'FASE 16 complete: OrigenRegistro added, providers updated.';
GO
