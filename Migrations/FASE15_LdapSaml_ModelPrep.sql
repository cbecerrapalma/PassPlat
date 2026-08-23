-- ============================================================================
-- FASE 15: Model Prep — LDAP/SAML/AD Support
-- Date: 2026-07-06
-- Purpose: Create tables for future LDAP and SAML authentication
-- ============================================================================

SET NOCOUNT ON;
GO

-- ============================================================================
-- 1. ConfLdap — LDAP configuration per tenant
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConfLdap')
BEGIN
    CREATE TABLE dbo.ConfLdap (
        Id              INT IDENTITY(1,1) NOT NULL,
        IdTenant        INT NOT NULL,
        Servidor        NVARCHAR(255) NOT NULL,
        Puerto          INT NOT NULL CONSTRAINT DF_ConfLdap_Puerto DEFAULT 389,
        BaseDN          NVARCHAR(500) NOT NULL,
        BindDN          NVARCHAR(500) NULL,
        BindPassword    NVARCHAR(1000) NULL,
        UsarSSL         BIT NOT NULL CONSTRAINT DF_ConfLdap_UsarSSL DEFAULT 0,
        UsarStartTLS    BIT NOT NULL CONSTRAINT DF_ConfLdap_UsarStartTLS DEFAULT 0,
        FiltroBusqueda  NVARCHAR(500) NULL,
        AtributoEmail   NVARCHAR(100) NULL CONSTRAINT DF_ConfLdap_AtributoEmail DEFAULT 'mail',
        AtributoNombre  NVARCHAR(100) NULL CONSTRAINT DF_ConfLdap_AtributoNombre DEFAULT 'displayName',
        AtributoUid     NVARCHAR(100) NULL CONSTRAINT DF_ConfLdap_AtributoUid DEFAULT 'sAMAccountName',
        AtributoGrupo   NVARCHAR(100) NULL CONSTRAINT DF_ConfLdap_AtributoGrupo DEFAULT 'memberOf',
        TimeoutSeconds  INT NULL CONSTRAINT DF_ConfLdap_TimeoutSeconds DEFAULT 30,
        AutoProvisionar BIT NOT NULL CONSTRAINT DF_ConfLdap_AutoProvisionar DEFAULT 0,
        SincronizarGrupos BIT NOT NULL CONSTRAINT DF_ConfLdap_SincronizarGrupos DEFAULT 0,
        Estado          TINYINT NOT NULL CONSTRAINT DF_ConfLdap_Estado DEFAULT 1,
        Metadata        NVARCHAR(MAX) NULL,
        Activo          BIT NOT NULL CONSTRAINT DF_ConfLdap_Activo DEFAULT 1,
        FecCrea         DATETIME2(3) NULL CONSTRAINT DF_ConfLdap_FecCrea DEFAULT sysutcdatetime(),
        FecMod          DATETIME2(3) NULL,

        CONSTRAINT PK_ConfLdap PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ConfLdap_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT CK_ConfLdap_Puerto CHECK (Puerto > 0 AND Puerto <= 65535)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UK_ConfLdap_Tenant ON dbo.ConfLdap(IdTenant) WHERE Activo = 1;
    CREATE NONCLUSTERED INDEX IX_ConfLdap_Activo ON dbo.ConfLdap(Activo) WHERE Activo = 1;
    CREATE NONCLUSTERED INDEX IX_ConfLdap_Servidor ON dbo.ConfLdap(Servidor);

    EXEC sys.sp_addextendedproperty N'MS_Description', N'Configuración de LDAP por tenant. Almacena credenciales y parámetros de conexión para autenticación LDAP/AD.', 'SCHEMA', N'dbo', 'TABLE', N'ConfLdap';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Servidor LDAP (hostname o IP)', 'SCHEMA', N'dbo', 'TABLE', N'ConfLdap', 'COLUMN', N'Servidor';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Base DN para búsqueda de usuarios', 'SCHEMA', N'dbo', 'TABLE', N'ConfLdap', 'COLUMN', N'BaseDN';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Bind DN para autenticación (null = anonymous bind)', 'SCHEMA', N'dbo', 'TABLE', N'ConfLdap', 'COLUMN', N'BindDN';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Password del Bind DN (cifrado)', 'SCHEMA', N'dbo', 'TABLE', N'ConfLdap', 'COLUMN', N'BindPassword';
END
GO

-- ============================================================================
-- 2. LdapSyncLog — LDAP sync audit log
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LdapSyncLog')
BEGIN
    CREATE TABLE dbo.LdapSyncLog (
        Id                  BIGINT IDENTITY(1,1) NOT NULL,
        IdTenant            INT NOT NULL,
        IdUsuario           INT NULL,
        Operacion           NVARCHAR(50) NOT NULL,
        Resultado           NVARCHAR(50) NOT NULL,
        LdapUid             NVARCHAR(255) NULL,
        Detalle             NVARCHAR(MAX) NULL,
        UsuariosCreados     INT NULL,
        UsuariosActualizados INT NULL,
        UsuariosDesactivados INT NULL,
        Errores             INT NULL,
        FecOperacion        DATETIME2(3) NOT NULL CONSTRAINT DF_LdapSyncLog_FecOperacion DEFAULT sysutcdatetime(),

        CONSTRAINT PK_LdapSyncLog PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LdapSyncLog_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_LdapSyncLog_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id)
    );

    CREATE NONCLUSTERED INDEX IX_LdapSyncLog_Tenant ON dbo.LdapSyncLog(IdTenant);
    CREATE NONCLUSTERED INDEX IX_LdapSyncLog_FecOperacion ON dbo.LdapSyncLog(FecOperacion DESC);
    CREATE NONCLUSTERED INDEX IX_LdapSyncLog_Operacion ON dbo.LdapSyncLog(Operacion);

    EXEC sys.sp_addextendedproperty N'MS_Description', N'Log de auditoría de sincronizaciones LDAP. Registra cada operación de sync (full, incremental, auth) con estadísticas de usuarios creados/actualizados/desactivados.', 'SCHEMA', N'dbo', 'TABLE', N'LdapSyncLog';
