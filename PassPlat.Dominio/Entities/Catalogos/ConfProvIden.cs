namespace PassPlat.Dominio.Entities.Catalogos;

public class ConfProvIden
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdProvIden { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string? Scopes { get; set; }
    public string Callback { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public int? RolDefecto { get; set; }
    public bool GuardarTokens { get; set; }
    public bool PermitirAutoLink { get; set; }
    public bool AutoProvisionar { get; set; }
    public bool RequiereMFALocal { get; set; }
    public bool RequireEmailVerified { get; set; } = true;
    public bool AllowLoginWithoutRefreshToken { get; set; } = true;
    public bool AllowRefreshTokenRotation { get; set; } = true;
    public byte Estado { get; set; } = 1;
    public string? Metadata { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public bool PermitirLogin { get; set; } = true;
    public bool PermitirCrearUsuario { get; set; } = true;
    public bool PermitirVincular { get; set; } = true;
    public bool PermitirDesvincular { get; set; } = true;
    public bool PermitirPasswordLocal { get; set; } = true;
    public bool ObligaMFA { get; set; }
    public bool PermitirCambioEmail { get; set; } = true;
    public bool PermitirCambioNombre { get; set; } = true;
    public bool PermitirSincronizarAvatar { get; set; } = true;
    public bool PermitirSincronizarPerfil { get; set; } = true;
    public string FrecuenciaSincronizacion { get; set; } = "Siempre";
    public int Prioridad { get; set; }
    public int OrdenVisual { get; set; }
    public string? Logo { get; set; }
    public string? Color { get; set; }
    public string? Tooltip { get; set; }
    public string? Descripcion { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public string? Issuer { get; set; }
    public string ResponseType { get; set; } = "code";
    public string GrantType { get; set; } = "authorization_code";
    public string? ExtraParams { get; set; }

    public Tenant? Tenant { get; set; }
    public ProvIden? ProvIden { get; set; }
    public Rol? RolDefectoNav { get; set; }

    public static ConfProvIden Crear(int idTenant, int idProvIden, string clientId, string clientSecret, string callback,
        string? scopes = null, string? redirectUri = null, int? rolDefecto = null, bool guardarTokens = false,
        bool permitirAutoLink = false, bool autoProvisionar = false, bool requiereMFALocal = false, string? metadata = null,
        bool permitirLogin = true, bool permitirCrearUsuario = true, bool permitirVincular = true, bool permitirDesvincular = true,
        bool permitirPasswordLocal = true, bool obligaMFA = false, bool permitirCambioEmail = true, bool permitirCambioNombre = true,
        bool permitirSincronizarAvatar = true, bool permitirSincronizarPerfil = true, string frecuenciaSincronizacion = "Siempre", int prioridad = 0, int ordenVisual = 0,
        string? logo = null, string? color = null, string? tooltip = null, string? descripcion = null,
        string? authorizationEndpoint = null, string? tokenEndpoint = null, string? jwksUri = null, string? issuer = null,
        string responseType = "code", string grantType = "authorization_code", string? extraParams = null,
        bool requireEmailVerified = true, bool allowLoginWithoutRefreshToken = true, bool allowRefreshTokenRotation = true)
    {
        return new ConfProvIden
        {
            IdTenant = idTenant,
            IdProvIden = idProvIden,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Callback = callback,
            Scopes = scopes,
            RedirectUri = redirectUri,
            RolDefecto = rolDefecto,
            GuardarTokens = guardarTokens,
            PermitirAutoLink = permitirAutoLink,
            AutoProvisionar = autoProvisionar,
            RequiereMFALocal = requiereMFALocal,
            RequireEmailVerified = requireEmailVerified,
            AllowLoginWithoutRefreshToken = allowLoginWithoutRefreshToken,
            AllowRefreshTokenRotation = allowRefreshTokenRotation,
            Metadata = metadata,
            Estado = 1,
            Activo = true,
            PermitirLogin = permitirLogin,
            PermitirCrearUsuario = permitirCrearUsuario,
            PermitirVincular = permitirVincular,
            PermitirDesvincular = permitirDesvincular,
            PermitirPasswordLocal = permitirPasswordLocal,
            ObligaMFA = obligaMFA,
            PermitirCambioEmail = permitirCambioEmail,
            PermitirCambioNombre = permitirCambioNombre,
            PermitirSincronizarAvatar = permitirSincronizarAvatar,
            PermitirSincronizarPerfil = permitirSincronizarPerfil,
            FrecuenciaSincronizacion = frecuenciaSincronizacion,
            Prioridad = prioridad,
            OrdenVisual = ordenVisual,
            Logo = logo,
            Color = color,
            Tooltip = tooltip,
            Descripcion = descripcion,
            AuthorizationEndpoint = authorizationEndpoint,
            TokenEndpoint = tokenEndpoint,
            JwksUri = jwksUri,
            Issuer = issuer,
            ResponseType = responseType,
            GrantType = grantType,
            ExtraParams = extraParams
        };
    }
}
