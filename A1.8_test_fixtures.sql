-- ============================================================
-- A1.8 Test Fixtures: 6 test users for multi-tenant certification
-- All users have password: Admin@123
-- ============================================================
SET NOCOUNT, XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @HashPwd nvarchar(512) = '$argon2id$v=19$m=131072,t=4,p=8$g0mWVEDTVZyHiXD+K1ZNCMUmzkzJU0LeK/zV62kruQ4=$E41z6gnr6RmiskoyNix7z6v+4gxyYdQY8ce8f3y/HhO2wZ92UA/gpn6E+vwGa8F+jAXteQr+ze++lDTFv3lz6w==$pv1';
DECLARE @IdPolitica int = 1;
DECLARE @IdTipoCambio int = 4; -- PrimerUso
DECLARE @IdApp int = 1;

-- Clean up any prior test users (idempotent)
DELETE FROM Accesos WHERE IdUsuario IN (SELECT Id FROM Usuarios WHERE NomUsuario LIKE 'test!_%' ESCAPE '!');
DELETE FROM UsuarioTenant WHERE IdUsuario IN (SELECT Id FROM Usuarios WHERE NomUsuario LIKE 'test!_%' ESCAPE '!');
DELETE FROM HistorialPwd WHERE IdUsuario IN (SELECT Id FROM Usuarios WHERE NomUsuario LIKE 'test!_%' ESCAPE '!');
DELETE FROM Usuarios WHERE NomUsuario LIKE 'test!_%' ESCAPE '!';

-- ==========================================
-- 1. test_multitenant
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 1, 'test_multitenant', 'test_mt@passplat.app', 0, 'Multi', 'Tenant', 0, 0, 0, 0, 1);

DECLARE @IdMultiTenant int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdMultiTenant, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo, FecAlta)
VALUES (@IdMultiTenant, 3, 1, 1, SYSDATETIME());
DECLARE @IdUT_A int = SCOPE_IDENTITY();

INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo, FecAlta)
VALUES (@IdMultiTenant, 4, 1, 1, SYSDATETIME());
DECLARE @IdUT_B int = SCOPE_IDENTITY();

-- Acceso platform scope (IdUsuarioTenant IS NULL)
INSERT INTO Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo, FecAsignacion, IdUsuarioTenant)
VALUES (@IdMultiTenant, 1, @IdApp, 4, 1, SYSDATETIME(), NULL);

-- Acceso ABARROTES (IdUsuarioTenant = UT_A)
INSERT INTO Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo, FecAsignacion, IdUsuarioTenant)
VALUES (@IdMultiTenant, 3, @IdApp, 12, 1, SYSDATETIME(), @IdUT_A);

-- Acceso VESTUARIO (IdUsuarioTenant = UT_B)
INSERT INTO Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo, FecAsignacion, IdUsuarioTenant)
VALUES (@IdMultiTenant, 4, @IdApp, 16, 1, SYSDATETIME(), @IdUT_B);

PRINT 'Created test_multitenant (Id=' + CAST(@IdMultiTenant AS varchar) + ')';

-- ==========================================
-- 2. test_tenantA (ABARROTES only)
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 1, 'test_tenantA', 'test_ta@passplat.app', 0, 'Tenant', 'AOnly', 0, 0, 0, 0, 1);

DECLARE @IdTenantA int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdTenantA, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo, FecAlta)
VALUES (@IdTenantA, 3, 1, 1, SYSDATETIME());
DECLARE @IdUT_TA int = SCOPE_IDENTITY();

INSERT INTO Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo, FecAsignacion, IdUsuarioTenant)
VALUES (@IdTenantA, 3, @IdApp, 12, 1, SYSDATETIME(), @IdUT_TA);

PRINT 'Created test_tenantA (Id=' + CAST(@IdTenantA AS varchar) + ')';

-- ==========================================
-- 3. test_tenantB (VESTUARIO only)
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 1, 'test_tenantB', 'test_tb@passplat.app', 0, 'Tenant', 'BOnly', 0, 0, 0, 0, 1);

DECLARE @IdTenantB int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdTenantB, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo, FecAlta)
VALUES (@IdTenantB, 4, 1, 1, SYSDATETIME());
DECLARE @IdUT_TB int = SCOPE_IDENTITY();

INSERT INTO Accesos (IdUsuario, IdTenant, IdApp, IdRol, Activo, FecAsignacion, IdUsuarioTenant)
VALUES (@IdTenantB, 4, @IdApp, 16, 1, SYSDATETIME(), @IdUT_TB);

PRINT 'Created test_tenantB (Id=' + CAST(@IdTenantB AS varchar) + ')';

-- ==========================================
-- 4. test_inactive_memb (UsuarioTenant Activo=0)
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 1, 'test_inactive_memb', 'test_im@passplat.app', 0, 'Inactive', 'Membership', 0, 0, 0, 0, 1);

DECLARE @IdInactMemb int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdInactMemb, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

INSERT INTO UsuarioTenant (IdUsuario, IdTenant, IdEstado, Activo, FecAlta)
VALUES (@IdInactMemb, 3, 1, 0, SYSDATETIME());

PRINT 'Created test_inactive_memb (Id=' + CAST(@IdInactMemb AS varchar) + ')';

-- ==========================================
-- 5. test_inactive_state (User IdEstado=2)
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 2, 'test_inactive_state', 'test_is@passplat.app', 0, 'Inactive', 'State', 0, 0, 0, 0, 1);

DECLARE @IdInactiveState int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdInactiveState, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

PRINT 'Created test_inactive_state (Id=' + CAST(@IdInactiveState AS varchar) + ')';

-- ==========================================
-- 6. test_deleted (User Eliminado=1)
-- ==========================================
INSERT INTO Usuarios (IdTenant, IdEstado, NomUsuario, Email, EmailVerificado, Nombre, Apellido, ReqCambioPwd, IntentosFallidos, Eliminado, EsSistema, TienePasswordLocal)
VALUES (1, 1, 'test_deleted', 'test_del@passplat.app', 0, 'Deleted', 'User', 0, 0, 1, 0, 1);

DECLARE @IdDeleted int = SCOPE_IDENTITY();

INSERT INTO HistorialPwd (IdUsuario, IdPolitica, IdTipoCambio, HashPwd, Algoritmo, PepperVersion, EsActual, EsForzado, EsComprometida, OrigenRegistro, FecRegistro)
VALUES (@IdDeleted, @IdPolitica, @IdTipoCambio, @HashPwd, 'Argon2id', 1, 1, 0, 0, 'LOCAL', SYSDATETIME());

PRINT 'Created test_deleted (Id=' + CAST(@IdDeleted AS varchar) + ')';

COMMIT TRANSACTION;
PRINT 'A1.8 test fixtures created successfully.';
GO