END
GO

-- ============================================================================
-- 3. ConfSaml — SAML configuration per tenant
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConfSaml')
BEGIN
    CREATE TABLE dbo.ConfSaml (
        Id                          INT IDENTITY(1,1) NOT NULL,
        IdTenant                    INT NOT NULL,
        EntityId                    NVARCHAR(500) NOT NULL,
        MetadataUrl                 NVARCHAR(1000) NULL,
        MetadataXml                 NVARCHAR(MAX) NULL,
        Certificate                 NVARCHAR(2000) NULL,
        SignatureAlgorithm          NVARCHAR(200) NULL CONSTRAINT DF_ConfSaml_SignatureAlgorithm DEFAULT 'http://www.w3.org/2001/04/xmldsig-more#rsa-sha256',
        DigestAlgorithm             NVARCHAR(200) NULL CONSTRAINT DF_ConfSaml_DigestAlgorithm DEFAULT 'http://www.w3.org/2001/04/xmlenc#sha256',
        SsoUrl                      NVARCHAR(1000) NULL,
        SloUrl                      NVARCHAR(1000) NULL,
        AttributeEmail              NVARCHAR(500) NULL CONSTRAINT DF_ConfSaml_AttributeEmail DEFAULT 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress',
        AttributeNombre             NVARCHAR(500) NULL CONSTRAINT DF_ConfSaml_AttributeNombre DEFAULT 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name',
        AttributeUid                NVARCHAR(500) NULL CONSTRAINT DF_ConfSaml_AttributeUid DEFAULT 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
        WantsAssertionsSigned       BIT NOT NULL CONSTRAINT DF_ConfSaml_WantsAssertionsSigned DEFAULT 1,
        AutenticacionRequestSigned  BIT NOT NULL CONSTRAINT DF_ConfSaml_AuthnRequestSigned DEFAULT 0,
        AllowCreate                 BIT NOT NULL CONSTRAINT DF_ConfSaml_AllowCreate DEFAULT 1,
        AutoProvisionar             BIT NOT NULL CONSTRAINT DF_ConfSaml_AutoProvisionar DEFAULT 0,
        Estado                      TINYINT NOT NULL CONSTRAINT DF_ConfSaml_Estado DEFAULT 1,
        Metadata                    NVARCHAR(MAX) NULL,
        Activo                      BIT NOT NULL CONSTRAINT DF_ConfSaml_Activo DEFAULT 1,
        FecCrea                     DATETIME2(3) NULL CONSTRAINT DF_ConfSaml_FecCrea DEFAULT sysutcdatetime(),
        FecMod                      DATETIME2(3) NULL,

        CONSTRAINT PK_ConfSaml PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ConfSaml_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UK_ConfSaml_Tenant ON dbo.ConfSaml(IdTenant) WHERE Activo = 1;
    CREATE NONCLUSTERED INDEX IX_ConfSaml_EntityId ON dbo.ConfSaml(EntityId);
    CREATE NONCLUSTERED INDEX IX_ConfSaml_Activo ON dbo.ConfSaml(Activo) WHERE Activo = 1;

    EXEC sys.sp_addextendedproperty N'MS_Description', N'Configuración de SAML 2.0 por tenant. Almacena metadata del IdP, certificados y atributos para federación SAML.', 'SCHEMA', N'dbo', 'TABLE', N'ConfSaml';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Entity ID del Identity Provider (SAML)', 'SCHEMA', N'dbo', 'TABLE', N'ConfSaml', 'COLUMN', N'EntityId';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'URL del IdP SSO endpoint', 'SCHEMA', N'dbo', 'TABLE', N'ConfSaml', 'COLUMN', N'SsoUrl';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Certificado X.509 del IdP (PEM o Base64)', 'SCHEMA', N'dbo', 'TABLE', N'ConfSaml', 'COLUMN', N'Certificate';
END
GO

-- ============================================================================
-- 4. SamlSession — SAML session tracking
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SamlSession')
BEGIN
    CREATE TABLE dbo.SamlSession (
        Id                      BIGINT IDENTITY(1,1) NOT NULL,
        IdTenant                INT NOT NULL,
        IdUsuario               INT NULL,
        IdConfSaml              INT NOT NULL,
        NameId                  NVARCHAR(500) NOT NULL,
        SessionIndex            NVARCHAR(500) NULL,
        NotOnOrAfter            NVARCHAR(50) NULL,
        SubjectConfirmationData NVARCHAR(500) NULL,
        AttributesJson          NVARCHAR(MAX) NULL,
        EsActiva                BIT NOT NULL CONSTRAINT DF_SamlSession_EsActiva DEFAULT 1,
        FecExpira               DATETIME2(3) NULL,
        FecCreacion             DATETIME2(3) NOT NULL CONSTRAINT DF_SamlSession_FecCreacion DEFAULT sysutcdatetime(),
        FecRevocacion           DATETIME2(3) NULL,

        CONSTRAINT PK_SamlSession PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_SamlSession_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_SamlSession_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id),
        CONSTRAINT FK_SamlSession_ConfSaml FOREIGN KEY (IdConfSaml) REFERENCES dbo.ConfSaml(Id)
    );

    CREATE NONCLUSTERED INDEX IX_SamlSession_Tenant ON dbo.SamlSession(IdTenant);
    CREATE NONCLUSTERED INDEX IX_SamlSession_NameId ON dbo.SamlSession(NameId);
    CREATE NONCLUSTERED INDEX IX_SamlSession_Activa ON dbo.SamlSession(EsActiva) WHERE EsActiva = 1;
    CREATE NONCLUSTERED INDEX IX_SamlSession_Expira ON dbo.SamlSession(FecExpira) WHERE FecExpira IS NOT NULL AND EsActiva = 1;

    EXEC sys.sp_addextendedproperty N'MS_Description', N'Sesiones SAML activas por tenant. Almacena NameID, SessionIndex y attributes del assertion para control de sesiones SSO.', 'SCHEMA', N'dbo', 'TABLE', N'SamlSession';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'NameID del usuario del assertion SAML', 'SCHEMA', N'dbo', 'TABLE', N'SamlSession', 'COLUMN', N'NameId';
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Index de sesión SAML para LogoutRequest', 'SCHEMA', N'dbo', 'TABLE', N'SamlSession', 'COLUMN', N'SessionIndex';
END
GO

PRINT N'FASE 15 complete: ConfLdap, LdapSyncLog, ConfSaml, SamlSession tables created.';
GO
