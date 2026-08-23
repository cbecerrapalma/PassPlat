namespace PassPlat.Dominio.Enums;

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
    OAuthProvisioning = 10,
    OAuthLogin = 11,
    OAuthProviderDisabled = 12,
    OAuthIdentityLinked = 13,
    OAuthIdentityRevoked = 14,
    OAuthProviderError = 15,
    OAuthUserWithoutEmail = 16,
    OAuthAutoLinkDenied = 17,
    OAuthRoleDefaultNotConfigured = 18
}
