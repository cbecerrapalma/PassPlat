-- FASE 17.3.3: Add missing columns to ConfProvIden
-- These columns exist in the C# entity but were never added to the DB schema

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ConfProvIden' AND COLUMN_NAME = 'AllowLoginWithoutRefreshToken')
    ALTER TABLE [ConfProvIden] ADD [AllowLoginWithoutRefreshToken] BIT NOT NULL CONSTRAINT [DF_ConfProvIden_AllowLoginWithoutRefreshToken] DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ConfProvIden' AND COLUMN_NAME = 'AllowRefreshTokenRotation')
    ALTER TABLE [ConfProvIden] ADD [AllowRefreshTokenRotation] BIT NOT NULL CONSTRAINT [DF_ConfProvIden_AllowRefreshTokenRotation] DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ConfProvIden' AND COLUMN_NAME = 'RequireEmailVerified')
    ALTER TABLE [ConfProvIden] ADD [RequireEmailVerified] BIT NOT NULL CONSTRAINT [DF_ConfProvIden_RequireEmailVerified] DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ConfProvIden' AND COLUMN_NAME = 'RowVersion')
    ALTER TABLE [ConfProvIden] ADD [RowVersion] ROWVERSION NOT NULL;
GO

PRINT 'FASE17.3.3: Missing columns added to ConfProvIden successfully';
