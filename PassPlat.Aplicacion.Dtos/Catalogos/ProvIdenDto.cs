namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ProvIdenDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte TipoProveedor { get; set; }
    public string? Protocolo { get; set; }
    public string? Version { get; set; }
    public string? UrlIssuer { get; set; }
    public string? EndpointAutorizacion { get; set; }
    public string? EndpointToken { get; set; }
    public string? EndpointUserInfo { get; set; }
    public string? JwksUri { get; set; }
    public string? EndpointRevocacion { get; set; }
    public bool SoportaPKCE { get; set; }
    public bool SoportaRefreshToken { get; set; }
    public bool SoportaMFA { get; set; }
    public string? Icono { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; }
    public DateTime? FecCrea { get; set; }
    public string? Metadata { get; set; }
}

public class CrearProvIdenDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte TipoProveedor { get; set; }
    public string? Protocolo { get; set; }
    public string? Version { get; set; }
    public string? UrlIssuer { get; set; }
    public string? EndpointAutorizacion { get; set; }
    public string? EndpointToken { get; set; }
    public string? EndpointUserInfo { get; set; }
    public string? JwksUri { get; set; }
    public string? EndpointRevocacion { get; set; }
    public bool SoportaPKCE { get; set; }
    public bool SoportaRefreshToken { get; set; }
    public bool SoportaMFA { get; set; }
    public string? Icono { get; set; }
    public short Orden { get; set; }
    public string? Metadata { get; set; }
}

public class ActualizarProvIdenDto
{
    public string? Nombre { get; set; }
    public byte? TipoProveedor { get; set; }
    public string? Protocolo { get; set; }
    public string? Version { get; set; }
    public string? UrlIssuer { get; set; }
    public string? EndpointAutorizacion { get; set; }
    public string? EndpointToken { get; set; }
    public string? EndpointUserInfo { get; set; }
    public string? JwksUri { get; set; }
    public string? EndpointRevocacion { get; set; }
    public bool? SoportaPKCE { get; set; }
    public bool? SoportaRefreshToken { get; set; }
    public bool? SoportaMFA { get; set; }
    public string? Icono { get; set; }
    public short? Orden { get; set; }
    public bool? Activo { get; set; }
    public string? Metadata { get; set; }
}
