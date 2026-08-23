-- =============================================================================
-- FASE 14: Federación de Identidades (ProvIden, ConfProvIden, IdenExt)
-- =============================================================================
-- Este script crea el subsistema de federación de identidades para PassPlat.
-- Compatible con usuarios locales existentes (no modifica autenticación actual).
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '=== FASE 14: Federación de Identidades ===';
GO

-- =============================================================================
-- 1. ProvIden - Catálogo global de proveedores de identidad
-- =============================================================================
PRINT 'Creando ProvIden...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ProvIden') AND type = 'U')
BEGIN
    CREATE TABLE dbo.ProvIden (
        Id          int             IDENTITY(1,1)   NOT NULL,
        Codigo      nvarchar(50)    NOT NULL,
        Nombre      nvarchar(100)   NOT NULL,
        TipoProveedor tinyint       NOT NULL,
        UrlIssuer   nvarchar(500)   NULL,
        Icono       nvarchar(50)    NULL,
        Orden       smallint        NOT NULL DEFAULT 0,
        Activo      bit             NOT NULL DEFAULT 1,
        FecCrea     datetime2       NOT NULL DEFAULT sysutcdatetime(),
        FecMod      datetime2       NULL,

        CONSTRAINT PK_ProvIden PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_ProvIden_Codigo UNIQUE (Codigo),
        CONSTRAINT CK_ProvIden_TipoProveedor CHECK (TipoProveedor BETWEEN 1 AND 10)
    );

    CREATE INDEX IX_ProvIden_Activo ON dbo.ProvIden (Activo) WHERE Activo = 1;
    CREATE INDEX IX_ProvIden_TipoProveedor ON dbo.ProvIden (TipoProveedor);
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Catálogo global de proveedores de identidad soportados por la plataforma (Google, GitHub, Microsoft, Apple, LinkedIn y futuros proveedores como SAML, LDAP o Entra ID).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Código único del proveedor (ej: GOOGLE, GITHUB, MICROSOFT)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'Codigo';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Nombre descriptivo del proveedor (ej: Google, GitHub, Microsoft)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'Nombre';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Tipo de proveedor: 1=OAuth2, 2=OpenIDConnect, 3=MicrosoftEntra, 4=LDAP, 5=SAML', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'TipoProveedor';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'URL del emisor (issuer) para validación de tokens ID', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'UrlIssuer';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Nombre del icono/material-icon para UI', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'Icono';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Orden de visualización en UI', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProvIden', @level2type = N'COLUMN', @level2name = N'Orden';
GO

-- Seed inicial
IF NOT EXISTS (SELECT 1 FROM dbo.ProvIden)
BEGIN
    INSERT INTO dbo.ProvIden (Codigo, Nombre, TipoProveedor, UrlIssuer, Icono, Orden) VALUES
        ('GOOGLE',     'Google',     2, 'https://accounts.google.com',         'google',      1),
        ('GITHUB',     'GitHub',     1, NULL,                                  'github',      2),
        ('MICROSOFT',  'Microsoft',  2, 'https://login.microsoftonline.com',   'microsoft',   3),
        ('APPLE',      'Apple',      2, 'https://appleid.apple.com',           'apple',       4),
        ('LINKEDIN',   'LinkedIn',   1, NULL,                                  'linkedin',    5);
    PRINT 'Seed ProvIden insertado: 5 proveedores';
END
GO

