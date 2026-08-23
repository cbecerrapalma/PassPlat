-- FIX: AudIdenExt.Origen demasiado corta
-- La columna Origen (NVARCHAR(50)) causaba truncación al almacenar el redirectUri del callback
-- OAuth (ej. https://localhost:5001/api/auth/externo/GOOGLE/callback = 55 chars).
-- El SP SP_ProvIden_RegistrarAuditoria recibe @Origen nvarchar(500) y la columna Destino es NVARCHAR(500).
-- Se amplía Origen a NVARCHAR(500) para consistencia, de modo que el INSERT del SP no falle
-- silenciosamente por truncation en el registro de LOGIN_EXTERNO.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID('dbo.AudIdenExt', 'U') IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AudIdenExt') AND name = 'Origen')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AudIdenExt_Origen' AND object_id = OBJECT_ID('dbo.AudIdenExt'))
        DROP INDEX IX_AudIdenExt_Origen ON dbo.AudIdenExt;

    ALTER TABLE dbo.AudIdenExt ALTER COLUMN Origen NVARCHAR(500) NULL;

    CREATE INDEX IX_AudIdenExt_Origen ON dbo.AudIdenExt(Origen) WHERE Origen IS NOT NULL;
    PRINT 'FIX: AudIdenExt.Origen ampliada a NVARCHAR(500) + indice recreado';
END
ELSE
BEGIN
    PRINT 'FIX: SKIP - columna Origen no existe en AudIdenExt';
END