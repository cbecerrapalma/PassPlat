namespace PassPlat.Dominio.Enums;

public enum ETipoAuditoria : byte
{
    LoginExitoso = 1,
    LoginFallido = 2,
    CambioPassword = 3,
    ResetPassword = 4,
    RevocacionSesiones = 5,
    RegistroMFA = 6,
    EliminacionCuenta = 7,
    CambioPolitica = 8,
    BloqueoCuenta = 9,
    DesbloqueoCuenta = 10,
    LoginExternoExitoso = 11,
    LoginExternoFallido = 12,
    VinculacionIdentidad = 13,
    DesvinculacionIdentidad = 14,
    AutoProvisioning = 15,
    ConfiguracionOAuthCambio = 20
}
