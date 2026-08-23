-- ============================================================================
-- FASE 15: Hybrid User Model + Security Fixes
-- Date: 2026-07-06
-- Purpose: Add TienePasswordLocal, MetodoAutenticacion, RequiereMFALocal,
--          fix OAuth password reset, fix providers
-- ============================================================================

SET NOCOUNT ON;
GO

-- ============================================================================
-- 1. Add TienePasswordLocal to Usuarios (FIRST - before any SP changes)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'TienePasswordLocal')
BEGIN
    ALTER TABLE dbo.Usuarios ADD TienePasswordLocal bit NOT NULL 
        CONSTRAINT DF_Usuarios_TienePasswordLocal DEFAULT(0);
    
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Indica si el usuario tiene contrasena local configurada. 0=Solo OAuth, 1=Local o Hibrido.', 'SCHEMA', N'dbo', 'TABLE', N'Usuarios', 'COLUMN', N'TienePasswordLocal';
    
    PRINT 'TienePasswordLocal column added to Usuarios';
END
ELSE
    PRINT 'TienePasswordLocal column already exists';
GO

-- ============================================================================
-- 2. Update existing users: Local users get TienePasswordLocal=1
-- ============================================================================
UPDATE u SET u.TienePasswordLocal = 1
FROM dbo.Usuarios u
WHERE u.Eliminado = 0
  AND EXISTS (SELECT 1 FROM dbo.HistorialPwd h WHERE h.IdUsuario = u.Id AND h.EsActual = 1);

PRINT 'Updated existing local users with TienePasswordLocal=1';
GO

-- ============================================================================
-- 3. Add MetodoAutenticacion to IntentosAcceso (BEFORE any SP changes)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.IntentosAcceso') AND name = 'MetodoAutenticacion')
BEGIN
    ALTER TABLE dbo.IntentosAcceso ADD MetodoAutenticacion nvarchar(20) NOT NULL 
        CONSTRAINT DF_IntentosAcceso_MetodoAutenticacion DEFAULT('Local');
    
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Metodo de autenticacion utilizado: Local, Google, GitHub, LinkedIn, Facebook, Instagram.', 'SCHEMA', N'dbo', 'TABLE', N'IntentosAcceso', 'COLUMN', N'MetodoAutenticacion';
    
    PRINT 'MetodoAutenticacion column added to IntentosAcceso';
END
ELSE
    PRINT 'MetodoAutenticacion column already exists';
GO

-- Create filtered index in separate batch (column must exist before compile)
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Intentos_MetodoAuth' AND object_id = OBJECT_ID('dbo.IntentosAcceso'))
    CREATE NONCLUSTERED INDEX IX_Intentos_MetodoAuth ON dbo.IntentosAcceso(MetodoAutenticacion, FecIntento) WHERE MetodoAutenticacion <> 'Local';
GO

-- ============================================================================
-- 4. Add RequiereMFALocal to ConfProvIden (BEFORE any SP changes)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConfProvIden') AND name = 'RequiereMFALocal')
BEGIN
    ALTER TABLE dbo.ConfProvIden ADD RequiereMFALocal bit NOT NULL 
        CONSTRAINT DF_ConfProvIden_RequiereMFALocal DEFAULT(0);
    
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Indica si se requiere MFA local despues de autenticacion externa. 0=No requiere, 1=Requiere MFA PassPlat.', 'SCHEMA', N'dbo', 'TABLE', N'ConfProvIden', 'COLUMN', N'RequiereMFALocal';
    
    PRINT 'RequiereMFALocal column added to ConfProvIden';
END
ELSE
    PRINT 'RequiereMFALocal column already exists';
GO

-- ============================================================================
-- 5. Deactivate Microsoft/Apple, Insert Instagram/Facebook
-- ============================================================================
UPDATE dbo.ProvIden SET Activo = 0 WHERE Codigo IN ('MICROSOFT', 'APPLE');

