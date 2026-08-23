namespace PassPlat.Dominio.Entities.Catalogos;

public class ConfSaml
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public string? MetadataUrl { get; set; }
    public string? MetadataXml { get; set; }
    public string? Certificate { get; set; }
    public string? SignatureAlgorithm { get; set; } = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public string? DigestAlgorithm { get; set; } = "http://www.w3.org/2001/04/xmlenc#sha256";
    public string? SsoUrl { get; set; }
    public string? SloUrl { get; set; }
    public string? AttributeEmail { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
    public string? AttributeNombre { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
    public string? AttributeUid { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
    public bool WantsAssertionsSigned { get; set; } = true;
    public bool AutenticacionRequestSigned { get; set; }
    public bool AllowCreate { get; set; } = true;
    public bool AutoProvisionar { get; set; }
    public byte Estado { get; set; } = 1;
    public string? Metadata { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Tenant? Tenant { get; set; }

    public static ConfSaml Crear(int idTenant, string entityId, string? ssoUrl = null)
    {
        return new ConfSaml
        {
            IdTenant = idTenant,
            EntityId = entityId,
            SsoUrl = ssoUrl,
            Estado = 1,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
