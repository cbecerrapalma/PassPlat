-- FASE 16 ETAPA 3+4+2+9: Identity Management Enterprise Migration
-- Date: 2026-07-07
-- Description: Adds EstIdenExt catalog, HistorialIdenExt table,
--              new fields to IdenExt, and policy fields to ConfProvIden
-- Note: Base schema (PASSWORDS.sql) must be run first. This script adds FASE 16 extensions.

SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- ETAPA 3: EstIdenExt Catalog
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EstIdenExt')
BEGIN
    CREATE TABLE EstIdenExt (
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

    INSERT INTO EstIdenExt (Id, Nombre, Descripcion, Color, Orden) VALUES
    (1, N'Pendiente', N'Identidad creada pendiente de autorizacion', N'#FF9800', 1),
    (2, N'Autorizada', N'Identidad autorizada y activa', N'#4CAF50', 2),
    (3, N'Revocada', N'Identidad revocada por administrador', N'#F44336', 3),
    (4, N'Expirada', N'Tokens de la identidad expirados', N'#9E9E9E', 4),
    (5, N'Suspendida', N'Identidad suspendida temporalmente', N'#FF5722', 5),
    (6, N'Error', N'Error en la autenticacion con el proveedor', N'#E91E63', 6),
    (7, N'Sincronizacion Pendiente', N'Pendiente de sincronizacion de perfil', N'#2196F3', 7);

    PRINT 'EstIdenExt table created with seed data';
END
ELSE
    PRINT 'EstIdenExt already exists';
GO

-- ============================================================
-- ETAPA 2: New fields on IdenExt
-- Only runs if the table exists in the database
-- ============================================================
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'IdEstado')
    BEGIN
        ALTER TABLE IdenExt ADD IdEstado TINYINT NOT NULL DEFAULT 2;
        PRINT 'Added IdEstado to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'Scopes')
    BEGIN
        ALTER TABLE IdenExt ADD Scopes NVARCHAR(1000) NULL;
        PRINT 'Added Scopes to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'UltimaIP')
    BEGIN
        ALTER TABLE IdenExt ADD UltimaIP NVARCHAR(45) NULL;
        PRINT 'Added UltimaIP to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'UltimoDisp')
    BEGIN
        ALTER TABLE IdenExt ADD UltimoDisp INT NULL;
        PRINT 'Added UltimoDisp to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'UltimoUserAgent')
    BEGIN
        ALTER TABLE IdenExt ADD UltimoUserAgent NVARCHAR(500) NULL;
        PRINT 'Added UltimoUserAgent to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'UltimoTenant')
    BEGIN
        ALTER TABLE IdenExt ADD UltimoTenant INT NULL;
        PRINT 'Added UltimoTenant to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'FecRevocacion')
    BEGIN
        ALTER TABLE IdenExt ADD FecRevocacion DATETIME2 NULL;
        PRINT 'Added FecRevocacion to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'IdUsuarioRevoca')
    BEGIN
        ALTER TABLE IdenExt ADD IdUsuarioRevoca INT NULL;
        PRINT 'Added IdUsuarioRevoca to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('IdenExt') AND name = 'MotivoRevocacion')
    BEGIN
        ALTER TABLE IdenExt ADD MotivoRevocacion NVARCHAR(500) NULL;
        PRINT 'Added MotivoRevocacion to IdenExt';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IdenExt_Estado')
    BEGIN
        ALTER TABLE IdenExt ADD CONSTRAINT FK_IdenExt_Estado
            FOREIGN KEY (IdEstado) REFERENCES EstIdenExt(Id);
        PRINT 'Added FK_IdenExt_Estado';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IdenExt_UltimoDisp')
    BEGIN
        ALTER TABLE IdenExt ADD CONSTRAINT FK_IdenExt_UltimoDisp
            FOREIGN KEY (UltimoDisp) REFERENCES Disp(Id);
        PRINT 'Added FK_IdenExt_UltimoDisp';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IdenExt_UltimoTenant')
    BEGIN
        ALTER TABLE IdenExt ADD CONSTRAINT FK_IdenExt_UltimoTenant
            FOREIGN KEY (UltimoTenant) REFERENCES Tenants(Id);
        PRINT 'Added FK_IdenExt_UltimoTenant';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_IdenExt_UsuarioRevoca')
    BEGIN
        ALTER TABLE IdenExt ADD CONSTRAINT FK_IdenExt_UsuarioRevoca
            FOREIGN KEY (IdUsuarioRevoca) REFERENCES Usuarios(Id);
        PRINT 'Added FK_IdenExt_UsuarioRevoca';
    END
    PRINT 'IdenExt migration applied';