-- =============================================================================
-- 2. ConfProvIden - Configuración del proveedor por tenant
-- =============================================================================
PRINT 'Creando ConfProvIden...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ConfProvIden') AND type = 'U')
BEGIN
    CREATE TABLE dbo.ConfProvIden (
        Id              int             IDENTITY(1,1)   NOT NULL,
        IdTenant        int             NOT NULL,
        IdProvIden      int             NOT NULL,
        ClientId        nvarchar(500)   NOT NULL,
        ClientSecret    nvarchar(1000)  NOT NULL,   -- Cifrado con AES-256-GCM vía IEncryptionService
        Scopes          nvarchar(500)   NULL,
        Callback        nvarchar(500)   NOT NULL,
        RolDefecto      int             NULL,
        GuardarTokens   bit             NOT NULL DEFAULT 0,
        PermitirAutoLink bit            NOT NULL DEFAULT 0,
        AutoProvisionar bit             NOT NULL DEFAULT 0,
        Activo          bit             NOT NULL DEFAULT 1,
        FecCrea         datetime2       NOT NULL DEFAULT sysutcdatetime(),
        FecMod          datetime2       NULL,

        CONSTRAINT PK_ConfProvIden PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_ConfProvIden_TenantProveedor UNIQUE (IdTenant, IdProvIden),
        CONSTRAINT FK_ConfProvIden_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_ConfProvIden_ProvIden FOREIGN KEY (IdProvIden) REFERENCES dbo.ProvIden(Id),
        CONSTRAINT FK_ConfProvIden_RolDefecto FOREIGN KEY (RolDefecto) REFERENCES dbo.Roles(Id),
        CONSTRAINT CK_ConfProvIden_ClientSecret CHECK (LEN(ClientSecret) > 0)
    );

    CREATE INDEX IX_ConfProvIden_Activo ON dbo.ConfProvIden (IdTenant, IdProvIden) WHERE Activo = 1;
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Configuración de proveedores de identidad por tenant. Almacena credenciales OAuth/OIDC cifradas y políticas de auto-provisionamiento.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Client ID del proveedor (OAuth2/OIDC)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'ClientId';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Client Secret cifrado con AES-256-GCM. Nunca en texto plano.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'ClientSecret';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Scopes solicitados al proveedor separados por espacio', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'Scopes';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'URL de callback/redirect registrada en el proveedor', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'Callback';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Rol asignado por defecto al auto-provisionar o auto-vincular', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'RolDefecto';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Si 1, persiste AccessToken y RefreshToken en IdenExt.MetadataJson', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'GuardarTokens';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Si 1, vincula automáticamente usuarios existentes por email coincidente', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'PermitirAutoLink';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Si 1, crea automáticamente usuario local + identidad externa en primer login', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ConfProvIden', @level2type = N'COLUMN', @level2name = N'AutoProvisionar';
GO

-- =============================================================================
-- 3. IdenExt - Relación usuario-proveedor
-- =============================================================================
PRINT 'Creando IdenExt...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('IdenExt') AND type = 'U')
BEGIN
    CREATE TABLE dbo.IdenExt (
        Id              bigint          IDENTITY(1,1)   NOT NULL,
        IdUsuario       int             NOT NULL,
        IdProvIden      int             NOT NULL,
        IdTenant        int             NOT NULL,
        SubExterno      nvarchar(255)   NOT NULL,   -- subject/sub del proveedor
        EmailExterno    nvarchar(255)   NULL,
        NombreExterno   nvarchar(255)   NULL,
        Avatar          nvarchar(500)   NULL,
        MetadataJson    nvarchar(max)   NULL,       -- datos variables del proveedor
        EsPrincipal     bit             NOT NULL DEFAULT 0,
        Activo          bit             NOT NULL DEFAULT 1,
        Eliminado       bit             NOT NULL DEFAULT 0,
        FecEliminacion  datetime2       NULL,
        IdUsuarioElimina int            NULL,
        UltimoLogin     datetime2       NULL,
        FecCrea         datetime2       NOT NULL DEFAULT sysutcdatetime(),
        FecMod          datetime2       NULL,

        CONSTRAINT PK_IdenExt PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UK_IdenExt_ProveedorSub UNIQUE (IdProvIden, SubExterno),
        CONSTRAINT UK_IdenExt_UsuarioProveedor UNIQUE (IdUsuario, IdProvIden),
        CONSTRAINT FK_IdenExt_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id),
        CONSTRAINT FK_IdenExt_ProvIden FOREIGN KEY (IdProvIden) REFERENCES dbo.ProvIden(Id),
        CONSTRAINT FK_IdenExt_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_IdenExt_UsuarioElimina FOREIGN KEY (IdUsuarioElimina) REFERENCES dbo.Usuarios(Id),
        CONSTRAINT CK_IdenExt_Sub CHECK (LEN(SubExterno) > 0)
    );

    CREATE INDEX IX_IdenExt_Usuario ON dbo.IdenExt (IdUsuario) WHERE Eliminado = 0;
    CREATE INDEX IX_IdenExt_EmailExterno ON dbo.IdenExt (EmailExterno) WHERE EmailExterno IS NOT NULL AND Eliminado = 0;
    CREATE INDEX IX_IdenExt_UltimoLogin ON dbo.IdenExt (UltimoLogin DESC) WHERE Activo = 1 AND Eliminado = 0;
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Relación entre usuarios locales y proveedores de identidad externos. Un usuario puede tener múltiples identidades externas (Google, GitHub, Microsoft, etc.) simultáneamente.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Identificador único del usuario en el proveedor externo (sub/claim)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'SubExterno';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Email registrado en el proveedor externo (puede ser NULL en GitHub/Apple)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'EmailExterno';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'JSON con datos variables del proveedor: tokens, perfil completo, claims adicionales', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'MetadataJson';
EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Si 1, esta es la identidad externa principal del usuario', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'IdenExt', @level2type = N'COLUMN', @level2name = N'EsPrincipal';
GO

