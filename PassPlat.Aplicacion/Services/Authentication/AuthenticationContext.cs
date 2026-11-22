namespace PassPlat.Aplicacion.Services.Authentication;

public sealed record AuthenticationContext(
    int IdUsuario,
    int? IdTenant,
    int IdApp,
    short? IdDispositivo,
    int? IdIp,
    AuthenticationOrigin Origen,
    bool EsSistema = false,
    int? IdUsuarioTenant = null,
    // S57.19 H-2: código de Apps (autoridad) para resolver audiencia JWT sin hardcode numérico.
    string? AppCodigo = null);