END
ELSE
    PRINT 'IdenExt table not found - run base schema first (PASSWORDS.sql)';
GO

-- ============================================================
-- ETAPA 4: HistorialIdenExt
-- Create table WITHOUT FK constraints first, then add them individually
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistorialIdenExt')
BEGIN
    CREATE TABLE HistorialIdenExt (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        IdTenant INT NOT NULL,
        IdUsuario INT NOT NULL,
        IdIdenExt BIGINT NOT NULL,
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

    CREATE INDEX IX_HistorialIdEx_Usuario ON HistorialIdenExt(IdUsuario);
    CREATE INDEX IX_HistorialIdEx_Identidad ON HistorialIdenExt(IdIdenExt);
    CREATE INDEX IX_HistorialIdEx_FecCambio ON HistorialIdenExt(FecCambio DESC);
    CREATE INDEX IX_HistorialIdEx_Tenant ON HistorialIdenExt(IdTenant);

    PRINT 'HistorialIdenExt table created (without FKs)';
END
ELSE
    PRINT 'HistorialIdenExt already exists';
GO

-- Add FKs individually with guards (only if referenced table exists)
IF OBJECT_ID('HistorialIdenExt', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HistorialIdEx_Tenant')
       AND OBJECT_ID('Tenants', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE HistorialIdenExt ADD CONSTRAINT FK_HistorialIdEx_Tenant
            FOREIGN KEY (IdTenant) REFERENCES Tenants(Id);
        PRINT 'Added FK_HistorialIdEx_Tenant';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HistorialIdEx_Usuario')
       AND OBJECT_ID('Usuarios', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE HistorialIdenExt ADD CONSTRAINT FK_HistorialIdEx_Usuario
            FOREIGN KEY (IdUsuario) REFERENCES Usuarios(Id);
        PRINT 'Added FK_HistorialIdEx_Usuario';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HistorialIdEx_Identidad')
       AND OBJECT_ID('IdenExt', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE HistorialIdenExt ADD CONSTRAINT FK_HistorialIdEx_Identidad
            FOREIGN KEY (IdIdenExt) REFERENCES IdenExt(Id);
        PRINT 'Added FK_HistorialIdEx_Identidad';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HistorialIdEx_ProvIden')
       AND OBJECT_ID('ProvIden', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE HistorialIdenExt ADD CONSTRAINT FK_HistorialIdEx_ProvIden
            FOREIGN KEY (IdProvIden) REFERENCES ProvIden(Id);
        PRINT 'Added FK_HistorialIdEx_ProvIden';
    END
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_HistorialIdEx_RealizadoPor')
       AND OBJECT_ID('Usuarios', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE HistorialIdenExt ADD CONSTRAINT FK_HistorialIdEx_RealizadoPor
            FOREIGN KEY (RealizadoPor) REFERENCES Usuarios(Id);
        PRINT 'Added FK_HistorialIdEx_RealizadoPor';
    END
    PRINT 'HistorialIdenExt FKs applied (skipping missing refs)';
END
GO

-- ============================================================
-- ETAPA 9: New policy fields on ConfProvIden
-- Only runs if the table exists in the database
-- ============================================================
IF OBJECT_ID('ConfProvIden', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfProvIden') AND name = 'PermitirLogin')
    BEGIN
        ALTER TABLE ConfProvIden ADD PermitirLogin BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirCrearUsuario BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirVincular BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirDesvincular BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirPasswordLocal BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD ObligaMFA BIT NOT NULL DEFAULT 0;
        ALTER TABLE ConfProvIden ADD PermitirCambioEmail BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirCambioNombre BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirSincronizarAvatar BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD PermitirSincronizarPerfil BIT NOT NULL DEFAULT 1;
        ALTER TABLE ConfProvIden ADD Prioridad INT NOT NULL DEFAULT 0;
        ALTER TABLE ConfProvIden ADD OrdenVisual INT NOT NULL DEFAULT 0;
        ALTER TABLE ConfProvIden ADD Logo NVARCHAR(500) NULL;
        ALTER TABLE ConfProvIden ADD Color NVARCHAR(20) NULL;
        ALTER TABLE ConfProvIden ADD Tooltip NVARCHAR(200) NULL;
        ALTER TABLE ConfProvIden ADD Descripcion NVARCHAR(500) NULL;
        PRINT 'Added 16 policy fields to ConfProvIden';
    END
    ELSE
        PRINT 'ConfProvIden policy fields already exist';
END
ELSE
    PRINT 'ConfProvIden table not found - run base schema first (PASSWORDS.sql)';
GO

PRINT '=== FASE 16 Migration Complete ===';
GO
