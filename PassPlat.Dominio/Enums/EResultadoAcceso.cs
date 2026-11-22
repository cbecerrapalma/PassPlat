namespace PassPlat.Dominio.Enums;

// S123 · D2 — mismos números que la tabla dbo.ResultadosAcceso; 22-28 los agrega Migrations/S123/01_ResultadosAcceso_OAuth.sql
public enum EResultadoAcceso : byte
{
    Exitoso = 1,
    CredencialesInvalidas = 2,
    CuentaBloqueada = 3,
    SinAccesoApp = 4,
    ErrorSistema = 5,
    CuentaInactiva = 6,
    MFARequerido = 7,
    TokenExpirado = 8,
    IPBloqueada = 9,
    SesionExpirada = 10,
    MFAInvalido = 11,
    CuentaSuspendida = 12,
    CambioPwdRequerido = 13,
    AccesoDenegado = 14,
    TokenInvalido = 15,
    DispositivoNoConfiable = 16,
    OAuthError = 17,
    OAuthAutoLinkDenied = 18,
    OAuthRoleDefaultNotConfigured = 19,
    OAuthProviderNotSupported = 20,
    PwdExpirada = 21,
    OAuthLogin = 22,
    OAuthIdentityLinked = 23,
    OAuthProvisioning = 24,
    OAuthProviderDisabled = 25,
    OAuthProviderError = 26,
    OAuthUserWithoutEmail = 27,
    OAuthIdentityRevoked = 28
}