-- =============================================================================
-- 4. AudIdenExt - Auditoría específica
-- =============================================================================
PRINT 'Creando AudIdenExt...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('AudIdenExt') AND type = 'U')
BEGIN
    CREATE TABLE dbo.AudIdenExt (
        Id              bigint          IDENTITY(1,1)   NOT NULL,
        IdTenant        int             NOT NULL,
        IdProvIden      int             NOT NULL,
        IdUsuario       int             NULL,
        SubExterno      nvarchar(255)   NULL,
        Evento          nvarchar(100)   NOT NULL,
        Resultado       nvarchar(50)    NOT NULL,
        Detalle         nvarchar(max)   NULL,
        IP              nvarchar(45)    NULL,
        UserAgent       nvarchar(500)   NULL,
        CorrelationId   nvarchar(50)    NULL,
        FecEvento       datetime2       NOT NULL DEFAULT sysutcdatetime(),

        CONSTRAINT PK_AudIdenExt PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AudIdenExt_Tenant FOREIGN KEY (IdTenant) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_AudIdenExt_ProvIden FOREIGN KEY (IdProvIden) REFERENCES dbo.ProvIden(Id),
        CONSTRAINT FK_AudIdenExt_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(Id)
    );

    CREATE INDEX IX_AudIdenExt_TenantFecha ON dbo.AudIdenExt (IdTenant, FecEvento DESC);
    CREATE INDEX IX_AudIdenExt_ProvIden ON dbo.AudIdenExt (IdProvIden, FecEvento DESC);
    CREATE INDEX IX_AudIdenExt_Usuario ON dbo.AudIdenExt (IdUsuario, FecEvento DESC);
    CREATE INDEX IX_AudIdenExt_CorrelationId ON dbo.AudIdenExt (CorrelationId);
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Registro de auditoría específico para eventos de identidad externa (login, vinculación, auto-provisionamiento, errores).', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AudIdenExt';
GO

-- =============================================================================
-- 5. Nuevos ResultadosAcceso para federación
-- =============================================================================
PRINT 'Insertando nuevos ResultadosAcceso...';
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthProvisioning')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthProvisioning', 'Usuario creado vía auto-provisionamiento OAuth/OIDC', 1);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthLogin')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthLogin', 'Login exitoso vía proveedor externo', 1);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthProviderDisabled')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthProviderDisabled', 'Proveedor externo deshabilitado', 0);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthIdentityLinked')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthIdentityLinked', 'Vinculación exitosa de identidad externa', 1);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthIdentityRevoked')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthIdentityRevoked', 'Identidad externa revocada', 0);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthProviderError')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthProviderError', 'Error de comunicación con proveedor externo', 0);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthUserWithoutEmail')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthUserWithoutEmail', 'Usuario externo sin email (no se puede auto-vincular)', 0);
GO
IF NOT EXISTS (SELECT 1 FROM dbo.ResultadosAcceso WHERE Nombre = 'OAuthAutoLinkDenied')
    INSERT INTO dbo.ResultadosAcceso (Nombre, Descripcion, EsExitoso) VALUES ('OAuthAutoLinkDenied', 'Auto-vinculación denegada por política del tenant', 0);
GO

-- =============================================================================
-- 6. SP_Auth_LoginExterno - Orquestador de login externo
-- =============================================================================
PRINT 'Creando SP_Auth_LoginExterno...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_Auth_LoginExterno') AND type = 'P')
    DROP PROCEDURE dbo.SP_Auth_LoginExterno;
GO