IF NOT EXISTS (SELECT * FROM dbo.ProvIden WHERE Codigo = 'INSTAGRAM')
BEGIN
    INSERT INTO dbo.ProvIden (Codigo, Nombre, TipoProveedor, Protocolo, EndpointAutorizacion, EndpointToken, EndpointUserInfo, SoportaPKCE, SoportaRefreshToken, SoportaMFA, Icono, Orden, Activo, FecCrea)
    VALUES ('INSTAGRAM', 'Instagram', 1, 'OAuth2', 
            'https://api.instagram.com/oauth/authorize', 
            'https://api.instagram.com/oauth/access_token', 
            'https://graph.instagram.com/me', 
            1, 0, 0, 'camera_alt', 6, 1, GETUTCDATE());
END

IF NOT EXISTS (SELECT * FROM dbo.ProvIden WHERE Codigo = 'FACEBOOK')
BEGIN
    INSERT INTO dbo.ProvIden (Codigo, Nombre, TipoProveedor, Protocolo, EndpointAutorizacion, EndpointToken, EndpointUserInfo, SoportaPKCE, SoportaRefreshToken, SoportaMFA, Icono, Orden, Activo, FecCrea)
    VALUES ('FACEBOOK', 'Facebook', 1, 'OAuth2', 
            'https://www.facebook.com/v18.0/dialog/oauth', 
            'https://graph.facebook.com/v18.0/oauth/access_token', 
            'https://graph.facebook.com/v18.0/me', 
            1, 0, 0, 'facebook', 7, 1, GETUTCDATE());
END
GO

