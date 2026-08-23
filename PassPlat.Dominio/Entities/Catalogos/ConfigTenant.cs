namespace PassPlat.Dominio.Entities.Catalogos;

public class ConfigTenant
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    [Obsolete("Usar ReqMFA en su lugar")]
    public bool MFAObligatorio { get; set; }
    public int TimeoutSesionMin { get; set; } = 30;
    public int MaxSesionesConc { get; set; } = 5;
    public bool ReqMFA { get; set; }
    public int DiasRetAuditoria { get; set; } = 365;
    public byte PepperVersionActual { get; set; } = 1;

    public Tenant? Tenant { get; set; }

    public static ConfigTenant Crear(int idTenant)
    {
        return new ConfigTenant
        {
            IdTenant = idTenant,
            TimeoutSesionMin = 30,
            MaxSesionesConc = 5,
            DiasRetAuditoria = 365,
            PepperVersionActual = 1
        };
    }
}
