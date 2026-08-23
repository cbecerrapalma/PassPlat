-- ============================================================
-- FASE 17.1 — OAuth Hardening
-- 
-- 1. Recrea IdenExtTokens (modelo genérico 3-tokens por fila)
-- 2. SP_Auth_RenovarTokenProveedor (transaccional + RowVersion)
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- +----------------------------------------------------------+
-- 1. IdenExtTokens — Nuevo modelo (1 fila/identidad)
-- +----------------------------------------------------------+
IF OBJECT_ID('IdenExtTokens', 'U') IS NOT NULL
    DROP TABLE [dbo].[IdenExtTokens];

CREATE TABLE [dbo].[IdenExtTokens] (
    [Id]                 BIGINT          IDENTITY(1,1) NOT NULL,
    [IdIdenExt]          BIGINT          NOT NULL,

    -- Access Token
    [AccessTokenEnc]     VARBINARY(4000) NULL,
    [AccessTokenHash]    NVARCHAR(128)   NULL,
    [AccessTokenExpires] DATETIME2(7)    NULL,

    -- Refresh Token
    [RefreshTokenEnc]     VARBINARY(4000) NULL,
    [RefreshTokenHash]    NVARCHAR(128)   NULL,
    [RefreshTokenExpires] DATETIME2(7)    NULL,

    -- ID Token
    [IdTokenEnc]         VARBINARY(8000) NULL,
    [IdTokenHash]        NVARCHAR(128)   NULL,

    -- Metadata
    [Scope]              NVARCHAR(1000)  NULL,
    [TokenType]          NVARCHAR(50)    NULL,
    [CorrelationId]      NVARCHAR(50)    NULL,
    [HashAlgoritmo]      NVARCHAR(20)    NOT NULL DEFAULT 'SHA256',

    -- Control
    [Version]            INT             NOT NULL DEFAULT 1,
    [Activo]             BIT             NOT NULL DEFAULT 1,
    [Revocado]           BIT             NOT NULL DEFAULT 0,
    [FechaRenovacion]    DATETIME2(7)    NULL,
    [UltimoUso]          DATETIME2(7)    NULL,
    [FechaRevocacion]    DATETIME2(7)    NULL,
    [MotivoRevocacion]   NVARCHAR(500)   NULL,

    -- Concurrencia (EF Core IsRowVersion)
    [RowVersion]         ROWVERSION      NOT NULL,

    CONSTRAINT [PK_IdenExtTokens] PRIMARY KEY CLUSTERED ([Id] ASC)
);

PRINT 'Table IdenExtTokens recreated with new schema.';

-- FK IdenExtTokens -> IdenExt
IF OBJECT_ID('IdenExt', 'U') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[IdenExtTokens]
        ADD CONSTRAINT [FK_IdenExtTokens_IdenExt]
        FOREIGN KEY ([IdIdenExt]) REFERENCES [dbo].[IdenExt]([Id])
        ON DELETE NO ACTION;
    PRINT 'FK_IdenExtTokens_IdenExt created.';
END
ELSE
    PRINT 'WARNING: IdenExt table not found, FK skipped.';
GO

-- Índices (SET QUOTED_IDENTIFIER ON requerido para filtered indexes)
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_IdIdenExt')
    CREATE INDEX [IX_IdenExtTokens_IdIdenExt] ON [dbo].[IdenExtTokens]([IdIdenExt]);
PRINT 'IX_IdenExtTokens_IdIdenExt created.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_Activos')
    CREATE INDEX [IX_IdenExtTokens_Activos] ON [dbo].[IdenExtTokens]([Activo]) WHERE [Activo] = 1;
PRINT 'IX_IdenExtTokens_Activos created.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_RefreshHash')
    CREATE INDEX [IX_IdenExtTokens_RefreshHash] ON [dbo].[IdenExtTokens]([RefreshTokenHash])
        WHERE [RefreshTokenHash] IS NOT NULL AND [Activo] = 1;
PRINT 'IX_IdenExtTokens_RefreshHash created.';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_AccessHash')
    CREATE INDEX [IX_IdenExtTokens_AccessHash] ON [dbo].[IdenExtTokens]([AccessTokenHash])
        WHERE [AccessTokenHash] IS NOT NULL AND [Activo] = 1;
