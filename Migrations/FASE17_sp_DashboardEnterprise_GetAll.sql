-- ============================================
-- FASE 17: sp_DashboardEnterprise_GetAll
-- Returns 10 result sets for the Enterprise Dashboard
-- ============================================
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_DashboardEnterprise_GetAll')
    DROP PROCEDURE sp_DashboardEnterprise_GetAll
GO

CREATE PROCEDURE sp_DashboardEnterprise_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Resumen Ejecutivo
    SELECT
        (SELECT COUNT(*) FROM Usuarios WHERE Eliminado = 0) AS TotalUsuarios,
        (SELECT COUNT(*) FROM Usuarios WHERE IdEstado = 1 AND Eliminado = 0) AS UsuariosActivos,
        (SELECT COUNT(*) FROM Usuarios WHERE IdEstado = 3) AS UsuariosBloqueados,
        (SELECT COUNT(*) FROM Usuarios WHERE Eliminado = 1) AS UsuariosEliminados,
        (SELECT COUNT(*) FROM Tenants WHERE Activo = 1) AS TotalTenants,
        (SELECT COUNT(*) FROM Apps) AS TotalApps,
        (SELECT COUNT(*) FROM IdenExt WHERE Eliminado = 0) AS IdentidadesExternas,
        (SELECT COUNT(*) FROM EmailLog WHERE Estado = 'pendiente') AS ColaEmailPendiente,
        (SELECT COUNT(*) FROM EmailLog WHERE FecCrea >= CAST(GETDATE() AS DATE) AND Estado IN ('Enviado','Sent')) AS EmailsEnviadosHoy,
        (SELECT COUNT(*) FROM EmailLog WHERE Estado = 'Error') AS EmailsFallidos;

    -- 2. Seguridad
    SELECT
        (SELECT COUNT(*) FROM IntentosAcceso WHERE Exitoso = 1) AS LoginsCorrectos,
        (SELECT COUNT(*) FROM IntentosAcceso WHERE Exitoso = 0) AS LoginsFallidos,
        (SELECT COUNT(*) FROM Bloqueos WHERE Activo = 1) AS BloqueosActivos,
        (SELECT COUNT(*) FROM MFA WHERE EsPrincipal = 1 AND IdEstado = 1) AS MFAHabilitado,
        (SELECT COUNT(*) FROM MFA WHERE IdEstado = 3) AS MFAPendiente,
        (SELECT COUNT(*) FROM HistorialPwd WHERE EsActual = 1 AND FecExpira < GETDATE()) AS PasswordsExpiradas,
        (SELECT COUNT(*) FROM HistorialPwd WHERE EsActual = 1 AND FecExpira >= GETDATE() AND FecExpira <= DATEADD(DAY, 7, GETDATE())) AS PasswordsProximasVencer,
        (SELECT COUNT(*) FROM Bloqueos WHERE Activo = 1 AND FecInicio >= DATEADD(HOUR, -24, GETDATE())) AS AlertasSeguridad,
        (SELECT COUNT(*) FROM IPs WHERE EsSospechosa = 1) AS IPsSospechosas,
        (SELECT COUNT(*) FROM DispConfiables WHERE FecAlta >= DATEADD(HOUR, -24, GETDATE())) AS NuevosDispositivos24h,
        (SELECT COUNT(*) FROM IdenExt WHERE Eliminado = 0 AND FecCrea >= DATEADD(HOUR, -24, GETDATE())) AS NuevosProveedoresOAuth24h,
        (SELECT COUNT(*) FROM Usuarios WHERE Eliminado = 0 AND (Email IS NULL OR Email = '')) AS UsuariosSinEmail;

    -- 3. OAuth / Federación
    SELECT
        (SELECT COUNT(*) FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id WHERE p.Codigo = 'google' AND ie.Eliminado = 0) AS UsuariosGoogle,
        (SELECT COUNT(*) FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id WHERE p.Codigo = 'github' AND ie.Eliminado = 0) AS UsuariosGithub,
        (SELECT COUNT(*) FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id WHERE p.Codigo = 'linkedin' AND ie.Eliminado = 0) AS UsuariosLinkedIn,
        (SELECT COUNT(*) FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id WHERE p.Codigo = 'facebook' AND ie.Eliminado = 0) AS UsuariosFacebook,
        (SELECT COUNT(*) FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id WHERE p.Codigo = 'instagram' AND ie.Eliminado = 0) AS UsuariosInstagram,
        (SELECT COUNT(*) FROM IdenExt WHERE Eliminado = 0 AND Activo = 1) AS ConsentimientosActivos,
        (SELECT COUNT(*) FROM IdenExt WHERE Eliminado = 0 AND Activo = 0) AS ConsentimientosRevocados,
        (SELECT COUNT(*) FROM AudIdenExt WHERE Resultado IN ('Error','ProviderDisabled','Timeout','Unavailable')) AS ErroresOAuth,
        (SELECT TOP 1 Nombre FROM ProvIden ORDER BY (SELECT COUNT(*) FROM IdenExt WHERE IdProvIden = ProvIden.Id AND Eliminado = 0) DESC) AS ProveedorMasUtilizado,
        (SELECT COUNT(*) FROM IdenExt WHERE Eliminado = 0) AS UsuariosVinculados;

    -- 4. Email
    SELECT
        (SELECT COUNT(*) FROM EmailLog WHERE Estado IN ('Enviado','Sent')) AS EmailsEnviados,
        (SELECT COUNT(*) FROM EmailLog WHERE Estado IN ('pendiente','Pending')) AS EmailsPendientes,
        (SELECT COUNT(*) FROM EmailLog WHERE Estado = 'Error') AS EmailsError,
        (SELECT COUNT(*) FROM EmailLog WHERE FecCrea >= CAST(GETDATE() AS DATE)) AS EmailsHoy,
        (SELECT COUNT(*) FROM EmailLog WHERE FecCrea >= DATEADD(DAY, -7, GETDATE())) AS EmailsSemana,
        (SELECT COUNT(*) FROM EmailLog WHERE FecCrea >= DATEADD(DAY, -30, GETDATE())) AS EmailsMes,
        0 AS TiempoPromedioEnvioMs;

    -- 5. Operacional
    SELECT
        ISNULL((SELECT AVG(CAST(TpoRespuesta AS FLOAT)) FROM IntentosAcceso WHERE TpoRespuesta > 0), 0) AS TiempoRespuestaLoginMs;

    -- 6. Auditoría
    SELECT
        (SELECT COUNT(*) FROM AuditoriaPwd WHERE FecAccion >= CAST(GETDATE() AS DATE))
            + (SELECT COUNT(*) FROM AudIdenExt WHERE FecEvento >= CAST(GETDATE() AS DATE)) AS EventosHoy,
        (SELECT COUNT(*) FROM AuditoriaPwd WHERE FecAccion >= DATEADD(DAY, -7, GETDATE()))
            + (SELECT COUNT(*) FROM AudIdenExt WHERE FecEvento >= DATEADD(DAY, -7, GETDATE())) AS EventosSemana,
        (SELECT COUNT(*) FROM AuditoriaPwd WHERE FecAccion >= DATEADD(DAY, -30, GETDATE()))
            + (SELECT COUNT(*) FROM AudIdenExt WHERE FecEvento >= DATEADD(DAY, -30, GETDATE())) AS EventosMes,
        (SELECT COUNT(DISTINCT IdUsuario) FROM AuditoriaPwd) AS UsuariosAuditados,
        (SELECT COUNT(*) FROM AuditoriaPwd WHERE IdTipoAccion IN (3,4)) AS CambiosPassword;

    -- 7. Dispositivos
    SELECT
        (SELECT COUNT(*) FROM DispConfiables WHERE Confiable = 1) AS DispositivosActivos,
        (SELECT COUNT(*) FROM DispConfiables WHERE Confiable = 0) AS DispositivosBloqueados,
        (SELECT COUNT(*) FROM DispConfiables WHERE FecAlta >= DATEADD(HOUR, -24, GETDATE())) AS DispositivosNuevos24h,
        (SELECT COUNT(*) FROM IPs) AS TotalIPs;

    -- 8. Tendencias (últimos 7 días, día por fila)
    SELECT CAST(FecCrea AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM Usuarios WHERE FecCrea >= DATEADD(DAY, -30, GETDATE()) GROUP BY CAST(FecCrea AS DATE) ORDER BY Fecha;
    SELECT CAST(FecIntento AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM IntentosAcceso WHERE FecIntento >= DATEADD(DAY, -30, GETDATE()) AND Exitoso = 1 GROUP BY CAST(FecIntento AS DATE) ORDER BY Fecha;
    SELECT CAST(FecCrea AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM EmailLog WHERE FecCrea >= DATEADD(DAY, -30, GETDATE()) GROUP BY CAST(FecCrea AS DATE) ORDER BY Fecha;
    SELECT CAST(FecEvento AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM AudIdenExt WHERE FecEvento >= DATEADD(DAY, -30, GETDATE()) GROUP BY CAST(FecEvento AS DATE) ORDER BY Fecha;
    SELECT CAST(FecIntento AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM IntentosAcceso WHERE FecIntento >= DATEADD(DAY, -30, GETDATE()) AND Exitoso = 0 GROUP BY CAST(FecIntento AS DATE) ORDER BY Fecha;
    SELECT CAST(FecAlta AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM MFA WHERE FecAlta >= DATEADD(DAY, -30, GETDATE()) GROUP BY CAST(FecAlta AS DATE) ORDER BY Fecha;
    SELECT CAST(FecRegistro AS DATE) AS Fecha, COUNT(*) AS Cantidad FROM HistorialPwd WHERE FecRegistro >= DATEADD(DAY, -30, GETDATE()) GROUP BY CAST(FecRegistro AS DATE) ORDER BY Fecha;

    -- 9. Estado General (10 módulos)
    SELECT 'Usuarios' AS Modulo,
        CASE WHEN (SELECT COUNT(*) FROM Bloqueos WHERE Activo = 1) > 0 THEN 'yellow' ELSE 'green' END AS Estado,
        CASE WHEN (SELECT COUNT(*) FROM Bloqueos WHERE Activo = 1) > 0
            THEN CAST((SELECT COUNT(*) FROM Bloqueos WHERE Activo = 1) AS VARCHAR) + ' bloqueos'
            ELSE 'OK' END AS Mensaje;
    SELECT 'OAuth' AS Modulo, 'green' AS Estado, 'Disponible' AS Mensaje;
    SELECT 'Email' AS Modulo,
        CASE WHEN (SELECT COUNT(*) FROM EmailLog WHERE Estado = 'Error' AND FecCrea >= DATEADD(HOUR, -1, GETDATE())) > 0
            THEN 'red' ELSE 'green' END AS Estado,
        CASE WHEN (SELECT COUNT(*) FROM EmailLog WHERE Estado = 'Error' AND FecCrea >= DATEADD(HOUR, -1, GETDATE())) > 0
            THEN 'Errores recientes' ELSE 'Disponible' END AS Mensaje;
    SELECT 'MFA' AS Modulo,
        CASE WHEN (SELECT COUNT(*) FROM MFA WHERE IdEstado = 3) > 0 THEN 'yellow' ELSE 'green' END AS Estado,
        CASE WHEN (SELECT COUNT(*) FROM MFA WHERE IdEstado = 3) > 0 THEN 'Pendientes' ELSE 'OK' END AS Mensaje;
    SELECT 'Password' AS Modulo, 'green' AS Estado, 'OK' AS Mensaje;
    SELECT 'Auditoria' AS Modulo, 'green' AS Estado, 'Activo' AS Mensaje;
    SELECT 'Background' AS Modulo, 'green' AS Estado, 'OK' AS Mensaje;
    SELECT 'Dashboard' AS Modulo, 'green' AS Estado, 'Activo' AS Mensaje;
    SELECT 'API' AS Modulo, 'green' AS Estado, 'Operativo' AS Mensaje;
    SELECT 'BaseDatos' AS Modulo, 'green' AS Estado, 'Conectado' AS Mensaje;

    -- 10. Ejecutivo Avanzado
    SELECT TOP 10 NomUsuario AS Nombre, 1 AS Cantidad FROM Usuarios WHERE Eliminado = 0 ORDER BY FecMod DESC;
    SELECT TOP 10 p.Nombre AS Nombre, COUNT(ie.Id) AS Cantidad
        FROM IdenExt ie INNER JOIN ProvIden p ON ie.IdProvIden = p.Id
        WHERE ie.Eliminado = 0 GROUP BY p.Nombre, p.Id ORDER BY COUNT(ie.Id) DESC;
    SELECT TOP 10 Direccion AS Nombre, 1 AS Cantidad FROM IPs ORDER BY Id DESC;
    SELECT TOP 10 LEFT(ISNULL(Detalles,''), 50) + '...' AS Nombre, COUNT(*) AS Cantidad
        FROM AuditoriaPwd WHERE Detalles IS NOT NULL AND Detalles <> ''
        GROUP BY Detalles ORDER BY COUNT(*) DESC;
    SELECT TOP 10 e.Id AS TemplateId, t.Nombre AS TemplateNombre, COUNT(*) AS Cantidad
        FROM EmailLog e LEFT JOIN EmailTemplate t ON e.IdTemplate = t.Id
        WHERE e.IdTemplate IS NOT NULL
        GROUP BY e.Id, t.Nombre ORDER BY COUNT(*) DESC;
END
GO