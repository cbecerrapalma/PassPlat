namespace PassPlat.Dominio.Entities.Catalogos;

public class ConfLdap
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public string Servidor { get; set; } = string.Empty;
    public int Puerto { get; set; } = 389;
    public string BaseDN { get; set; } = string.Empty;
    public string? BindDN { get; set; }
    public string? BindPassword { get; set; }
    public bool UsarSSL { get; set; }
    public bool UsarStartTLS { get; set; }
    public string? FiltroBusqueda { get; set; }
    public string? AtributoEmail { get; set; } = "mail";
    public string? AtributoNombre { get; set; } = "displayName";
    public string? AtributoUid { get; set; } = "sAMAccountName";
    public string? AtributoGrupo { get; set; } = "memberOf";
    public int? TimeoutSeconds { get; set; } = 30;
    public bool AutoProvisionar { get; set; }
    public bool SincronizarGrupos { get; set; }
    public byte Estado { get; set; } = 1;
    public string? Metadata { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? FecCrea { get; set; }
    public DateTime? FecMod { get; set; }

    public Tenant? Tenant { get; set; }

    public static ConfLdap Crear(int idTenant, string servidor, string baseDN, string? bindDN = null, string? bindPassword = null)
    {
        return new ConfLdap
        {
            IdTenant = idTenant,
            Servidor = servidor,
            BaseDN = baseDN,
            BindDN = bindDN,
            BindPassword = bindPassword,
            Estado = 1,
            Activo = true,
            FecCrea = DateTime.Now
        };
    }
}
