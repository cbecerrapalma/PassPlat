-- ============================================================
-- S21.1 — Outbox Schema
-- PassPlat — Transactional Outbox for NewIpDetectedEvent
-- ============================================================
-- Idempotent: no drop/recreate existing data.
-- This schema creates the Outbox table if it does not exist yet.
--
-- Estados validos: pending, processing, published, failed
-- La validacion de estados se realiza a nivel de codigo.
-- No se utilizan CHECK constraints por convencion del proyecto.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Outbox')
BEGIN
    CREATE TABLE Outbox (
        Id               BIGINT IDENTITY(1,1) PRIMARY KEY,
        EventType        NVARCHAR(100)    NOT NULL,
        Payload          NVARCHAR(MAX)    NOT NULL,
        CorrelationId    NVARCHAR(64)     NOT NULL,
        IdTenant         INT              NULL,
        IdUsuario        INT              NULL,
        Status           NVARCHAR(20)     NOT NULL CONSTRAINT DF_Outbox_Status DEFAULT ('pending'),
        Attempts         INT              NOT NULL CONSTRAINT DF_Outbox_Attempts DEFAULT (0),
        CreatedAt        DATETIME2(3)     NOT NULL CONSTRAINT DF_Outbox_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ProcessingStartedAt  DATETIME2(3)     NULL,
        ProcessedAt      DATETIME2(3)     NULL,
        LastError        NVARCHAR(MAX)    NULL,
        NextAttemptAt    DATETIME2(3)     NULL
    );

    -- INDEX: optimiza el polling de pendientes
    -- Status = 'pending', ORDER BY CreatedAt
    CREATE NONCLUSTERED INDEX IX_Outbox_Pending_Status_CreatedOn
    ON Outbox (Status, CreatedAt)
    INCLUDE (Id)
    WHERE Status = 'pending';

    -- INDEX: para recovery de processing stale
    CREATE NONCLUSTERED INDEX IX_Outbox_ProcessingStartedAt
    ON Outbox (ProcessingStartedAt)
    INCLUDE (Id)
    WHERE Status = 'processing';

    PRINT 'Table Outbox created successfully.';
END
ELSE
    PRINT 'Table Outbox already exists. No changes made.';

-- No modify existing tables (IPs, UX_IPs_Direccion, EmailLog).
