namespace PassPlat.Aplicacion.Dtos.Core;

public class TenantEmailAccountDto
{
    public int Id { get; set; }
    public int IdTenant { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
    public bool Activo { get; set; }
    public string? EmailAccountNombre { get; set; }
    public string? TenantNombre { get; set; }
}

public class CrearTenantEmailAccountDto
{
    public int IdTenant { get; set; }
    public int IdEmailAccount { get; set; }
    public bool EsPredeterminada { get; set; }
}