-- ============================================================================
-- 6. Update SP_Auth_LoginExterno (all columns exist now)
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.SP_Auth_LoginExterno
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

        SELECT @IdIdentidad = Id, @IdUsuario = IdUsuario
        FROM dbo.IdenExt
        WHERE IdProvIden = @IdProvIden AND SubExterno = @SubExterno AND Eliminado = 0;

        IF @IdIdentidad IS NOT NULL
        BEGIN
            SET @IdResultado = @ID_OAuthLogin;
            SET @Mensaje = 'Login externo exitoso';
            SET @DetResultado = 'Vinculacion existente';

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
                SET @Mensaje = 'Identidad externa vinculada automaticamente';
                SET @DetResultado = 'Auto-link por email';
                GOTO VerificarUsuario;
            END
        END

        IF @AutoProvisionar = 1
        BEGIN
            IF @EmailExterno IS NULL
            BEGIN
                SET @IdResultado = @ID_UserWithoutEmail;
                SET @Mensaje = 'No se puede auto-provisionar: usuario sin email';
                SET @DetResultado = 'El proveedor no proporciono email';
                GOTO Finalizar;
            END

            INSERT INTO dbo.Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, TienePasswordLocal)
            VALUES (@IdTenant, 1, @SubExterno, @EmailExterno, 1, ISNULL(@NombreExterno, @SubExterno), '', 0, 0);

            SET @IdUsuario = SCOPE_IDENTITY();

            INSERT INTO dbo.IdenExt (IdUsuario, IdProvIden, IdTenant, SubExterno, EmailExterno, NombreExterno, Avatar, MetadataJson, EsPrincipal, UltimoLogin)
            VALUES (@IdUsuario, @IdProvIden, @IdTenant, @SubExterno, @EmailExterno, @NombreExterno, @Avatar,
                    CASE WHEN @GuardarTokens = 1 THEN @MetadataJson ELSE NULL END, 1, SYSUTCDATETIME());

            SET @IdIdentidad = SCOPE_IDENTITY();

            IF @IdRolDefecto IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.Accesos WHERE IdUsuario = @IdUsuario AND IdApp = @IdApp AND IdTenant = @IdTenant)
                BEGIN
                    INSERT INTO dbo.Accesos (IdUsuario, IdTenant, IdApp, IdRol)
                    VALUES (@IdUsuario, @IdTenant, @IdApp, @IdRolDefecto);
                END
            END

            SET @IdResultado = @ID_Provisioning;
            SET @Mensaje = 'Usuario creado via auto-provisionamiento';
            SET @DetResultado = 'Provisioning exitoso';
            GOTO VerificarUsuario;
        END

        SET @IdResultado = @ID_AutoLinkDenied;
        SET @Mensaje = 'No se pudo autenticar: usuario no vinculado y auto-provisionamiento deshabilitado';
        SET @DetResultado = 'Auto-link denegado';
        GOTO Finalizar;

        VerificarUsuario:

        IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Id = @IdUsuario AND IdEstado = 2)
        BEGIN
            SET @IdResultado = @ID_CuentaInactiva;
            SET @Mensaje = 'Cuenta inactiva';
            SET @DetResultado = 'Cuenta inactiva';
            GOTO Finalizar;
        END

        IF NOT EXISTS (
            SELECT 1 FROM dbo.Accesos
            WHERE IdUsuario = @IdUsuario AND IdApp = @IdApp AND IdTenant = @IdTenant AND Activo = 1
        )
        BEGIN
            SET @IdResultado = @ID_SinAccesoApp;
            SET @Mensaje = 'Sin acceso a la aplicacion';
            SET @DetResultado = 'Sin acceso app';
            GOTO Finalizar;
        END

        DECLARE @IdEstadoActivoMFA int = (SELECT Id FROM dbo.EstadosMFA WHERE Codigo = 'ACTIVO');
        SELECT @IdMFAPrincipal = Id
        FROM dbo.MFA
        WHERE IdUsuario = @IdUsuario AND IdEstado = @IdEstadoActivoMFA AND EsPrincipal = 1;

        IF @IdMFAPrincipal IS NOT NULL
        BEGIN
            SET @IdResultado = @ID_MFARequerido;
            SET @Mensaje = 'MFA requerido';
            SET @DetResultado = 'MFA requerido tras autenticacion externa';

            SELECT @ReqCambioPwd = ReqCambioPwd, @EsSistema = EsSistema
            FROM dbo.Usuarios WHERE Id = @IdUsuario;

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

            INSERT INTO dbo.IntentosAcceso (IdUsuario, IdTenant, IdApp, IdResultado, DetResultado, Exitoso, NomUsuarioIntentado, MetodoAutenticacion)
            VALUES (@IdUsuario, @IdTenant, @IdApp, @ID_MFARequerido, 'MFA requerido tras OAuth', 0, ISNULL(@EmailExterno, @SubExterno),
                    ISNULL((SELECT Codigo FROM dbo.ProvIden WHERE Id = @IdProvIden), 'OAuth'));

            RETURN;
        END

        SET @IdResultado = @ID_Exitoso;
        SET @Mensaje = 'Login exitoso';
        SET @DetResultado = 'Autenticacion externa exitosa';

        GOTO Finalizar;

    END TRY
    BEGIN CATCH
        SET @IdResultado = @ID_ErrorSistema;
        SET @Mensaje = ERROR_MESSAGE();
        SET @DetResultado = 'Error en SP_Auth_LoginExterno: ' + ERROR_MESSAGE();
    END CATCH

    Finalizar:

    INSERT INTO dbo.IntentosAcceso
        (IdUsuario, IdTenant, IdApp, IdResultado, DetResultado, Exitoso, NomUsuarioIntentado,
         IdDisp, IdAgente, IdIP, MetodoAutenticacion)
    VALUES
        (@IdUsuario, @IdTenant, @IdApp, @IdResultado, @DetResultado,
         CASE WHEN @IdResultado IN (@ID_Exitoso, @ID_OAuthLogin, @ID_OAuthLinked, @ID_Provisioning) THEN 1 ELSE 0 END,
         ISNULL(@EmailExterno, @SubExterno), @IdDisp, @IdAgente, NULL,
         ISNULL((SELECT Codigo FROM dbo.ProvIden WHERE Id = @IdProvIden), 'OAuth'));

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