CREATE PROCEDURE dbo.SP_Auth_LoginExterno
    @IdTenant           int,
    @IdApp              int,
    @IdProvIden         int,
    @SubExterno         nvarchar(255),
    @EmailExterno       nvarchar(255) = NULL,
    @NombreExterno      nvarchar(255) = NULL,
    @Avatar             nvarchar(500) = NULL,
    @MetadataJson       nvarchar(max) = NULL,
    @IP                 nvarchar(45) = NULL,
    @UserAgent          nvarchar(500) = NULL,
    @IdDisp             int = NULL,
    @IdAgente           int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdUsuario int,
            @IdRolDefecto int,
            @PermitirAutoLink bit,
            @AutoProvisionar bit,
            @GuardarTokens bit,
            @IdIdentidad bigint,
            @IdResultado int,
            @Mensaje nvarchar(500),
            @DetResultado nvarchar(500),
            @IdMFAPrincipal int,
            @ReqCambioPwd bit,
            @PwdExpirada bit,
            @EsSistema bit;

    DECLARE @TablaIntentos TABLE (IntentosFallidos tinyint);

    -- IDs de resultados (resueltos desde catálogo)
    DECLARE @ID_Exitoso int, @ID_Provisioning int, @ID_OAuthLogin int,
            @ID_OAuthLinked int, @ID_ProviderDisabled int, @ID_ProviderError int,
            @ID_UserWithoutEmail int, @ID_AutoLinkDenied int, @ID_ErrorSistema int,
            @ID_MFARequerido int, @ID_CuentaInactiva int, @ID_SinAccesoApp int;

    SELECT
        @ID_Exitoso          = MAX(CASE WHEN Nombre = 'Exitoso'               THEN Id END),
        @ID_OAuthLogin       = MAX(CASE WHEN Nombre = 'OAuthLogin'            THEN Id END),
        @ID_OAuthLinked      = MAX(CASE WHEN Nombre = 'OAuthIdentityLinked'   THEN Id END),
        @ID_Provisioning     = MAX(CASE WHEN Nombre = 'OAuthProvisioning'     THEN Id END),
        @ID_ProviderDisabled = MAX(CASE WHEN Nombre = 'OAuthProviderDisabled' THEN Id END),
        @ID_ProviderError    = MAX(CASE WHEN Nombre = 'OAuthProviderError'    THEN Id END),
        @ID_UserWithoutEmail = MAX(CASE WHEN Nombre = 'OAuthUserWithoutEmail' THEN Id END),
        @ID_AutoLinkDenied   = MAX(CASE WHEN Nombre = 'OAuthAutoLinkDenied'   THEN Id END),
        @ID_MFARequerido     = MAX(CASE WHEN Nombre = 'MFARequerido'          THEN Id END),
        @ID_CuentaInactiva   = MAX(CASE WHEN Nombre = 'CuentaInactiva'        THEN Id END),
        @ID_SinAccesoApp     = MAX(CASE WHEN Nombre = 'SinAccesoApp'          THEN Id END),
        @ID_ErrorSistema     = MAX(CASE WHEN Nombre = 'ErrorSistema'          THEN Id END)
    FROM dbo.ResultadosAcceso;

    BEGIN TRY
        -- Validar que el proveedor esté activo y configurado para este tenant
        SELECT
            @IdRolDefecto = c.RolDefecto,
            @PermitirAutoLink = c.PermitirAutoLink,
            @AutoProvisionar = c.AutoProvisionar,
            @GuardarTokens = c.GuardarTokens
        FROM dbo.ConfProvIden c
        INNER JOIN dbo.ProvIden p ON p.Id = c.IdProvIden
        WHERE c.IdTenant = @IdTenant AND c.IdProvIden = @IdProvIden AND c.Activo = 1 AND p.Activo = 1;

        IF @@ROWCOUNT = 0
        BEGIN
            SET @IdResultado = @ID_ProviderDisabled;
            SET @Mensaje = 'Proveedor de identidad no configurado o deshabilitado';
            SET @DetResultado = 'IdProvIden=' + ISNULL(CAST(@IdProvIden AS nvarchar), 'NULL') + ', IdTenant=' + ISNULL(CAST(@IdTenant AS nvarchar), 'NULL');
            GOTO Finalizar;
        END

        -- Buscar identidad externa existente
        SELECT @IdIdentidad = Id, @IdUsuario = IdUsuario
        FROM dbo.IdenExt
        WHERE IdProvIden = @IdProvIden AND SubExterno = @SubExterno AND Eliminado = 0;

        IF @IdIdentidad IS NOT NULL
        BEGIN
            -- ESCENARIO A: Usuario ya vinculado
            SET @IdResultado = @ID_OAuthLogin;
            SET @Mensaje = 'Login externo exitoso';
            SET @DetResultado = 'Vinculación existente';

            -- Actualizar último login y metadata
            UPDATE dbo.IdenExt
            SET UltimoLogin = SYSUTCDATETIME(),
                EmailExterno = ISNULL(@EmailExterno, EmailExterno),
                NombreExterno = ISNULL(@NombreExterno, NombreExterno),
                Avatar = ISNULL(@Avatar, Avatar),
                MetadataJson = CASE WHEN @GuardarTokens = 1 AND @MetadataJson IS NOT NULL THEN @MetadataJson ELSE MetadataJson END,
                FecMod = SYSUTCDATETIME()
            WHERE Id = @IdIdentidad;

            GOTO VerificarUsuario;
        END

        -- ESCENARIO B: Auto-link por email
        IF @PermitirAutoLink = 1 AND @EmailExterno IS NOT NULL
        BEGIN
            SELECT @IdUsuario = Id
            FROM dbo.Usuarios
            WHERE Email = @EmailExterno AND IdTenant = @IdTenant AND Eliminado = 0;

            IF @IdUsuario IS NOT NULL
            BEGIN
                INSERT INTO dbo.IdenExt (IdUsuario, IdProvIden, IdTenant, SubExterno, EmailExterno, NombreExterno, Avatar, MetadataJson, UltimoLogin)
                VALUES (@IdUsuario, @IdProvIden, @IdTenant, @SubExterno, @EmailExterno, @NombreExterno, @Avatar,
                        CASE WHEN @GuardarTokens = 1 THEN @MetadataJson ELSE NULL END, SYSUTCDATETIME());

                SET @IdIdentidad = SCOPE_IDENTITY();
                SET @IdResultado = @ID_OAuthLinked;
                SET @Mensaje = 'Identidad externa vinculada automáticamente';
                SET @DetResultado = 'Auto-link por email';
                GOTO VerificarUsuario;
            END
        END

        -- ESCENARIO C: Auto-provisionar
        IF @AutoProvisionar = 1
        BEGIN
            -- Validar que tenga email (obligatorio para creación)
            IF @EmailExterno IS NULL
            BEGIN
                SET @IdResultado = @ID_UserWithoutEmail;
                SET @Mensaje = 'No se puede auto-provisionar: usuario sin email';
                SET @DetResultado = 'El proveedor no proporcionó email';
                GOTO Finalizar;
            END

            -- Crear usuario local
            INSERT INTO dbo.Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd)
            VALUES (@IdTenant, 1, @SubExterno, @EmailExterno, 1, ISNULL(@NombreExterno, @SubExterno), '' , 0);

            SET @IdUsuario = SCOPE_IDENTITY();

            -- Vincular identidad externa
            INSERT INTO dbo.IdenExt (IdUsuario, IdProvIden, IdTenant, SubExterno, EmailExterno, NombreExterno, Avatar, MetadataJson, EsPrincipal, UltimoLogin)
            VALUES (@IdUsuario, @IdProvIden, @IdTenant, @SubExterno, @EmailExterno, @NombreExterno, @Avatar,
                    CASE WHEN @GuardarTokens = 1 THEN @MetadataJson ELSE NULL END, 1, SYSUTCDATETIME());

            SET @IdIdentidad = SCOPE_IDENTITY();

            -- Asignar rol por defecto si está configurado
            IF @IdRolDefecto IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.Accesos WHERE IdUsuario = @IdUsuario AND IdApp = @IdApp AND IdTenant = @IdTenant)
                BEGIN
                    INSERT INTO dbo.Accesos (IdUsuario, IdTenant, IdApp, IdRol)
                    VALUES (@IdUsuario, @IdTenant, @IdApp, @IdRolDefecto);
                END
            END

            SET @IdResultado = @ID_Provisioning;
            SET @Mensaje = 'Usuario creado vía auto-provisionamiento';
            SET @DetResultado = 'Provisioning exitoso';
            GOTO VerificarUsuario;
        END

        -- Si llegamos aquí, no se pudo vincular ni auto-provisionar
        SET @IdResultado = @ID_AutoLinkDenied;
        SET @Mensaje = 'No se pudo autenticar: usuario no vinculado y auto-provisionamiento deshabilitado';
        SET @DetResultado = 'Auto-link denegado';
        GOTO Finalizar;

        -----------------------------------------------------------------------
        VerificarUsuario:
        -----------------------------------------------------------------------
        -- Verificar estado del usuario
        IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Id = @IdUsuario AND IdEstado = 2) -- Inactivo
        BEGIN
            SET @IdResultado = @ID_CuentaInactiva;
            SET @Mensaje = 'Cuenta inactiva';
            SET @DetResultado = 'Cuenta inactiva';
            GOTO Finalizar;
        END

        -- Verificar acceso a la aplicación
        IF NOT EXISTS (
            SELECT 1 FROM dbo.Accesos
            WHERE IdUsuario = @IdUsuario AND IdApp = @IdApp AND IdTenant = @IdTenant AND Activo = 1
        )
        BEGIN
            SET @IdResultado = @ID_SinAccesoApp;
            SET @Mensaje = 'Sin acceso a la aplicación';
            SET @DetResultado = 'Sin acceso app';
            GOTO Finalizar;
        END

        -- Verificar MFA
        SELECT @IdMFAPrincipal = Id
        FROM dbo.MFA
        WHERE IdUsuario = @IdUsuario AND IdEstado = 1 AND EsPrincipal = 1;

        IF @IdMFAPrincipal IS NOT NULL
        BEGIN
            SET @IdResultado = @ID_MFARequerido;
            SET @Mensaje = 'MFA requerido';
            SET @DetResultado = 'MFA requerido tras autenticación externa';

            SELECT @ReqCambioPwd = ReqCambioPwd, @EsSistema = EsSistema
            FROM dbo.Usuarios WHERE Id = @IdUsuario;

            -- Devolver resultado con MFA pendiente
            SELECT
                @ID_MFARequerido AS Resultado,
                @Mensaje AS Mensaje,
                @IdUsuario AS IdUsuario,
                @IdTenant AS IdTenant,
                ISNULL(@EsSistema, 0) AS EsSistema,
                @ReqCambioPwd AS ReqCambioPwd,
                CAST(0 AS bit) AS PwdExpirada,
                CAST(0 AS bit) AS RequiereReHash,
                @IdMFAPrincipal AS IdMFAPrincipal,
                NULL AS IdBloqueo,
                NULL AS FecFinBloqueo,
                NULL AS IntentosRestantes;

            INSERT INTO dbo.IntentosAcceso (IdUsuario, IdTenant, IdApp, IdResultado, DetResultado, Exitoso, NomUsuarioIntentado)
            VALUES (@IdUsuario, @IdTenant, @IdApp, @ID_MFARequerido, 'MFA requerido tras OAuth', 0, ISNULL(@EmailExterno, @SubExterno));

            RETURN;
        END

        -- Éxito: login completo
        SET @IdResultado = @ID_Exitoso;
        SET @Mensaje = 'Login exitoso';
        SET @DetResultado = 'Autenticación externa exitosa';

        GOTO Finalizar;

    END TRY
    BEGIN CATCH
        SET @IdResultado = @ID_ErrorSistema;
        SET @Mensaje = ERROR_MESSAGE();
        SET @DetResultado = 'Error en SP_Auth_LoginExterno: ' + ERROR_MESSAGE();
    END CATCH

    Finalizar:

    -- Registrar intento de acceso
    INSERT INTO dbo.IntentosAcceso
        (IdUsuario, IdTenant, IdApp, IdResultado, DetResultado, Exitoso, NomUsuarioIntentado,
         IdDisp, IdAgente, IdIP)
    VALUES
        (@IdUsuario, @IdTenant, @IdApp, @IdResultado, @DetResultado,
         CASE WHEN @IdResultado IN (@ID_Exitoso, @ID_OAuthLogin, @ID_OAuthLinked, @ID_Provisioning) THEN 1 ELSE 0 END,
         ISNULL(@EmailExterno, @SubExterno), @IdDisp, @IdAgente, NULL);

    -- Devolver resultado unificado (mismo contrato que SP_Auth_Login)
    SELECT
        @IdResultado AS Resultado,
        @Mensaje AS Mensaje,
        @IdUsuario AS IdUsuario,
        @IdTenant AS IdTenant,
        ISNULL((SELECT EsSistema FROM dbo.Usuarios WHERE Id = @IdUsuario), 0) AS EsSistema,
        (SELECT ReqCambioPwd FROM dbo.Usuarios WHERE Id = @IdUsuario) AS ReqCambioPwd,
        CAST(0 AS bit) AS PwdExpirada,
        CAST(0 AS bit) AS RequiereReHash,
        NULL AS IdMFAPrincipal,
        NULL AS IdBloqueo,
        NULL AS FecFinBloqueo,
        NULL AS IntentosRestantes;
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Orquestador de login mediante proveedor externo. Implementa tres escenarios: A) Usuario ya vinculado, B) Auto-link por email, C) Auto-provisionamiento. Mismo contrato de salida que SP_Auth_Login.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'PROCEDURE', @level1name = N'SP_Auth_LoginExterno';
GO

