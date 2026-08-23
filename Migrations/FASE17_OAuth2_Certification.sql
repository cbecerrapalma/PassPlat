-- ============================================================
-- FASE 17 — OAuth2 Certification & HTTPS Hardening
-- 
-- Include: IdenExtTokens table, indexes, rollback
-- ============================================================
-- Requisito: HTTPS, PKCE, RedirectUri desde BD, Offline Access
-- ============================================================

-- +----------------------------------------------------------+
-- FASE 17.7: IdenExtTokens (Refresh Token storage)
-- +----------------------------------------------------------
IF OBJECT_ID('IdenExtTokens', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdenExtTokens] (
        [Id]              BIGINT          IDENTITY(1,1) NOT NULL,
        [IdIdenExt]       BIGINT          NOT NULL,
        [RefreshToken]    VARBINARY(2000) NULL,
        [ScopesHash]      INT             NULL,
        [ExpiresAt]       DATETIME2(7)    NULL,
        [RefreshExpiresAt] DATETIME2(7)   NULL,
        [LastRefresh]     DATETIME2(7)    NULL,
        [Revoked]         BIT             NOT NULL DEFAULT 0,
        [RevokedAt]       DATETIME2(7)    NULL,
        [RevokeReason]    NVARCHAR(500)   NULL,
        [FecCrea]         DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        [FecMod]          DATETIME2(7)    NULL,

        CONSTRAINT [PK_IdenExtTokens] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    PRINT 'Table IdenExtTokens created.';
END
ELSE
BEGIN
    PRINT 'Table IdenExtTokens already exists.';
END;

-- FK IdenExtTokens -> IdenExt
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_IdenExtTokens_IdenExt')
BEGIN
    IF OBJECT_ID('IdenExt', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[IdenExtTokens]
            ADD CONSTRAINT [FK_IdenExtTokens_IdenExt]
            FOREIGN KEY ([IdIdenExt]) REFERENCES [dbo].[IdenExt]([Id])
            ON DELETE NO ACTION;
        PRINT 'FK_IdenExtTokens_IdenExt created.';
    END
    ELSE
        PRINT 'WARNING: IdenExt table not found, FK_IdenExtTokens_IdenExt skipped.';
END;

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_IdIdenExt')
    CREATE NONCLUSTERED INDEX [IX_IdenExtTokens_IdIdenExt] ON [dbo].[IdenExtTokens]([IdIdenExt] ASC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdenExtTokens_Activos')
    CREATE NONCLUSTERED INDEX [IX_IdenExtTokens_Activos] ON [dbo].[IdenExtTokens]([Revoked] ASC)
        WHERE [Revoked] = 0;

-- +----------------------------------------------------------+
-- FASE 17.11: ConfProvIden — OAuth2 endpoints expansion
-- +----------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'AuthorizationEndpoint')
    ALTER TABLE [dbo].[ConfProvIden] ADD [AuthorizationEndpoint] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'TokenEndpoint')
    ALTER TABLE [dbo].[ConfProvIden] ADD [TokenEndpoint] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'JwksUri')
    ALTER TABLE [dbo].[ConfProvIden] ADD [JwksUri] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'Issuer')
    ALTER TABLE [dbo].[ConfProvIden] ADD [Issuer] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'ResponseType')
    ALTER TABLE [dbo].[ConfProvIden] ADD [ResponseType] NVARCHAR(50) NOT NULL DEFAULT 'code';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'GrantType')
    ALTER TABLE [dbo].[ConfProvIden] ADD [GrantType] NVARCHAR(50) NOT NULL DEFAULT 'authorization_code';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE OBJECT_ID('ConfProvIden') = object_id AND name = 'ExtraParams')
    ALTER TABLE [dbo].[ConfProvIden] ADD [ExtraParams] NVARCHAR(1000) NULL;

PRINT 'FASE 17 migration completed.';
GO
