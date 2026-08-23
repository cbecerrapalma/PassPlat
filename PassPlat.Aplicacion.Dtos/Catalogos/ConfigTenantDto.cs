namespace PassPlat.Aplicacion.Dtos.Catalogos;

public class ConfigTenantDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    [Obsolete("Usar ReqMFA en su lugar")]
    public bool MFAObligatorio { get; set; }
    public int TimeoutSesionMin { get; set; }
    public int MaxSesionesConc { get; set; }
    public bool ReqMFA { get; set; }
    public int DiasRetAuditoria { get; set; }
    public byte PepperVersionActual { get; set; }
    public string? TenantNombre { get; set; }
}