-- =============================================================================
-- 7. SP_ProvIden_BuscarUsuario - Busca usuario por sub externo
-- =============================================================================
PRINT 'Creando SP_ProvIden_BuscarUsuario...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_ProvIden_BuscarUsuario') AND type = 'P')
    DROP PROCEDURE dbo.SP_ProvIden_BuscarUsuario;
GO

CREATE PROCEDURE dbo.SP_ProvIden_BuscarUsuario
    @IdProvIden  int,
    @SubExterno  nvarchar(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ie.Id, ie.IdUsuario, ie.IdTenant, ie.SubExterno, ie.EmailExterno,
           ie.NombreExterno, ie.Avatar, ie.EsPrincipal, ie.UltimoLogin,
           u.NomUsuario, u.Email, u.Nombre, u.Apellido, u.IdEstado, u.Eliminado
    FROM dbo.IdenExt ie
    INNER JOIN dbo.Usuarios u ON u.Id = ie.IdUsuario
    WHERE ie.IdProvIden = @IdProvIden AND ie.SubExterno = @SubExterno AND ie.Eliminado = 0;
END
GO

-- =============================================================================
-- 8. SP_ProvIden_VincularUsuario - Vincula usuario local con proveedor
-- =============================================================================
PRINT 'Creando SP_ProvIden_VincularUsuario...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_ProvIden_VincularUsuario') AND type = 'P')
    DROP PROCEDURE dbo.SP_ProvIden_VincularUsuario;
