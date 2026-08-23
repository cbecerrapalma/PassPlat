namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ConfProvIdenDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdProvIden { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? Scopes { get; set; }
    public string Callback { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public int? RolDefecto { get; set; }
    public bool GuardarTokens { get; set; }
    public bool PermitirAutoLink { get; set; }
    public bool AutoProvisionar { get; set; }
    public bool RequiereMFALocal { get; set; }
    public byte Estado { get; set; }
    public string? Metadata { get; set; }
    public bool Activo { get; set; }
    public DateTime? FecCrea { get; set; }
    public string? ProvIdenNombre { get; set; }
    public string? TenantNombre { get; set; }
    public string? RolDefectoNombre { get; set; }
    public bool TieneClientSecret { get; set; }
    public DateTime? FechaCambioSecret { get; set; }

    public bool PermitirLogin { get; set; }
    public bool PermitirCrearUsuario { get; set; }
    public bool PermitirVincular { get; set; }
    public bool PermitirDesvincular { get; set; }
    public bool PermitirPasswordLocal { get; set; }
    public bool ObligaMFA { get; set; }
    public bool PermitirCambioEmail { get; set; }
    public bool PermitirCambioNombre { get; set; }
    public bool PermitirSincronizarAvatar { get; set; }
    public bool PermitirSincronizarPerfil { get; set; }
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
}

public class CrearConfProvIdenDto
{
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
    public string? Metadata { get; set; }
    public byte Estado { get; set; } = 1;

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
}

public class ActualizarConfProvIdenDto
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scopes { get; set; }
    public string? Callback { get; set; }
    public string? RedirectUri { get; set; }
    public int? RolDefecto { get; set; }
    public bool? GuardarTokens { get; set; }
    public bool? PermitirAutoLink { get; set; }
    public bool? AutoProvisionar { get; set; }
    public bool? RequiereMFALocal { get; set; }
    public string? Metadata { get; set; }
    public byte? Estado { get; set; }

    public bool? PermitirLogin { get; set; }
    public bool? PermitirCrearUsuario { get; set; }
    public bool? PermitirVincular { get; set; }
    public bool? PermitirDesvincular { get; set; }
    public bool? PermitirPasswordLocal { get; set; }
    public bool? ObligaMFA { get; set; }
    public bool? PermitirCambioEmail { get; set; }
    public bool? PermitirCambioNombre { get; set; }
    public bool? PermitirSincronizarAvatar { get; set; }
    public bool? PermitirSincronizarPerfil { get; set; }
    public string? FrecuenciaSincronizacion { get; set; }
    public int? Prioridad { get; set; }
    public int? OrdenVisual { get; set; }
    public string? Logo { get; set; }
    public string? Color { get; set; }
    public string? Tooltip { get; set; }
    public string? Descripcion { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public string? Issuer { get; set; }
    public string? ResponseType { get; set; }
    public string? GrantType { get; set; }
    public string? ExtraParams { get; set; }
}