PRINT 'IX_IdenExtTokens_AccessHash created.';
GO

-- +----------------------------------------------------------+
-- 2. SP_Auth_RenovarTokenProveedor (transaccional + RowVersion)
-- +----------------------------------------------------------+
CREATE OR ALTER PROCEDURE [dbo].[SP_Auth_RenovarTokenProveedor]
    @IdIdenExtTokens      BIGINT,
    @IdIdenExt            BIGINT,
    @AccessTokenEnc       VARBINARY(4000) = NULL,
    @AccessTokenHash      NVARCHAR(128)  = NULL,
    @AccessTokenExpires   DATETIME2(7)   = NULL,
    @RefreshTokenEnc      VARBINARY(4000) = NULL,
    @RefreshTokenHash     NVARCHAR(128)  = NULL,
    @RefreshTokenExpires  DATETIME2(7)   = NULL,
    @IdTokenEnc           VARBINARY(8000) = NULL,
    @IdTokenHash          NVARCHAR(128)  = NULL,
    @Scope                NVARCHAR(1000) = NULL,
    @TokenType            NVARCHAR(50)   = NULL,
    @CorrelationId        NVARCHAR(50)   = NULL,
    @RowVersion           BINARY(8),
    @NuevoId              BIGINT          = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Validar concurrencia (RowVersion)
        IF NOT EXISTS (SELECT 1 FROM [dbo].[IdenExtTokens]
                       WHERE [Id] = @IdIdenExtTokens AND [RowVersion] = @RowVersion)
        BEGIN
            THROW 50000, 'Conflicto de concurrencia: el token fue modificado por otro proceso.', 1;
        END

        -- 2. Revocar token anterior (lógico)
        UPDATE [dbo].[IdenExtTokens]
        SET [Activo] = 0,
            [Revocado] = 1,
            [FechaRevocacion] = SYSUTCDATETIME(),
            [MotivoRevocacion] = 'Renovado por SP_Auth_RenovarTokenProveedor'
        WHERE [Id] = @IdIdenExtTokens AND [Activo] = 1;

        -- 3. Insertar nuevo token con Version+1
        INSERT INTO [dbo].[IdenExtTokens]
            ([IdIdenExt], [Version],
             [AccessTokenEnc], [AccessTokenHash], [AccessTokenExpires],
             [RefreshTokenEnc], [RefreshTokenHash], [RefreshTokenExpires],
             [IdTokenEnc], [IdTokenHash],
             [Scope], [TokenType], [CorrelationId],
             [Activo], [Revocado])
        SELECT
            @IdIdenExt, [Version] + 1,
            @AccessTokenEnc, @AccessTokenHash, @AccessTokenExpires,
            @RefreshTokenEnc, @RefreshTokenHash, @RefreshTokenExpires,
            @IdTokenEnc, @IdTokenHash,
            @Scope, @TokenType, @CorrelationId,
            1, 0
        FROM [dbo].[IdenExtTokens]
        WHERE [Id] = @IdIdenExtTokens;

        SET @NuevoId = SCOPE_IDENTITY();

        -- 4. Registrar auditoría
        INSERT INTO [dbo].[AudIdenExt]
            ([IdTenant], [IdProvIden], [IdUsuario], [Evento], [Resultado],
             [Detalle], [CorrelationId], [Origen], [Destino])
        SELECT
            i.[IdTenant], i.[IdProvIden], i.[IdUsuario], 'TOKEN_RENOVADO', 'EXITOSO',
            CONCAT('Token renovado: IdIdenExtTokens=', @IdIdenExtTokens, ' NuevoId=', @NuevoId),
            @CorrelationId,
            CONCAT('old_version=', t.[Version]), CONCAT('new_version=', t.[Version] + 1)
        FROM [dbo].[IdenExtTokens] t
        INNER JOIN [dbo].[IdenExt] i ON i.[Id] = t.[IdIdenExt]
        WHERE t.[Id] = @IdIdenExtTokens;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT 'SP_Auth_RenovarTokenProveedor created.';
GO