GO

CREATE PROCEDURE dbo.SP_ProvIden_VincularUsuario
    @IdUsuario      int,
    @IdProvIden     int,
    @IdTenant       int,
    @SubExterno     nvarchar(255),
    @EmailExterno   nvarchar(255) = NULL,
    @NombreExterno  nvarchar(255) = NULL,
    @Avatar         nvarchar(500) = NULL,
    @GuardarTokens  bit = 0,
    @MetadataJson   nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Validar que no exista ya la vinculación
    IF EXISTS (SELECT 1 FROM dbo.IdenExt WHERE IdUsuario = @IdUsuario AND IdProvIden = @IdProvIden AND Eliminado = 0)
    BEGIN
        RAISERROR('El usuario ya está vinculado a este proveedor', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.IdenExt WHERE IdProvIden = @IdProvIden AND SubExterno = @SubExterno AND Eliminado = 0)
    BEGIN
        RAISERROR('El subexterno ya está vinculado a otro usuario', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.IdenExt (IdUsuario, IdProvIden, IdTenant, SubExterno, EmailExterno, NombreExterno, Avatar, MetadataJson, UltimoLogin)
    VALUES (@IdUsuario, @IdProvIden, @IdTenant, @SubExterno, @EmailExterno, @NombreExterno, @Avatar,
            CASE WHEN @GuardarTokens = 1 THEN @MetadataJson ELSE NULL END, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS Id;
END
GO

-- =============================================================================
-- 9. SP_ProvIden_ActualizarPerfil - Actualiza perfil de identidad externa
-- =============================================================================
PRINT 'Creando SP_ProvIden_ActualizarPerfil...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_ProvIden_ActualizarPerfil') AND type = 'P')
    DROP PROCEDURE dbo.SP_ProvIden_ActualizarPerfil;
