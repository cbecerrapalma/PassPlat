namespace PassPlat.Aplicacion.Services.Authentication;

public sealed record AuthenticationContext(
    int IdUsuario,
    int? IdTenant,
    int IdApp,
    short? IdDispositivo,
    int? IdIp,
    AuthenticationOrigin Origen,
    bool EsSistema = false,
    int? IdUsuarioTenant = null);
