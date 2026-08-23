-- ============================================================
-- FASE 17.3.3 - Version + Metadata + JwksUri for ProvIden
-- ============================================================

-- Batch 1: Add columns
BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Adding new columns to ProvIden ---';

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProvIden' AND COLUMN_NAME = 'Version')
    BEGIN
        ALTER TABLE [dbo].[ProvIden] ADD [Version] NVARCHAR(50) NULL;
        PRINT 'Column Version added.';
    END
    ELSE
        PRINT 'Column Version already exists.';

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProvIden' AND COLUMN_NAME = 'JwksUri')
    BEGIN
        ALTER TABLE [dbo].[ProvIden] ADD [JwksUri] NVARCHAR(500) NULL;
        PRINT 'Column JwksUri added.';
    END
    ELSE
        PRINT 'Column JwksUri already exists.';

    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProvIden' AND COLUMN_NAME = 'Metadata')
    BEGIN
        ALTER TABLE [dbo].[ProvIden] ADD [Metadata] NVARCHAR(MAX) NULL;
        PRINT 'Column Metadata added.';
    END
    ELSE
        PRINT 'Column Metadata already exists.';

    COMMIT;
    PRINT '--- Columns added successfully. ---';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT 'ERROR adding columns: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO

-- Batch 2: Seed data
SET QUOTED_IDENTIFIER ON;
BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Seeding Version, JwksUri, Metadata ---';

    UPDATE [dbo].[ProvIden]
    SET [Version] = '2.0',
        [JwksUri] = 'https://www.googleapis.com/oauth2/v3/certs',
        [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["openid","profile","email"]}'
    WHERE [Codigo] = 'GOOGLE';
    PRINT 'GOOGLE: Version=2.0, JwksUri, Metadata seeded.';

    UPDATE [dbo].[ProvIden]
    SET [Version] = '14.0',
        [JwksUri] = 'https://www.facebook.com/.well-known/oauth/openid/jwks/',
        [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["email","public_profile"]}'
    WHERE [Codigo] = 'FACEBOOK';
    PRINT 'FACEBOOK: Version=14.0, JwksUri, Metadata seeded.';

    UPDATE [dbo].[ProvIden]
    SET [Version] = '1.0',
        [JwksUri] = NULL,
        [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":false,"SupportsDynamicClientRegistration":false,"Claims":["user_profile","user_media"]}'
    WHERE [Codigo] = 'INSTAGRAM';
    PRINT 'INSTAGRAM: Version=1.0, JwksUri, Metadata seeded.';

    UPDATE [dbo].[ProvIden]
    SET [Version] = '2.0',
        [JwksUri] = 'https://www.linkedin.com/oauth/openid/jwks',
        [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["openid","profile","email"]}'
    WHERE [Codigo] = 'LINKEDIN';
    PRINT 'LINKEDIN: Version=2.0, JwksUri, Metadata seeded.';

    COMMIT;
    PRINT '--- FASE 17.3.3 completada exitosamente. ---';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT 'ERROR seeding data: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