GO

CREATE PROCEDURE dbo.SP_ProvIden_ActualizarPerfil
    @IdIdentidad    bigint,
    @EmailExterno   nvarchar(255) = NULL,
    @NombreExterno  nvarchar(255) = NULL,
    @Avatar         nvarchar(500) = NULL,
    @MetadataJson   nvarchar(max) = NULL,
    @GuardarTokens  bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.IdenExt
    SET EmailExterno = ISNULL(@EmailExterno, EmailExterno),
        NombreExterno = ISNULL(@NombreExterno, NombreExterno),
        Avatar = ISNULL(@Avatar, Avatar),
        MetadataJson = CASE WHEN @GuardarTokens = 1 AND @MetadataJson IS NOT NULL THEN @MetadataJson ELSE MetadataJson END,
        FecMod = SYSUTCDATETIME()
    WHERE Id = @IdIdentidad AND Eliminado = 0;

    SELECT @@ROWCOUNT AS FilasAfectadas;
END
GO

-- =============================================================================
-- 10. SP_ProvIden_RegistrarAuditoria
-- =============================================================================
PRINT 'Creando SP_ProvIden_RegistrarAuditoria...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_ProvIden_RegistrarAuditoria') AND type = 'P')
    DROP PROCEDURE dbo.SP_ProvIden_RegistrarAuditoria;
