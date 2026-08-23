-- ============================================================
-- FASE 16: Rename subsystem tables
-- Migration: CREATE NEW -> Migrate Data -> DROP OLD
-- Complete script with Extended Properties, FK handling,
-- transaction protection, idempotency guards.
-- ============================================================
-- Correct order: IdenExt first (still references EstadosIdentidadExterna
-- via FK), then EstIdenExt (now table referencing it is gone).
-- Drop blocking FKs before DROP OLD, recreate them at the end.
-- ============================================================

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- STEP 0: Drop FKs that would block DROP TABLE
-- ============================================================
IF OBJECT_ID('IdentidadesExternas', 'U') IS NOT NULL
    AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExt_Estado')
    ALTER TABLE dbo.IdentidadesExternas DROP CONSTRAINT FK_IdenExt_Estado;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UsuariosPermisos_TipoAsig')
    ALTER TABLE dbo.UsuariosPermisos DROP CONSTRAINT FK_UsuariosPermisos_TipoAsig;
GO

-- ============================================================
-- STEP 1: IdentidadesExternas -> IdenExt
-- ============================================================
IF OBJECT_ID('IdentidadesExternas', 'U') IS NOT NULL AND OBJECT_ID('IdenExt', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.IdenExt (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        IdUsuario INT NOT NULL,
        IdProvIden INT NOT NULL,
        IdTenant INT NOT NULL,
        SubExterno NVARCHAR(500) NOT NULL,
        EmailExterno NVARCHAR(255) NULL,
        NombreExterno NVARCHAR(255) NULL,
        Avatar NVARCHAR(1000) NULL,
        ProviderUserName NVARCHAR(255) NULL,
        ClaimsJson NVARCHAR(MAX) NULL,
        MetadataJson NVARCHAR(MAX) NULL,
        AccessToken NVARCHAR(MAX) NULL,
        RefreshToken NVARCHAR(MAX) NULL,
        IdToken NVARCHAR(MAX) NULL,
        TokenExpiration DATETIME2 NULL,
        EsPrincipal BIT NOT NULL DEFAULT 0,
        Activo BIT NOT NULL DEFAULT 1,
        Eliminado BIT NOT NULL DEFAULT 0,
        FecEliminacion DATETIME2 NULL,
        IdUsuarioElimina INT NULL,
        UltimoLogin DATETIME2 NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        IdEstado TINYINT NULL,
        Scopes NVARCHAR(1000) NULL,
        UltimaIP NVARCHAR(45) NULL,
        UltimoDisp INT NULL,
        UltimoUserAgent NVARCHAR(500) NULL,
        UltimoTenant INT NULL,
        FecRevocacion DATETIME2 NULL,
        IdUsuarioRevoca INT NULL,
        MotivoRevocacion NVARCHAR(500) NULL,
        FecCrea DATETIME2 NULL DEFAULT sysutcdatetime(),
        FecMod DATETIME2 NULL,
        CONSTRAINT PK_IdenExt PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_IdenExt_ProveedorSub UNIQUE (IdProvIden, SubExterno),
        CONSTRAINT CK_IdenExt_Sub CHECK (len(SubExterno) > 0)
    );

    SET IDENTITY_INSERT dbo.IdenExt ON;
    INSERT INTO dbo.IdenExt (
        Id,IdUsuario,IdProvIden,IdTenant,SubExterno,
        EmailExterno,NombreExterno,Avatar,ProviderUserName,ClaimsJson,
        MetadataJson,AccessToken,RefreshToken,IdToken,TokenExpiration,
        EsPrincipal,Activo,Eliminado,FecEliminacion,IdUsuarioElimina,
        UltimoLogin,CorrelationId,IdEstado,Scopes,UltimaIP,
        UltimoDisp,UltimoUserAgent,UltimoTenant,FecRevocacion,IdUsuarioRevoca,
        MotivoRevocacion,FecCrea,FecMod
    )
    SELECT
        Id,IdUsuario,IdProvIden,IdTenant,SubExterno,
        EmailExterno,NombreExterno,Avatar,ProviderUserName,ClaimsJson,
        MetadataJson,
        CONVERT(NVARCHAR(MAX), AccessToken),
        CONVERT(NVARCHAR(MAX), RefreshToken),
        CONVERT(NVARCHAR(MAX), IdToken),
        TokenExpiration,
        EsPrincipal,Activo,Eliminado,FecEliminacion,IdUsuarioElimina,
        UltimoLogin,CorrelationId,IdEstado,Scopes,UltimaIP,
        UltimoDisp,UltimoUserAgent,UltimoTenant,FecRevocacion,IdUsuarioRevoca,
        MotivoRevocacion,FecCrea,FecMod
    FROM dbo.IdentidadesExternas;
    SET IDENTITY_INSERT dbo.IdenExt OFF;

    CREATE INDEX IX_IdenExt_Email ON dbo.IdenExt(EmailExterno) WHERE EmailExterno IS NOT NULL;
    CREATE INDEX IX_IdenExt_UltimoLogin ON dbo.IdenExt(UltimoLogin DESC) WHERE UltimoLogin IS NOT NULL;
    CREATE INDEX IX_IdenExt_Usuario ON dbo.IdenExt(IdUsuario);
    CREATE INDEX IX_IdenExt_Estado ON dbo.IdenExt(IdEstado) WHERE IdEstado IS NOT NULL;

    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identidades externas vinculadas a usuarios del sistema.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico de la identidad externa (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Id';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario local vinculado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al tenant.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdTenant';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del usuario en el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'SubExterno';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Email proporcionado por el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'EmailExterno';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del usuario en el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'NombreExterno';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'URL del avatar del usuario externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Avatar';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al estado actual de la identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdEstado';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Scopes autorizados por el proveedor.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Scopes';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indica si es la identidad principal del usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'EsPrincipal';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de la tabla IdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_IdenExt';

    DROP TABLE dbo.IdentidadesExternas;
    COMMIT TRANSACTION;
    PRINT 'IdenExt: OK';
END
ELSE
    PRINT 'IdenExt: SKIP';
GO

-- ============================================================
-- STEP 2: EstadosIdentidadExterna -> EstIdenExt
--         Now IdentidadesExternas is gone, so no FK blocks DROP.
-- ============================================================
IF OBJECT_ID('EstadosIdentidadExterna', 'U') IS NOT NULL AND OBJECT_ID('EstIdenExt', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.EstIdenExt (
        Id TINYINT NOT NULL,
        Nombre NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        Color NVARCHAR(20) NULL,
        Orden SMALLINT NOT NULL DEFAULT 0,
        Activo BIT NOT NULL DEFAULT 1,
        FecCrea DATETIME2 NULL DEFAULT sysutcdatetime(),
        FecMod DATETIME2 NULL,
        CONSTRAINT PK_EstIdenExt PRIMARY KEY (Id),
        CONSTRAINT UK_EstIdenExt_Nombre UNIQUE (Nombre)
    );

    INSERT INTO dbo.EstIdenExt (Id,Nombre,Descripcion,Color,Orden,Activo,FecCrea,FecMod)
        SELECT Id,Nombre,Descripcion,Color,Orden,Activo,FecCrea,FecMod FROM dbo.EstadosIdentidadExterna;

    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Catalogo de estados de identidad externa.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del estado (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Id';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del estado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Nombre';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Color hexadecimal para representacion visual.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Color';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Orden de visualizacion.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Orden';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de EstIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_EstIdenExt';

    DROP TABLE dbo.EstadosIdentidadExterna;
    COMMIT TRANSACTION;
    PRINT 'EstIdenExt: OK';
END
ELSE
    PRINT 'EstIdenExt: SKIP';
GO

-- ============================================================
-- STEP 3: AuditoriaIdentidadExterna -> AudIdenExt
-- ============================================================
IF OBJECT_ID('AuditoriaIdentidadExterna', 'U') IS NOT NULL AND OBJECT_ID('AudIdenExt', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.AudIdenExt (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        IdTenant INT NOT NULL,
        IdProvIden INT NOT NULL,
        IdUsuario INT NOT NULL,
        SubExterno NVARCHAR(500) NULL,
        Evento NVARCHAR(100) NOT NULL,
        Resultado NVARCHAR(100) NULL,
        Detalle NVARCHAR(MAX) NULL,
        IP NVARCHAR(45) NULL,
        UserAgent NVARCHAR(500) NULL,
        CorrelationId NVARCHAR(100) NULL,
        FecEvento DATETIME2 NOT NULL DEFAULT sysutcdatetime(),
        CONSTRAINT PK_AudIdenExt PRIMARY KEY CLUSTERED (Id)
    );

    SET IDENTITY_INSERT dbo.AudIdenExt ON;
    INSERT INTO dbo.AudIdenExt (Id,IdTenant,IdProvIden,IdUsuario,SubExterno,Evento,Resultado,Detalle,IP,UserAgent,CorrelationId,FecEvento)
        SELECT Id,IdTenant,IdProvIden,IdUsuario,SubExterno,Evento,Resultado,Detalle,IP,UserAgent,CorrelationId,FecEvento
        FROM dbo.AuditoriaIdentidadExterna;
    SET IDENTITY_INSERT dbo.AudIdenExt OFF;

    CREATE INDEX IX_AudIdenExt_TenantFecha ON dbo.AudIdenExt(IdTenant, FecEvento DESC);
    CREATE INDEX IX_AudIdenExt_ProvIden ON dbo.AudIdenExt(IdProvIden, FecEvento DESC);
    CREATE INDEX IX_AudIdenExt_CorrelationId ON dbo.AudIdenExt(CorrelationId) WHERE CorrelationId IS NOT NULL;

    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Registro de auditoria para eventos de identidad externa.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico del registro de auditoria (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'Id';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al tenant.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdTenant';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del evento de auditoria.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'Evento';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de AudIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_AudIdenExt';

    DROP TABLE dbo.AuditoriaIdentidadExterna;
    COMMIT TRANSACTION;
    PRINT 'AudIdenExt: OK';
END
ELSE
    PRINT 'AudIdenExt: SKIP';
GO

-- ============================================================
-- STEP 4: HistorialIdentidadExterna -> HistorialIdenExt
-- ============================================================
IF OBJECT_ID('HistorialIdentidadExterna', 'U') IS NOT NULL AND OBJECT_ID('HistorialIdenExt', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.HistorialIdenExt (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        IdTenant INT NOT NULL,
        IdUsuario INT NOT NULL,
        IdIdentidadExterna BIGINT NOT NULL,
        IdProvIden INT NOT NULL,
        TipoCambio NVARCHAR(100) NOT NULL,
        ValorAnterior NVARCHAR(1000) NULL,
        ValorNuevo NVARCHAR(1000) NULL,
        RealizadoPor INT NULL,
        EsAutomatico BIT NOT NULL DEFAULT 0,
        CorrelationId UNIQUEIDENTIFIER NULL,
        FecCambio DATETIME2 NOT NULL DEFAULT sysutcdatetime(),
        CONSTRAINT PK_HistorialIdenExt PRIMARY KEY (Id)
    );

    SET IDENTITY_INSERT dbo.HistorialIdenExt ON;
    INSERT INTO dbo.HistorialIdenExt (Id,IdTenant,IdUsuario,IdIdentidadExterna,IdProvIden,TipoCambio,ValorAnterior,ValorNuevo,RealizadoPor,EsAutomatico,CorrelationId,FecCambio)
        SELECT Id,IdTenant,IdUsuario,IdIdentidadExterna,IdProvIden,TipoCambio,ValorAnterior,ValorNuevo,RealizadoPor,EsAutomatico,CorrelationId,FecCambio
        FROM dbo.HistorialIdentidadExterna;
    SET IDENTITY_INSERT dbo.HistorialIdenExt OFF;

    CREATE INDEX IX_HistorialIdenExt_Usuario ON dbo.HistorialIdenExt(IdUsuario);
    CREATE INDEX IX_HistorialIdenExt_Identidad ON dbo.HistorialIdenExt(IdIdentidadExterna);
    CREATE INDEX IX_HistorialIdenExt_FecCambio ON dbo.HistorialIdenExt(FecCambio DESC);
    CREATE INDEX IX_HistorialIdenExt_Tenant ON dbo.HistorialIdenExt(IdTenant);

    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Historial de cambios de identidades externas.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico del registro historico (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'Id';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK a la identidad externa.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdIdentidadExterna';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Tipo de cambio realizado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'TipoCambio';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de HistorialIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_HistorialIdenExt';

    DROP TABLE dbo.HistorialIdentidadExterna;
    COMMIT TRANSACTION;
    PRINT 'HistorialIdenExt: OK';
END
ELSE
    PRINT 'HistorialIdenExt: SKIP';
GO

-- ============================================================
-- STEP 5: TipoAsignacionPermiso -> TipAsigPermiso
--         Note: source has ONLY Id(tinyint),Nombre(varchar) - NO Descripcion
-- ============================================================
IF OBJECT_ID('TipoAsignacionPermiso', 'U') IS NOT NULL AND OBJECT_ID('TipAsigPermiso', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.TipAsigPermiso (
        Id TINYINT NOT NULL,
        Nombre VARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        CONSTRAINT PK_TipAsigPermiso PRIMARY KEY CLUSTERED (Id)
    );

    INSERT INTO dbo.TipAsigPermiso (Id, Nombre, Descripcion)
        SELECT Id, Nombre, NULL FROM dbo.TipoAsignacionPermiso;

    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Tipo de asignacion de permiso.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del tipo de asignacion (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'COLUMN',@level2name=N'Id';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del tipo de asignacion.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'COLUMN',@level2name=N'Nombre';
    EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de TipAsigPermiso.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'CONSTRAINT',@level2name=N'PK_TipAsigPermiso';

    DROP TABLE dbo.TipoAsignacionPermiso;
    COMMIT TRANSACTION;
    PRINT 'TipAsigPermiso: OK';
END
ELSE
    PRINT 'TipAsigPermiso: SKIP';
GO

-- ============================================================
-- STEP 6: Add FKs that reference new tables (wrapped in transaction)
-- ============================================================
BEGIN TRANSACTION;

-- IdenExt -> EstIdenExt
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL AND OBJECT_ID('EstIdenExt', 'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExt_Estado')
BEGIN
    ALTER TABLE dbo.IdenExt ADD CONSTRAINT FK_IdenExt_Estado FOREIGN KEY (IdEstado) REFERENCES dbo.EstIdenExt(Id) ON DELETE NO ACTION;
    PRINT 'FK_IdenExt_Estado: OK';
END

-- IdenExt -> ProvIden
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExt_ProvIden')
    ALTER TABLE dbo.IdenExt ADD CONSTRAINT FK_IdenExt_ProvIden FOREIGN KEY (IdProvIden) REFERENCES dbo.ProvIden(Id) ON DELETE NO ACTION;

-- IdenExt -> Tenant
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExt_Tenant')
    ALTER TABLE dbo.IdenExt ADD CONSTRAINT FK_IdenExt_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION;

-- IdenExt -> Usuario
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExt_Usuario')
    ALTER TABLE dbo.IdenExt ADD CONSTRAINT FK_IdenExt_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id) ON DELETE NO ACTION;

-- HistorialIdenExt -> IdenExt
IF OBJECT_ID('HistorialIdenExt', 'U') IS NOT NULL AND OBJECT_ID('IdenExt', 'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HistorialIdenExt_Identidad')
BEGIN
    ALTER TABLE dbo.HistorialIdenExt ADD CONSTRAINT FK_HistorialIdenExt_Identidad FOREIGN KEY (IdIdentidadExterna) REFERENCES dbo.IdenExt(Id) ON DELETE NO ACTION;
    PRINT 'FK_HistorialIdenExt_Identidad: OK';
END

-- HistorialIdenExt -> Tenant
IF OBJECT_ID('HistorialIdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HistorialIdenExt_Tenant')
    ALTER TABLE dbo.HistorialIdenExt ADD CONSTRAINT FK_HistorialIdenExt_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id) ON DELETE NO ACTION;

-- HistorialIdenExt -> Usuario
IF OBJECT_ID('HistorialIdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HistorialIdenExt_Usuario')
    ALTER TABLE dbo.HistorialIdenExt ADD CONSTRAINT FK_HistorialIdenExt_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id) ON DELETE NO ACTION;

-- HistorialIdenExt -> ProvIden
IF OBJECT_ID('HistorialIdenExt', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HistorialIdenExt_ProvIden')
    ALTER TABLE dbo.HistorialIdenExt ADD CONSTRAINT FK_HistorialIdenExt_ProvIden FOREIGN KEY (IdProvIden) REFERENCES dbo.ProvIden(Id) ON DELETE NO ACTION;

-- UsuariosPermisos -> TipAsigPermiso (recreate FK after old table was dropped)
IF OBJECT_ID('UsuariosPermisos', 'U') IS NOT NULL AND OBJECT_ID('TipAsigPermiso', 'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_UsuariosPermisos_TipoAsig')
BEGIN
    ALTER TABLE dbo.UsuariosPermisos ADD CONSTRAINT FK_UsuariosPermisos_TipoAsig FOREIGN KEY (IdTipoAsig) REFERENCES dbo.TipAsigPermiso(Id) ON DELETE NO ACTION;
    PRINT 'FK_UsuariosPermisos_TipoAsig: OK';
END

COMMIT TRANSACTION;
PRINT '=== FASE 16 RENAME TABLES COMPLETE ===';
GO

-- ============================================================
-- STEP 7: Ensure all Extended Properties exist (safe for rerun)
-- ============================================================
-- IdenExt column + PK EPs
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico de la identidad externa (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Id'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario local vinculado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al tenant.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdTenant'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del usuario en el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'SubExterno'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Email proporcionado por el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'EmailExterno'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del usuario en el proveedor externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'NombreExterno'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'URL del avatar del usuario externo.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Avatar'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al estado actual de la identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'IdEstado'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Scopes autorizados por el proveedor.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'Scopes'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indica si es la identidad principal del usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'COLUMN',@level2name=N'EsPrincipal'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de la tabla IdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_IdenExt'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
-- EstIdenExt column + PK EPs
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del estado (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Id'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del estado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Nombre'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Color hexadecimal para representacion visual.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Color'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Orden de visualizacion.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'COLUMN',@level2name=N'Orden'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de EstIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EstIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_EstIdenExt'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
-- AudIdenExt column + PK EPs
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico del registro de auditoria (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'Id'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al tenant.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdTenant'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del evento de auditoria.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'COLUMN',@level2name=N'Evento'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de AudIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AudIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_AudIdenExt'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
-- HistorialIdenExt column + PK EPs
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador unico del registro historico (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'Id'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al usuario.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdUsuario'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK a la identidad externa.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdIdentidadExterna'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK al proveedor de identidad.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'IdProvIden'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Tipo de cambio realizado.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'COLUMN',@level2name=N'TipoCambio'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de HistorialIdenExt.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'HistorialIdenExt', @level2type=N'CONSTRAINT',@level2name=N'PK_HistorialIdenExt'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
-- TipAsigPermiso column + PK EPs
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identificador del tipo de asignacion (PK).', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'COLUMN',@level2name=N'Id'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Nombre del tipo de asignacion.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'COLUMN',@level2name=N'Nombre'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
BEGIN TRY EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Clave primaria de TipAsigPermiso.', @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TipAsigPermiso', @level2type=N'CONSTRAINT',@level2name=N'PK_TipAsigPermiso'; END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (15233) THROW; END CATCH;
PRINT 'STEP 7: Extended Properties OK';
GO
