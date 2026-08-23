using PassPlat.Dominio.Entities.Core;
using PassPlat.Dominio.Models;

namespace PassPlat.Dominio.Entities.Catalogos;

public class ProvIden
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
    public bool Activo { get; set; } = true;
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }
    public OAuthProviderMetadata? Metadata { get; set; }

    public ICollection<ConfProvIden> Configuraciones { get; set; } = [];
    public ICollection<IdenExt> IdenExt { get; set; } = [];
    public ICollection<AudIdenExt> Auditorias { get; set; } = [];

    public static ProvIden Crear(string codigo, string nombre, byte tipoProveedor, string? protocolo = null, string? version = null, string? urlIssuer = null, string? endpointAutorizacion = null, string? endpointToken = null, string? endpointUserInfo = null, string? jwksUri = null, string? endpointRevocacion = null, bool soportaPKCE = false, bool soportaRefreshToken = false, bool soportaMFA = false, string? icono = null, short orden = 0, OAuthProviderMetadata? metadata = null)
    {
        return new ProvIden
        {
            Codigo = codigo,
            Nombre = nombre,
            TipoProveedor = tipoProveedor,
            Protocolo = protocolo,
            Version = version,
            UrlIssuer = urlIssuer,
            EndpointAutorizacion = endpointAutorizacion,
            EndpointToken = endpointToken,
            EndpointUserInfo = endpointUserInfo,
            JwksUri = jwksUri,
            EndpointRevocacion = endpointRevocacion,
            SoportaPKCE = soportaPKCE,
            SoportaRefreshToken = soportaRefreshToken,
            SoportaMFA = soportaMFA,
            Icono = icono,
            Orden = orden,
            Activo = true,
            Metadata = metadata
        };
    }
}