GO

CREATE PROCEDURE dbo.SP_ProvIden_RegistrarAuditoria
    @IdTenant       int,
    @IdProvIden     int,
    @IdUsuario      int = NULL,
    @SubExterno     nvarchar(255) = NULL,
    @Evento         nvarchar(100),
    @Resultado      nvarchar(50),
    @Detalle        nvarchar(max) = NULL,
    @IP             nvarchar(45) = NULL,
    @UserAgent      nvarchar(500) = NULL,
    @CorrelationId  nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.AudIdenExt
        (IdTenant, IdProvIden, IdUsuario, SubExterno, Evento, Resultado, Detalle, IP, UserAgent, CorrelationId)
    VALUES
        (@IdTenant, @IdProvIden, @IdUsuario, @SubExterno, @Evento, @Resultado, @Detalle, @IP, @UserAgent, @CorrelationId);

    SELECT SCOPE_IDENTITY() AS Id;
END
GO

-- =============================================================================
-- 11. SP_Dashboard_IdenExt - Indicadores de federación
-- =============================================================================
PRINT 'Creando SP_Dashboard_IdenExt...';
GO

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SP_Dashboard_IdenExt') AND type = 'P')
    DROP PROCEDURE dbo.SP_Dashboard_IdenExt;
GO

CREATE PROCEDURE dbo.SP_Dashboard_IdenExt
    @IdTenant int
AS
BEGIN
    SET NOCOUNT ON;

    -- Usuarios externos: usuarios que tienen al menos una identidad externa activa
    SELECT
        (SELECT COUNT(DISTINCT IdUsuario) FROM dbo.IdenExt WHERE IdTenant = @IdTenant AND Eliminado = 0) AS UsuariosExternos,
        (SELECT COUNT(*) FROM dbo.Usuarios WHERE IdTenant = @IdTenant AND Eliminado = 0) AS TotalUsuarios,
        (SELECT COUNT(*) FROM dbo.Usuarios WHERE IdTenant = @IdTenant AND Eliminado = 0 AND (Email IS NULL OR Email = '')) AS UsuariosSinEmail;

    -- Usuarios por proveedor
    SELECT
        p.Codigo,
        p.Nombre,
        p.Icono,
        COUNT(ie.Id) AS UsuariosVinculados
    FROM dbo.ProvIden p
    LEFT JOIN dbo.IdenExt ie ON ie.IdProvIden = p.Id AND ie.IdTenant = @IdTenant AND ie.Eliminado = 0
    GROUP BY p.Codigo, p.Nombre, p.Icono, p.Orden
    ORDER BY p.Orden;

    -- Últimos logins externos
    SELECT TOP 10
        ie.UltimoLogin AS Fecha,
        p.Nombre AS Proveedor,
        ie.NombreExterno,
        ie.EmailExterno,
        ie.SubExterno
    FROM dbo.IdenExt ie
    INNER JOIN dbo.ProvIden p ON p.Id = ie.IdProvIden
    WHERE ie.IdTenant = @IdTenant AND ie.Eliminado = 0 AND ie.UltimoLogin IS NOT NULL
    ORDER BY ie.UltimoLogin DESC;

    -- Proveedores habilitados para este tenant
    SELECT
        p.Codigo,
        p.Nombre,
        p.Icono,
        c.Activo,
        c.PermitirAutoLink,
        c.AutoProvisionar,
        c.RolDefecto
    FROM dbo.ConfProvIden c
    INNER JOIN dbo.ProvIden p ON p.Id = c.IdProvIden
    WHERE c.IdTenant = @IdTenant;

    -- Últimas auditorías de identidad externa
    SELECT TOP 20
        a.FecEvento,
        p.Nombre AS Proveedor,
        a.Evento,
        a.Resultado,
        a.SubExterno,
        a.IP
    FROM dbo.AudIdenExt a
    INNER JOIN dbo.ProvIden p ON p.Id = a.IdProvIden
    WHERE a.IdTenant = @IdTenant
    ORDER BY a.FecEvento DESC;
END
GO

EXEC sp_addextendedproperty @name = N'MS_Description', @value = N'Indicadores de federación de identidades para el dashboard. Retorna 4 resultsets: resumen, usuarios por proveedor, últimos logins, proveedores habilitados y auditorías recientes.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'PROCEDURE', @level1name = N'SP_Dashboard_IdenExt';
GO

PRINT '=== FASE 14: Migración completada exitosamente ===';
GO
