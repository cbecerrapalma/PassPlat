namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ProviderConfigurationInfoDto
{
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? JwksUri { get; set; }
    public string? Issuer { get; set; }
    public string ResponseType { get; set; } = "code";
    public string GrantType { get; set; } = "authorization_code";
    public bool SoportaPKCE { get; set; }
    public bool SoportaRefreshToken { get; set; }
    public bool SoportaMFA { get; set; }
}
