namespace PassPlat.Dominio.Entities.Core;

public class SamlSession
{
    public long Id { get; set; }
    public int IdTenant { get; set; }
    public int? IdUsuario { get; set; }
    public int IdConfSaml { get; set; }
    public string NameId { get; set; } = string.Empty;
    public string? SessionIndex { get; set; }
    public string? NotOnOrAfter { get; set; }
    public string? SubjectConfirmationData { get; set; }
    public string? AttributesJson { get; set; }
    public bool EsActiva { get; set; } = true;
    public DateTime? FecExpira { get; set; }
    public DateTime FecCreacion { get; set; } = DateTime.Now;
    public DateTime? FecRevocacion { get; set; }

    public Tenant? Tenant { get; set; }
    public Usuario? Usuario { get; set; }
    public ConfSaml? ConfSaml { get; set; }

    public static SamlSession Crear(int idTenant, int idConfSaml, string nameId, string? sessionIndex = null, DateTime? fecExpira = null)
    {
        return new SamlSession
        {
            IdTenant = idTenant,
            IdConfSaml = idConfSaml,
            NameId = nameId,
            SessionIndex = sessionIndex,
            FecExpira = fecExpira,
            EsActiva = true,
            FecCreacion = DateTime.Now
        };
    }

    public void Revocar()
    {
        EsActiva = false;
        FecRevocacion = DateTime.Now;
    }
}
