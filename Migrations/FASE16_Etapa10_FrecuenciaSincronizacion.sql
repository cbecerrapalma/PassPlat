-- ============================================================
-- FASE 16 — ETAPA 10: Sincronización Perfil
-- Adds FrecuenciaSincronizacion column to ConfProvIden
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConfProvIden') AND name = 'FrecuenciaSincronizacion')
BEGIN
    ALTER TABLE ConfProvIden
        ADD FrecuenciaSincronizacion NVARCHAR(20) NOT NULL CONSTRAINT DF_ConfProvIden_FrecuenciaSincronizacion DEFAULT 'Siempre';
    PRINT 'OK: FrecuenciaSincronizacion added to ConfProvIden';
END
ELSE
    PRINT 'SKIP: FrecuenciaSincronizacion already exists';
GO
