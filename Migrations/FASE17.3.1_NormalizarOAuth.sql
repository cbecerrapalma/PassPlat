-- ============================================================
-- FASE:        17.3.1
-- Versión:     1.0
-- Fecha:       2026-07-19
-- Requiere:    FASE 17.2 completada
-- Reversible:  No (los proveedores eliminados deben restaurarse
--              manualmente desde backup si se requiere rollback)
--
-- Normalizar el catálogo ProvIden a solo 4 proveedores aprobados
-- y limpiar configuraciones heredadas (test providers, GitHub,
-- Microsoft, Apple).
--
-- Referencia ETipoProveedor (enum C#):
--   OAuth2        = 1  → FACEBOOK, INSTAGRAM, LINKEDIN
--   OpenIDConnect = 2  → GOOGLE
--   MicrosoftEntra = 3 (no usado)
--   LDAP          = 4 (no usado)
--   SAML          = 5 (no usado)
--
-- Idempotente: puede ejecutarse múltiples veces sin duplicar
-- datos ni dañar el estado esperado.
-- ============================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    -- ═══════════════════════════════════════════════════════════
    -- 1. VALIDACIONES PREVIAS
    -- ═══════════════════════════════════════════════════════════

    -- 1a. Verificar que no existan referencias en tablas dependientes
    --     a proveedores que serán eliminados
    DECLARE @provBlocker NVARCHAR(100);

    -- IdenExt
    SELECT TOP 1 @provBlocker = P.Codigo
    FROM IdenExt IE
    JOIN ProvIden P ON P.Id = IE.IdProvIden
    WHERE P.Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    IF @provBlocker IS NOT NULL
        THROW 50001, N'Proveedor bloqueante posee identidades externas. Eliminar primero los registros asociados.', 1;

    -- IdenExtTokens (indirecto via IdenExt)
    SELECT TOP 1 @provBlocker = P.Codigo
    FROM IdenExtTokens IET
    JOIN IdenExt IE ON IE.Id = IET.IdIdenExt
    JOIN ProvIden P ON P.Id = IE.IdProvIden
    WHERE P.Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    IF @provBlocker IS NOT NULL
        THROW 50001, N'Proveedor bloqueante posee tokens activos. Eliminar primero los registros asociados.', 1;

    -- AudIdenExt
    SELECT TOP 1 @provBlocker = P.Codigo
    FROM AudIdenExt A
    JOIN ProvIden P ON P.Id = A.IdProvIden
    WHERE P.Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    IF @provBlocker IS NOT NULL
        THROW 50001, N'Proveedor bloqueante posee auditorías. Eliminar primero los registros asociados.', 1;

    -- HistorialIdenExt
    SELECT TOP 1 @provBlocker = P.Codigo
    FROM HistorialIdenExt H
    JOIN ProvIden P ON P.Id = H.IdProvIden
    WHERE P.Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    IF @provBlocker IS NOT NULL
        THROW 50001, N'Proveedor bloqueante posee historial. Eliminar primero los registros asociados.', 1;

    -- ═══════════════════════════════════════════════════════════
    -- 2. LIMPIAR CONFIGURACIONES DE PROVEEDORES A ELIMINAR
    -- ═══════════════════════════════════════════════════════════
    DELETE C
    FROM ConfProvIden C
    JOIN ProvIden P ON P.Id = C.IdProvIden
    WHERE P.Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    PRINT 'ConfProvIden limpiada para proveedores no aprobados.';

    -- ═══════════════════════════════════════════════════════════
    -- 3. ELIMINAR PROVEEDORES NO APROBADOS
    -- ═══════════════════════════════════════════════════════════
    DELETE FROM ProvIden
    WHERE Codigo NOT IN ('GOOGLE','FACEBOOK','INSTAGRAM','LINKEDIN');

    PRINT 'ProvIden limpiada: solo quedan GOOGLE, FACEBOOK, INSTAGRAM, LINKEDIN.';

    -- ═══════════════════════════════════════════════════════════
    -- 4. MERGE (INSERT/UPDATE) — CATÁLOGO APROBADO
    -- ═══════════════════════════════════════════════════════════
    -- ETipoProveedor: OAuth2=1, OpenIDConnect=2, MicrosoftEntra=3, LDAP=4, SAML=5
    -- GOOGLE usa 2 (OpenIDConnect — compatible con OIDC)
    -- FACEBOOK, INSTAGRAM, LINKEDIN usan 1 (OAuth2)
    MERGE ProvIden AS T
    USING (
        SELECT *
        FROM (VALUES
            (N'FACEBOOK',  N'Facebook',  CAST(1 AS TINYINT), N'OAuth',
             NULL,  N'facebook',  CAST(7 AS SMALLINT),  CAST(1 AS BIT),
             N'https://www.facebook.com/v18.0/dialog/oauth',
             N'https://graph.facebook.com/v18.0/oauth/access_token',
             N'https://graph.facebook.com/v18.0/me',
             NULL,  CAST(1 AS BIT), CAST(0 AS BIT), CAST(0 AS BIT)),

            (N'GOOGLE',    N'Google',    CAST(2 AS TINYINT), N'OAuth',
             N'https://accounts.google.com',  N'google',  CAST(1 AS SMALLINT),  CAST(1 AS BIT),
             N'https://accounts.google.com/o/oauth2/v2/auth',
             N'https://oauth2.googleapis.com/token',
             N'https://openidconnect.googleapis.com/v1/userinfo',
             N'https://oauth2.googleapis.com/revoke',
             CAST(1 AS BIT), CAST(1 AS BIT), CAST(1 AS BIT)),

            (N'INSTAGRAM', N'Instagram', CAST(1 AS TINYINT), N'OAuth',
             NULL,  N'camera_alt',  CAST(6 AS SMALLINT),  CAST(1 AS BIT),
             N'https://api.instagram.com/oauth/authorize',
             N'https://api.instagram.com/oauth/access_token',
             N'https://graph.instagram.com/me',
             NULL,  CAST(1 AS BIT), CAST(0 AS BIT), CAST(0 AS BIT)),

            (N'LINKEDIN',  N'LinkedIn',  CAST(1 AS TINYINT), N'OAuth',
             NULL,  N'linkedin',  CAST(5 AS SMALLINT),  CAST(1 AS BIT),
             N'https://www.linkedin.com/oauth/v2/authorization',
             N'https://www.linkedin.com/oauth/v2/accessToken',
             N'https://api.linkedin.com/v2/userinfo',
             NULL,  CAST(1 AS BIT), CAST(1 AS BIT), CAST(0 AS BIT))
        ) AS Src (Codigo, Nombre, TipoProveedor, Protocolo,
                  UrlIssuer, Icono, Orden, Activo,
                  EndpointAutorizacion, EndpointToken,
                  EndpointUserInfo, EndpointRevocacion,
                  SoportaPKCE, SoportaRefreshToken, SoportaMFA)
    ) AS S
    ON T.Codigo = S.Codigo
    WHEN MATCHED THEN UPDATE SET
        Nombre               = S.Nombre,
        TipoProveedor        = S.TipoProveedor,
        Protocolo            = S.Protocolo,
        UrlIssuer            = S.UrlIssuer,
        EndpointAutorizacion = S.EndpointAutorizacion,
        EndpointToken        = S.EndpointToken,
        EndpointUserInfo     = S.EndpointUserInfo,
        EndpointRevocacion   = S.EndpointRevocacion,
        SoportaPKCE          = S.SoportaPKCE,
        SoportaRefreshToken  = S.SoportaRefreshToken,
        SoportaMFA           = S.SoportaMFA,
        Icono                = S.Icono,
        Orden                = S.Orden,
        -- Preservar Activo existente (no sobreescribir con el source)
        FecMod = SYSDATETIME()
    WHEN NOT MATCHED THEN INSERT (
        Codigo, Nombre, TipoProveedor, Protocolo,
        UrlIssuer, Icono, Orden, Activo,
        EndpointAutorizacion, EndpointToken,
        EndpointUserInfo, EndpointRevocacion,
        SoportaPKCE, SoportaRefreshToken, SoportaMFA,
        FecCrea
    ) VALUES (
        S.Codigo, S.Nombre, S.TipoProveedor, S.Protocolo,
        S.UrlIssuer, S.Icono, S.Orden, 1,
        S.EndpointAutorizacion, S.EndpointToken,
        S.EndpointUserInfo, S.EndpointRevocacion,
        S.SoportaPKCE, S.SoportaRefreshToken, S.SoportaMFA,
        SYSDATETIME()
    );

    PRINT 'MERGE ProvIden completado (INSERT/UPDATE).';

    -- ═══════════════════════════════════════════════════════════
    -- 5. CONFIGURACIÓN POR PROVEEDOR (ConfProvIden)
    -- ═══════════════════════════════════════════════════════════

    -- 5a. GOOGLE: asegurar que existe un registro activo
    --     (preservar credenciales existentes, o crear placeholder)
    IF NOT EXISTS (
        SELECT 1 FROM ConfProvIden C
        JOIN ProvIden P ON P.Id = C.IdProvIden
        WHERE P.Codigo = 'GOOGLE'
    )
    BEGIN
        INSERT INTO ConfProvIden (
            IdTenant, IdProvIden, ClientId, ClientSecret, Scopes,
            Callback, GuardarTokens, PermitirAutoLink, AutoProvisionar,
            Activo, FecCrea, Estado, RequiereMFALocal,
            PermitirLogin, PermitirCrearUsuario, PermitirVincular,
            PermitirDesvincular, PermitirPasswordLocal, ObligaMFA,
            PermitirCambioEmail, PermitirCambioNombre,
            PermitirSincronizarAvatar, PermitirSincronizarPerfil,
            Prioridad, OrdenVisual, Logo, Color, Tooltip,
            FrecuenciaSincronizacion, ResponseType, GrantType
        )
        SELECT
            1, P.Id, '', '', '',
            '', 0, 0, 0,
            1, SYSDATETIME(), 1, 0,
            1, 0, 1,
            1, 1, 0,
            1, 1,
            1, 0,
            0, 1, 'google', 'Error', 'Continuar con Google',
            'Siempre', 'code', 'authorization_code'
        FROM ProvIden P
        WHERE P.Codigo = 'GOOGLE';

        PRINT 'ConfProvIden GOOGLE creado (placeholder).';
    END
    ELSE
    BEGIN
        PRINT 'ConfProvIden GOOGLE ya existe — preservado.';
    END;

    -- 5b. No-GOOGLE: desactivar y limpiar credenciales
    -- NOTA: CK_ConfProvIden_ClientSecret exige len>0, no puede ser ''
    UPDATE C
    SET
        Activo     = 0,
        ClientId   = '',
        ClientSecret = '*',
        Callback   = '',
        RedirectUri = NULL,
        FecMod     = SYSDATETIME()
    FROM ConfProvIden C
    JOIN ProvIden P ON P.Id = C.IdProvIden
    WHERE P.Codigo <> 'GOOGLE'
      AND (C.Activo = 1
           OR C.ClientId <> ''
           OR C.ClientSecret <> ''
           OR C.Callback <> ''
           OR C.RedirectUri IS NOT NULL);

    PRINT 'ConfProvIden no-GOOGLE desactivadas y limpiadas.';

    -- ═══════════════════════════════════════════════════════════
    -- 6. VERIFICACIONES
    -- ═══════════════════════════════════════════════════════════
    PRINT '';
    PRINT '═══════════════════════════════════════════════════════';
    PRINT 'VERIFICACIONES POST-MIGRACIÓN';
    PRINT '═══════════════════════════════════════════════════════';

    -- 6a. Solo 4 proveedores
    DECLARE @provCount INT;
    SELECT @provCount = COUNT(*) FROM ProvIden;
    PRINT 'Proveedores en catálogo: ' + CAST(@provCount AS NVARCHAR(10));

    -- 6b. Listado
    PRINT 'Listado:';
    SELECT Codigo, Nombre, Protocolo, Activo FROM ProvIden ORDER BY Codigo;

    -- 6c. Sin duplicados
    IF EXISTS (SELECT Codigo, COUNT(*) FROM ProvIden GROUP BY Codigo HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'ERROR: Existen códigos duplicados en ProvIden:';
        SELECT Codigo, COUNT(*) AS Duplicados FROM ProvIden GROUP BY Codigo HAVING COUNT(*) > 1;
        THROW 50002, 'Catálogo ProvIden contiene duplicados. Revisar datos.', 1;
    END
    ELSE
        PRINT 'No hay códigos duplicados.';

    -- 6d. Protocolo único
    SELECT DISTINCT Protocolo FROM ProvIden;

    -- 6e. Google activo con configuración
    PRINT 'Configuración GOOGLE:';
    SELECT C.*, P.Codigo
    FROM ConfProvIden C
    JOIN ProvIden P ON P.Id = C.IdProvIden
    WHERE P.Codigo = 'GOOGLE';

    -- 6f. No-GOOGLE: ninguna config activa
    IF EXISTS (
        SELECT 1 FROM ConfProvIden C
        JOIN ProvIden P ON P.Id = C.IdProvIden
        WHERE P.Codigo <> 'GOOGLE' AND C.Activo = 1
    )
    BEGIN
        PRINT 'ERROR: Existen configuraciones activas para proveedores no-GOOGLE:';
        SELECT P.Codigo, C.*
        FROM ConfProvIden C
        JOIN ProvIden P ON P.Id = C.IdProvIden
        WHERE P.Codigo <> 'GOOGLE' AND C.Activo = 1;
        THROW 50003, 'Configuraciones activas no-GOOGLE encontradas. Revisar.', 1;
    END
    ELSE
        PRINT 'No hay configuraciones activas para proveedores no-GOOGLE.';

    PRINT '═══════════════════════════════════════════════════════';
    PRINT 'FASE 17.3.1 completada exitosamente.';
    PRINT '═══════════════════════════════════════════════════════';

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    DECLARE @ErrorLine INT = ERROR_LINE();

    PRINT '═══════════════════════════════════════════════════════';
    PRINT 'ERROR en FASE 17.3.1 (Línea ' + CAST(@ErrorLine AS NVARCHAR(10)) + '):';
    PRINT @ErrorMessage;
    PRINT 'Transacción revertida.';
    PRINT '═══════════════════════════════════════════════════════';

    THROW;
END CATCH;
GO