-- ============================================================================
-- 7. Update SP_Pwd_Cambiar to set TienePasswordLocal=1
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.SP_Pwd_Cambiar
    @IdUsuario int,
    @IdTenant int,
    @HashPwdNuevo nvarchar(512),
    @PepperVersion tinyint,
    @IdTipoCambio int,
    @IdDisp int = NULL,
    @IdIP int = NULL,
    @IdAgente int = NULL
AS
BEGIN
    SET NOCOUNT, XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRAN;

        DECLARE @PwdRecordadas tinyint = 5, @IdPolitica int;

        SELECT TOP 1 @PwdRecordadas = pp.PwdRecordadas, @IdPolitica = pp.Id
        FROM dbo.PoliticasPwd pp
        WHERE (pp.IdTenant = @IdTenant OR pp.IdTenant IS NULL)
          AND pp.Activa = 1
        ORDER BY pp.IdTenant DESC;

        IF @IdPolitica IS NULL
        BEGIN
            ROLLBACK;
            SELECT 0 AS Exito, 'No hay politica activa para el tenant' AS Mensaje;
            RETURN;
        END

        IF EXISTS (
            SELECT 1 FROM (
                SELECT TOP (@PwdRecordadas) HashPwd 
                FROM dbo.HistorialPwd 
                WHERE IdUsuario = @IdUsuario 
                ORDER BY FecRegistro DESC
            ) h WHERE h.HashPwd = @HashPwdNuevo
        )
        BEGIN
            ROLLBACK;
            SELECT 0 AS Exito, 'La contrasena fue utilizada recientemente' AS Mensaje;
            RETURN;
        END

        UPDATE dbo.HistorialPwd SET EsActual = 0 WHERE IdUsuario = @IdUsuario AND EsActual = 1;

        INSERT INTO dbo.HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, FecRegistro)
        VALUES (@IdUsuario, @IdPolitica, @IdTipoCambio, @HashPwdNuevo, 'Argon2id', @PepperVersion, 1, SYSUTCDATETIME());

        UPDATE dbo.Usuarios SET ReqCambioPwd = 0, FecUltCambioPwd = SYSUTCDATETIME(), IntentosFallidos = 0, TienePasswordLocal = 1 WHERE Id = @IdUsuario;

        COMMIT;
        SELECT 1 AS Exito, 'Contrasena actualizada' AS Mensaje;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        SELECT 0 AS Exito, @ErrMsg AS Mensaje;
    END CATCH
END;
GO

-- ============================================================================
-- 8. Update SP_Auth_Login: add MetodoAutenticacion='Local' to INSERT statements
--    ONLY modify the two INSERT INTO IntentosAcceso statements
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.SP_Auth_Login
    @IdTenant          int,
    @IdApp             int,
    @HashPwdCalculado  nvarchar(512),
    @NomUsuario        nvarchar(100)  = NULL,
    @Email             nvarchar(255)  = NULL,
    @IdDisp            int            = NULL,
    @IdIP              int            = NULL,
    @IdAgente          int            = NULL
