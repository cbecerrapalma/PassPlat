-- ============================================================
-- FASE 16 ETAPA 12: Mejorar Auditoría (AudIdenExt)
-- Agrega 17 campos de auditoría extendida a la tabla AudIdenExt
-- ============================================================

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('AudIdenExt', 'U') IS NOT NULL
BEGIN
    -- TraceId - Distributed tracing
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'TraceId')
        ALTER TABLE dbo.AudIdenExt ADD TraceId NVARCHAR(100) NULL;

    -- SessionId - Sesion activa
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'SessionId')
        ALTER TABLE dbo.AudIdenExt ADD SessionId UNIQUEIDENTIFIER NULL;

    -- RefreshTokenId - Token used
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'RefreshTokenId')
        ALTER TABLE dbo.AudIdenExt ADD RefreshTokenId NVARCHAR(500) NULL;

    -- JwtId - JWT ID
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'JwtId')
        ALTER TABLE dbo.AudIdenExt ADD JwtId NVARCHAR(500) NULL;

    -- HttpStatus - HTTP status code
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'HttpStatus')
        ALTER TABLE dbo.AudIdenExt ADD HttpStatus INT NULL;

    -- TiempoRespuesta - ms
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'TiempoRespuesta')
        ALTER TABLE dbo.AudIdenExt ADD TiempoRespuesta INT NULL;

    -- Scopes - Scopes solicitados
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Scopes')
        ALTER TABLE dbo.AudIdenExt ADD Scopes NVARCHAR(2000) NULL;

    -- MetodoAutenticacion - Local/OAuth
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'MetodoAutenticacion')
        ALTER TABLE dbo.AudIdenExt ADD MetodoAutenticacion NVARCHAR(50) NULL;

    -- TipoLogin - Normal/MFA/Refresh
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'TipoLogin')
        ALTER TABLE dbo.AudIdenExt ADD TipoLogin NVARCHAR(50) NULL;

    -- Origen - API/UI/Mobile
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Origen')
        ALTER TABLE dbo.AudIdenExt ADD Origen NVARCHAR(50) NULL;

    -- Destino - Redirect URL
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Destino')
        ALTER TABLE dbo.AudIdenExt ADD Destino NVARCHAR(500) NULL;

    -- Codigo - Error code
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Codigo')
        ALTER TABLE dbo.AudIdenExt ADD Codigo NVARCHAR(100) NULL;

    -- Excepcion - Exception message
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Excepcion')
        ALTER TABLE dbo.AudIdenExt ADD Excepcion NVARCHAR(MAX) NULL;

    -- StackResumido - Stack trace summary
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'StackResumido')
        ALTER TABLE dbo.AudIdenExt ADD StackResumido NVARCHAR(MAX) NULL;

    -- IdDevice - FK Disp
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'IdDevice')
        ALTER TABLE dbo.AudIdenExt ADD IdDevice INT NULL;

    -- Browser - Browser name
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'Browser')
        ALTER TABLE dbo.AudIdenExt ADD Browser NVARCHAR(200) NULL;

    -- OS - Operating system
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AudIdenExt') AND name = 'OS')
        ALTER TABLE dbo.AudIdenExt ADD OS NVARCHAR(200) NULL;

    -- FK to Disp
    IF OBJECT_ID('Disp', 'U') IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AudIdenExt_Device')
    BEGIN
        ALTER TABLE dbo.AudIdenExt ADD CONSTRAINT FK_AudIdenExt_Device FOREIGN KEY (IdDevice) REFERENCES dbo.Disp(Id) ON DELETE NO ACTION;
    END

    PRINT 'FASE16 Etapa12: AudIdenExt extendida correctamente (17 campos)';
END
ELSE
    PRINT 'FASE16 Etapa12: SKIP - tabla AudIdenExt no existe';
GO

-- Indexes in a separate batch (columns must exist at parse time)
IF OBJECT_ID('AudIdenExt', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AudIdenExt_Usuario' AND object_id = OBJECT_ID('AudIdenExt'))
        CREATE INDEX IX_AudIdenExt_Usuario ON dbo.AudIdenExt(IdUsuario) WHERE IdUsuario IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AudIdenExt_Metodo' AND object_id = OBJECT_ID('AudIdenExt'))
        CREATE INDEX IX_AudIdenExt_Metodo ON dbo.AudIdenExt(MetodoAutenticacion) WHERE MetodoAutenticacion IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AudIdenExt_Origen' AND object_id = OBJECT_ID('AudIdenExt'))
        CREATE INDEX IX_AudIdenExt_Origen ON dbo.AudIdenExt(Origen) WHERE Origen IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AudIdenExt_Resultado' AND object_id = OBJECT_ID('AudIdenExt'))
        CREATE INDEX IX_AudIdenExt_Resultado ON dbo.AudIdenExt(Resultado) WHERE Resultado IS NOT NULL;

    PRINT 'FASE16 Etapa12: Indexes created';
END
GO