AS
BEGIN
    SET NOCOUNT, XACT_ABORT ON;

    IF @NomUsuario IS NULL AND @Email IS NULL
    BEGIN
        RAISERROR('Debe especificar @NomUsuario o @Email.', 16, 1);
        RETURN;
    END

    DECLARE @FecInicio     datetime2(3)  = SYSUTCDATETIME();
    DECLARE @NomUsuarioLog nvarchar(100) = COALESCE(@NomUsuario, @Email, '');

    DECLARE @IdUsuario int, @IdEstado int, @EsEliminado bit, @EsSistema bit;
    DECLARE @IntentosActuales tinyint, @PepperVersionActual tinyint;
    DECLARE @MaxIntentos tinyint, @DurBloqueo int;
    DECLARE @HashPwdDB nvarchar(512), @PepperVersionDB tinyint, @FecExpiraPwd datetime2(3);

    DECLARE @Resultado        int,
            @Mensaje          nvarchar(200),
            @DetResultado     nvarchar(200) = NULL,
            @Exitoso          bit           = 0,
            @ReqCambioPwd     bit           = NULL,
            @PwdExpirada      bit           = NULL,
            @RequiereReHash   bit           = NULL,
            @IdMFAPrincipal   int           = NULL,
            @IdBloqueo        int           = NULL,
            @FecFinBloqueo    datetime2(3)  = NULL,
            @IntentosRestantes int          = NULL;

    DECLARE @ID_Exitoso int, @ID_CredInvalidas int, @ID_CuentaBloqueada int,
            @ID_SinAccesoApp int, @ID_CuentaInactiva int, @ID_ErrorSistema int;

    SELECT
        @ID_Exitoso         = MAX(CASE WHEN Nombre = 'Exitoso'               THEN Id END),
        @ID_CredInvalidas   = MAX(CASE WHEN Nombre = 'CredencialesInvalidas' THEN Id END),
        @ID_CuentaBloqueada = MAX(CASE WHEN Nombre = 'CuentaBloqueada'       THEN Id END),
        @ID_SinAccesoApp    = MAX(CASE WHEN Nombre = 'SinAccesoApp'         THEN Id END),
        @ID_CuentaInactiva  = MAX(CASE WHEN Nombre = 'CuentaInactiva'       THEN Id END),
        @ID_ErrorSistema    = MAX(CASE WHEN Nombre = 'ErrorSistema'         THEN Id END)
    FROM dbo.ResultadosAcceso;

    BEGIN TRY

        SELECT TOP 1
            @IdUsuario           = u.Id,
            @IdEstado            = u.IdEstado,
            @EsEliminado         = u.Eliminado,
            @EsSistema           = u.EsSistema,
            @IntentosActuales    = u.IntentosFallidos,
            @PepperVersionActual = ct.PepperVersionActual
        FROM dbo.Usuarios u
        JOIN dbo.Tenants t        ON u.IdTenant = t.Id
        JOIN dbo.ConfigTenants ct ON u.IdTenant = ct.IdTenant
        WHERE u.IdTenant = @IdTenant
          AND u.Eliminado = 0
          AND (u.NomUsuario = @NomUsuario OR u.Email = @Email)
          AND t.Activo = 1;

        IF @IdUsuario IS NULL
        BEGIN
            DECLARE @dummy nvarchar(512) = @HashPwdCalculado;
            SET @Resultado = @ID_CredInvalidas; SET @Mensaje = 'Credenciales invalidas';
            SET @DetResultado = 'Usuario no encontrado';
            GOTO Finalizar;
        END

        IF @IdEstado <> 1
        BEGIN
            SET @Resultado = @ID_CuentaInactiva; SET @Mensaje = 'Cuenta inactiva';
            SET @DetResultado = 'Cuenta inactiva';
            GOTO Finalizar;
        END

        IF EXISTS (
            SELECT 1 FROM dbo.Bloqueos b
            JOIN dbo.TiposBloqueo tb ON b.IdTipoBloqueo = tb.Id
            WHERE b.IdUsuario = @IdUsuario AND b.IdTenant = @IdTenant
              AND b.Activo = 1 AND (tb.EsTemporal = 0 OR b.FecFin > SYSUTCDATETIME())
        )
        BEGIN
            SET @Resultado = @ID_CuentaBloqueada; SET @Mensaje = 'Cuenta bloqueada';
            SET @DetResultado = 'Cuenta bloqueada'; SET @Exitoso = 0;
            GOTO Finalizar;
        END

        IF @EsSistema = 0
           AND NOT EXISTS (SELECT 1 FROM dbo.Accesos a WHERE a.IdUsuario = @IdUsuario AND a.IdApp = @IdApp AND a.Activo = 1)
        BEGIN
            SET @Resultado = @ID_SinAccesoApp; SET @Mensaje = 'Sin acceso a la aplicacion';
            SET @DetResultado = 'Sin acceso a la aplicacion';
            GOTO Finalizar;
        END

        ;WITH PoliticaCandidata AS (
            SELECT pp.MaxIntentos, pp.DurBloqueoMin, 1 AS Prioridad
            FROM dbo.Accesos a
            JOIN dbo.RolesPoliticasPwd rp ON rp.IdTenant = a.IdTenant AND rp.IdRol = a.IdRol AND rp.Activo = 1
            JOIN dbo.PoliticasPwd pp ON pp.Id = rp.IdPolitica AND pp.Activa = 1
            WHERE a.IdUsuario = @IdUsuario AND a.IdApp = @IdApp AND a.Activo = 1

            UNION ALL
            SELECT pp.MaxIntentos, pp.DurBloqueoMin, 2
            FROM dbo.PoliticasPwd pp
            WHERE pp.IdTenant = @IdTenant AND pp.IdApp = @IdApp AND pp.Activa = 1

            UNION ALL
            SELECT pp.MaxIntentos, pp.DurBloqueoMin, 3
            FROM dbo.PoliticasPwd pp
            WHERE pp.IdTenant = @IdTenant AND pp.IdApp IS NULL AND pp.Activa = 1

            UNION ALL
            SELECT pp.MaxIntentos, pp.DurBloqueoMin, 4
            FROM dbo.PoliticasPwd pp
            WHERE pp.IdTenant IS NULL AND pp.IdApp IS NULL AND pp.Activa = 1
        )
        SELECT TOP 1 @MaxIntentos = MaxIntentos, @DurBloqueo = DurBloqueoMin
        FROM PoliticaCandidata
        ORDER BY Prioridad ASC, MaxIntentos ASC;

        SET @MaxIntentos = ISNULL(@MaxIntentos, 5);
        SET @DurBloqueo  = ISNULL(@DurBloqueo, 30);

        SELECT @HashPwdDB = h.HashPwd, @PepperVersionDB = h.PepperVersion, @FecExpiraPwd = h.FecExpira
        FROM dbo.HistorialPwd h
        WHERE h.IdUsuario = @IdUsuario AND h.EsActual = 1;

        IF @HashPwdDB IS NULL
        BEGIN
            SET @Resultado = @ID_CuentaInactiva; SET @Mensaje = 'Usuario sin contrasena configurada';
            SET @DetResultado = 'Usuario sin contrasena configurada';
            GOTO Finalizar;
        END

        IF @HashPwdDB COLLATE Latin1_General_BIN2 = @HashPwdCalculado COLLATE Latin1_General_BIN2
        BEGIN
            UPDATE dbo.Usuarios
            SET IntentosFallidos = 0, FecUltIntentoFallido = NULL
            WHERE Id = @IdUsuario;

            SELECT @ReqCambioPwd = ReqCambioPwd FROM dbo.Usuarios WHERE Id = @IdUsuario;
            SET @PwdExpirada    = CASE WHEN @FecExpiraPwd IS NOT NULL AND @FecExpiraPwd <= SYSUTCDATETIME() THEN 1 ELSE 0 END;
            SET @RequiereReHash = CASE WHEN @PepperVersionDB < @PepperVersionActual THEN 1 ELSE 0 END;
            SELECT @IdMFAPrincipal = Id FROM dbo.MFA WHERE IdUsuario = @IdUsuario AND EsPrincipal = 1 AND IdEstado = (SELECT Id FROM dbo.EstadosMFA WHERE Codigo = 'ACTIVO');

            SET @Resultado = @ID_Exitoso; SET @Mensaje = NULL; SET @Exitoso = 1;
            GOTO Finalizar;
        END
        ELSE
        BEGIN
            DECLARE @TablaIntentos TABLE (IntentosFallidos tinyint);

            UPDATE dbo.Usuarios
            SET IntentosFallidos = IntentosFallidos + 1, FecUltIntentoFallido = SYSUTCDATETIME()
            OUTPUT inserted.IntentosFallidos INTO @TablaIntentos
            WHERE Id = @IdUsuario;

            SELECT @IntentosActuales = IntentosFallidos FROM @TablaIntentos;

            IF @IntentosActuales >= @MaxIntentos
            BEGIN
                DECLARE @TablaBloqueo TABLE (Id int, FecFin datetime2(3));

                INSERT INTO dbo.Bloqueos (IdUsuario, IdTenant, IdTipoBloqueo, IdAgente, IdIP, Motivo, FecInicio, FecFin, Activo)
                OUTPUT inserted.Id, inserted.FecFin INTO @TablaBloqueo
                VALUES (@IdUsuario, @IdTenant, 1, @IdAgente, @IdIP, 'Intentos fallidos superados', SYSUTCDATETIME(), DATEADD(MINUTE, @DurBloqueo, SYSUTCDATETIME()), 1);

                SELECT @IdBloqueo = Id, @FecFinBloqueo = FecFin FROM @TablaBloqueo;

                SET @Resultado = @ID_CuentaBloqueada; SET @Mensaje = 'Cuenta bloqueada por intentos fallidos';
                SET @DetResultado = 'Cuenta bloqueada por intentos fallidos';
            END
            ELSE
            BEGIN
                SET @IntentosRestantes = @MaxIntentos - @IntentosActuales;
                SET @Resultado = @ID_CredInvalidas; SET @Mensaje = 'Credenciales invalidas';
                SET @DetResultado = 'Credenciales invalidas';
            END

            GOTO Finalizar;
        END

    END TRY
    BEGIN CATCH
        INSERT INTO dbo.IntentosAcceso
            (IdUsuario, IdTenant, IdApp, IdResultado, IdDisp, IdAgente, IdIP,
             DetResultado, NomUsuarioIntentado, Exitoso, TpoRespuesta, CodRespuesta, MetodoAutenticacion)
        VALUES
            (@IdUsuario, @IdTenant, @IdApp, @ID_ErrorSistema, @IdDisp, @IdAgente, @IdIP,
             ERROR_MESSAGE(), @NomUsuarioLog, 0, DATEDIFF(MILLISECOND, @FecInicio, SYSUTCDATETIME()), ERROR_NUMBER(), 'Local');

        THROW;
    END CATCH

    Finalizar:

    INSERT INTO dbo.IntentosAcceso
        (IdUsuario, IdTenant, IdApp, IdResultado, IdDisp, IdAgente, IdIP,
         DetResultado, NomUsuarioIntentado, Exitoso, TpoRespuesta, CodRespuesta, MetodoAutenticacion)
    VALUES
        (@IdUsuario, @IdTenant, @IdApp, @Resultado, @IdDisp, @IdAgente, @IdIP,
         @DetResultado, @NomUsuarioLog, @Exitoso, DATEDIFF(MILLISECOND, @FecInicio, SYSUTCDATETIME()), NULL, 'Local');

    SELECT
        @Resultado          AS Resultado,
        @Mensaje             AS Mensaje,
        @IdUsuario           AS IdUsuario,
        @IdTenant            AS IdTenant,
        @EsSistema           AS EsSistema,
        @ReqCambioPwd        AS ReqCambioPwd,
        @PwdExpirada         AS PwdExpirada,
        @RequiereReHash      AS RequiereReHash,
        @IdMFAPrincipal      AS IdMFAPrincipal,
        @IdBloqueo           AS IdBloqueo,
        @FecFinBloqueo       AS FecFinBloqueo,
        @IntentosRestantes   AS IntentosRestantes;
END;
GO

PRINT N'FASE 15 complete: TienePasswordLocal, MetodoAutenticacion, RequiereMFALocal added. SPs updated.';
GO
